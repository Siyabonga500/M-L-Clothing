using ClothingStore.Data;
using ClothingStore.Models;
using ClothingStore.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClothingStore.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Roles = Roles.Admin)]
public class UsersController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public UsersController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index(string? search)
    {
        var query = _userManager.Users.AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(u => u.Email!.Contains(search) || u.FirstName.Contains(search) || u.Surname.Contains(search));

        var users = await query.OrderBy(u => u.FirstName).ToListAsync();
        var adminIds = (await _userManager.GetUsersInRoleAsync(Roles.Admin)).Select(u => u.Id).ToHashSet();
        var driverIds = (await _userManager.GetUsersInRoleAsync(Roles.Driver)).Select(u => u.Id).ToHashSet();
        var orderCounts = await _db.Orders.GroupBy(o => o.UserId)
            .Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);

        ViewData["Search"] = search;
        return View(users.Select(u => new AdminUserViewModel
        {
            User = u,
            IsAdmin = adminIds.Contains(u.Id),
            IsDriver = driverIds.Contains(u.Id),
            IsLocked = u.LockoutEnd.HasValue && u.LockoutEnd > DateTimeOffset.UtcNow,
            OrderCount = orderCounts.GetValueOrDefault(u.Id)
        }).ToList());
    }

    public async Task<IActionResult> Details(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        ViewData["Orders"] = await _db.Orders.Where(o => o.UserId == id || o.DriverId == id)
            .OrderByDescending(o => o.CreatedAt).ToListAsync();
        return View(new AdminUserViewModel
        {
            User = user,
            IsAdmin = await _userManager.IsInRoleAsync(user, Roles.Admin),
            IsDriver = await _userManager.IsInRoleAsync(user, Roles.Driver),
            IsLocked = user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow
        });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();
        return View(new ProfileViewModel
        {
            FirstName = user.FirstName, Surname = user.Surname, Email = user.Email ?? "",
            Gender = user.Gender, DateOfBirth = user.DateOfBirth, PhoneNumber = user.PhoneNumber,
            HomeAddress = user.HomeAddress, City = user.City, Province = user.Province, PostalCode = user.PostalCode
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, ProfileViewModel model)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();
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
        await _userManager.UpdateAsync(user);

        TempData["Success"] = $"{user.FullName} updated.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleLock(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();
        if (IsSelf(user)) return SelfError();

        var locked = user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow;
        await _userManager.SetLockoutEnabledAsync(user, true);
        await _userManager.SetLockoutEndDateAsync(user, locked ? null : DateTimeOffset.MaxValue);
        TempData["Success"] = locked ? $"{user.Email} unlocked." : $"{user.Email} locked.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleAdmin(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();
        if (IsSelf(user)) return SelfError();

        if (await _userManager.IsInRoleAsync(user, Roles.Admin))
        {
            await _userManager.RemoveFromRoleAsync(user, Roles.Admin);
            TempData["Success"] = $"{user.Email} is no longer an admin.";
        }
        else
        {
            await _userManager.AddToRoleAsync(user, Roles.Admin);
            TempData["Success"] = $"{user.Email} is now an admin.";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(string id, string newPassword)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, newPassword ?? "");
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded
            ? $"Password for {user.Email} was reset."
            : string.Join(" ", result.Errors.Select(e => e.Description));
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();
        if (IsSelf(user)) return SelfError();

        await _userManager.DeleteAsync(user);
        TempData["Success"] = $"{user.Email} deleted.";
        return RedirectToAction(nameof(Index));
    }

    private bool IsSelf(ApplicationUser user) => user.Id == _userManager.GetUserId(User);

    private IActionResult SelfError()
    {
        TempData["Error"] = "You can't do that to your own account.";
        return RedirectToAction(nameof(Index));
    }
}
