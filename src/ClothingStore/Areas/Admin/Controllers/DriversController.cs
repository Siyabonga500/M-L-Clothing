using ClothingStore.Data;
using ClothingStore.Models;
using ClothingStore.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClothingStore.Areas.Admin.Controllers;

/// <summary>Drivers can only be registered by an admin.</summary>
[Area("Admin"), Authorize(Roles = Roles.Admin)]
public class DriversController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public DriversController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        var drivers = (await _userManager.GetUsersInRoleAsync(Roles.Driver)).OrderBy(d => d.FirstName).ToList();
        var active = new[] { OrderStatus.DriverAssigned, OrderStatus.OutForDelivery };
        var counts = await _db.Orders.Where(o => o.DriverId != null)
            .GroupBy(o => o.DriverId!)
            .Select(g => new
            {
                g.Key,
                Active = g.Count(o => active.Contains(o.Status)),
                Done = g.Count(o => o.Status == OrderStatus.Delivered)
            })
            .ToDictionaryAsync(x => x.Key);
        var ratings = await _db.DriverReviews.GroupBy(r => r.DriverId)
            .Select(g => new { g.Key, Avg = g.Average(r => r.Rating), Count = g.Count() })
            .ToDictionaryAsync(x => x.Key);

        return View(drivers.Select(d => new AdminUserViewModel
        {
            User = d,
            IsDriver = true,
            IsLocked = d.LockoutEnd.HasValue && d.LockoutEnd > DateTimeOffset.UtcNow,
            ActiveDeliveries = counts.TryGetValue(d.Id, out var c) ? c.Active : 0,
            OrderCount = counts.TryGetValue(d.Id, out var c2) ? c2.Done : 0,
            Rating = ratings.TryGetValue(d.Id, out var r) ? r.Avg : 0,
            ReviewCount = ratings.TryGetValue(d.Id, out var r2) ? r2.Count : 0
        }).ToList());
    }

    [HttpGet]
    public IActionResult Create() => View(new DriverCreateViewModel { Gender = "" });

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(DriverCreateViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var driver = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            EmailConfirmed = true,
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
            HomeLongitude = model.HomeLongitude,
            VehicleDescription = model.VehicleDescription,
            VehicleRegistration = model.VehicleRegistration
        };

        var result = await _userManager.CreateAsync(driver, model.Password);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors) ModelState.AddModelError(string.Empty, e.Description);
            return View(model);
        }

        await _userManager.AddToRoleAsync(driver, Roles.Driver);
        TempData["Success"] = $"Driver {driver.FullName} registered. They can log in with {driver.Email}.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        var d = await _userManager.FindByIdAsync(id);
        if (d == null || !await _userManager.IsInRoleAsync(d, Roles.Driver)) return NotFound();
        return View(new ProfileViewModel
        {
            FirstName = d.FirstName, Surname = d.Surname, Email = d.Email ?? "", Gender = d.Gender,
            DateOfBirth = d.DateOfBirth, PhoneNumber = d.PhoneNumber, HomeAddress = d.HomeAddress,
            City = d.City, Province = d.Province, PostalCode = d.PostalCode,
            HomeLatitude = d.HomeLatitude, HomeLongitude = d.HomeLongitude,
            VehicleDescription = d.VehicleDescription, VehicleRegistration = d.VehicleRegistration
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, ProfileViewModel model)
    {
        var d = await _userManager.FindByIdAsync(id);
        if (d == null || !await _userManager.IsInRoleAsync(d, Roles.Driver)) return NotFound();
        model.Email = d.Email ?? "";
        if (!ModelState.IsValid) return View(model);

        d.FirstName = model.FirstName;
        d.Surname = model.Surname;
        d.Gender = model.Gender;
        d.DateOfBirth = model.DateOfBirth;
        d.PhoneNumber = model.PhoneNumber;
        d.HomeAddress = model.HomeAddress;
        d.City = model.City;
        d.Province = model.Province;
        d.PostalCode = model.PostalCode;
        d.HomeLatitude = model.HomeLatitude;
        d.HomeLongitude = model.HomeLongitude;
        d.VehicleDescription = model.VehicleDescription;
        d.VehicleRegistration = model.VehicleRegistration;
        await _userManager.UpdateAsync(d);

        TempData["Success"] = $"{d.FullName} updated.";
        return RedirectToAction(nameof(Index));
    }
}
