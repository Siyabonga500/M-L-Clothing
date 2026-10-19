using ClothingStore.Data;
using ClothingStore.Models;
using ClothingStore.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ClothingStore.Services;

namespace ClothingStore.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly EmailService _email;
    private readonly StoreSettings _store;

    public AccountController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager,
        EmailService email, IOptions<StoreSettings> store)
    {
        _email = email;
        _store = store.Value;
        _userManager = userManager;
        _signInManager = signInManager;
    }

    [HttpGet]
    public IActionResult Register(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View(new RegisterViewModel());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        if (!ModelState.IsValid) return View(model);

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            FirstName = model.FirstName,
            Surname = model.Surname,
            Gender = model.Gender,
            DateOfBirth = model.DateOfBirth,
            PhoneNumber = model.PhoneNumber,
            HomeAddress = model.HomeAddress,
            City = model.City,
            Province = model.Province,
            PostalCode = model.PostalCode,
            HomeLatitude = model.HomeLatitude,
            HomeLongitude = model.HomeLongitude
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (result.Succeeded)
        {
            await _userManager.AddToRoleAsync(user, Roles.Customer);
            TempData["Success"] = "Your account was created. Please log in.";
            return RedirectToAction(nameof(Login), new { returnUrl });
        }

        foreach (var error in result.Errors)
            ModelState.AddModelError(string.Empty, error.Description);
        return View(model);
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null, string? email = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel { Email = email ?? "" });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        if (!ModelState.IsValid) return View(model);

        var user = await _userManager.FindByEmailAsync(model.Email)
                   ?? await _userManager.FindByNameAsync(model.Email);
        if (user == null)
        {
            ModelState.AddModelError(string.Empty, "Wrong username: no account uses that email address.");
            return View(model);
        }
        {
            var result = await _signInManager.PasswordSignInAsync(user, model.Password, model.RememberMe, lockoutOnFailure: true);
            if (result.Succeeded)
            {
                if (Url.IsLocalUrl(returnUrl)) return LocalRedirect(returnUrl);
                if (await _userManager.IsInRoleAsync(user, Roles.Admin))
                    return RedirectToAction("Index", "Dashboard", new { area = "Admin" });
                if (await _userManager.IsInRoleAsync(user, Roles.Driver))
                    return RedirectToAction("Index", "Deliveries", new { area = "Driver" });
                return RedirectToAction("Index", "Home");
            }
            if (result.IsLockedOut)
            {
                ModelState.AddModelError(string.Empty, "This account is locked. Try again later or contact the administrator.");
                return View(model);
            }
        }

        ModelState.AddModelError(string.Empty, "Wrong password. Try again or use \"Forgot password?\" to reset it.");
        return View(model);
    }

    // ---------- Forgot password: verify email + phone, then choose a new password ----------

    [HttpGet]
    public IActionResult ForgotPassword(string? email = null) => View(new ForgotPasswordViewModel { Email = email ?? "" });

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await _userManager.FindByEmailAsync(model.Email.Trim());
        if (user == null)
        {
            ModelState.AddModelError(string.Empty, "Wrong username: no account uses that email address.");
            return View(model);
        }
        if (await _userManager.IsLockedOutAsync(user))
        {
            var minutes = Math.Max(1, (int)Math.Ceiling(((user.LockoutEnd ?? DateTimeOffset.UtcNow) - DateTimeOffset.UtcNow).TotalMinutes));
            ModelState.AddModelError(string.Empty, minutes > 60 * 24
                ? "This account is locked. Please contact the store."
                : $"Too many wrong attempts. Please try again in {minutes} minute(s).");
            return View(model);
        }
        if (string.IsNullOrWhiteSpace(user.PhoneNumber))
        {
            ModelState.AddModelError(string.Empty, "This account has no phone number saved, so it can't be reset here. Please contact the store.");
            return View(model);
        }
        if (NormalizePhone(user.PhoneNumber) != NormalizePhone(model.Phone))
        {
            // Counts towards the normal lockout (5 wrong tries locks the account for a few minutes).
            await _userManager.AccessFailedAsync(user);
            ModelState.AddModelError(string.Empty, "That phone number doesn't match the one on this account.");
            return View(model);
        }

        await _userManager.ResetAccessFailedCountAsync(user);
        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        ModelState.Clear();
        return View("ResetPassword", new ResetPasswordViewModel { UserId = user.Id, Token = token, Email = user.Email ?? "" });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await _userManager.FindByIdAsync(model.UserId);
        if (user == null) return RedirectToAction(nameof(ForgotPassword));

        var result = await _userManager.ResetPasswordAsync(user, model.Token, model.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors)
                ModelState.AddModelError(string.Empty, e.Code == "InvalidToken"
                    ? "This reset link has expired. Please start again."
                    : e.Description);
            return View(model);
        }

        await _email.SendAsync(user.Email!, "Your password was changed",
            EmailService.Layout(_store.Name, "Password changed", $"""
                <p>Hi {System.Net.WebUtility.HtmlEncode(user.FirstName)},</p>
                <p>The password for your {System.Net.WebUtility.HtmlEncode(_store.Name)} account was just changed.</p>
                <p>If this wasn't you, please contact the store straight away.</p>
                """));

        TempData["Success"] = "Your password has been reset. Please log in with your new password.";
        return RedirectToAction(nameof(Login), new { email = user.Email });
    }

    /// <summary>Compares phone numbers by digits only, treating +27 / 27 the same as a leading 0.</summary>
    private static string NormalizePhone(string phone)
    {
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.StartsWith("27") && digits.Length == 11) digits = "0" + digits[2..];
        return digits;
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    [Authorize, HttpGet]
    public async Task<IActionResult> Profile()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();
        return View(new ProfileViewModel
        {
            FirstName = user.FirstName,
            Surname = user.Surname,
            Email = user.Email ?? "",
            Gender = user.Gender,
            DateOfBirth = user.DateOfBirth,
            PhoneNumber = user.PhoneNumber,
            HomeAddress = user.HomeAddress,
            City = user.City,
            Province = user.Province,
            PostalCode = user.PostalCode,
            HomeLatitude = user.HomeLatitude,
            HomeLongitude = user.HomeLongitude,
            VehicleDescription = user.VehicleDescription,
            VehicleRegistration = user.VehicleRegistration
        });
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(ProfileViewModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();
        model.Email = user.Email ?? "";
        if (!ModelState.IsValid) return View(model);

        user.FirstName = model.FirstName;
        user.Surname = model.Surname;
        user.Gender = model.Gender;
        user.DateOfBirth = model.DateOfBirth;
        user.PhoneNumber = model.PhoneNumber;
        user.HomeAddress = model.HomeAddress;
        user.City = model.City;
        user.Province = model.Province;
        user.PostalCode = model.PostalCode;
        user.HomeLatitude = model.HomeLatitude;
        user.HomeLongitude = model.HomeLongitude;
        await _userManager.UpdateAsync(user);

        TempData["Success"] = "Profile updated.";
        return RedirectToAction(nameof(Profile));
    }

    public IActionResult AccessDenied() => View();
}
