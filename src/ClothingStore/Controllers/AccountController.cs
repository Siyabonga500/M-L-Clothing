using ClothingStore.Data;
using ClothingStore.Models;
using ClothingStore.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace ClothingStore.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;

    public AccountController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager)
    {
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
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        if (!ModelState.IsValid) return View(model);

        var user = await _userManager.FindByEmailAsync(model.Email)
                   ?? await _userManager.FindByNameAsync(model.Email);
        if (user != null)
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

        ModelState.AddModelError(string.Empty, "Invalid email or password.");
        return View(model);
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
