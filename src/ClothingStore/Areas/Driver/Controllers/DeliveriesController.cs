using ClothingStore.Data;
using ClothingStore.Models;
using ClothingStore.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClothingStore.Areas.Driver.Controllers;

[Area("Driver"), Authorize(Roles = Roles.Driver)]
public class DeliveriesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly DeliveryService _delivery;
    private readonly UserManager<ApplicationUser> _users;

    public DeliveriesController(ApplicationDbContext db, DeliveryService delivery, UserManager<ApplicationUser> users)
    {
        _db = db;
        _delivery = delivery;
        _users = users;
    }

    private string DriverId => _users.GetUserId(User)!;

    public async Task<IActionResult> Index()
    {
        var orders = await _db.Orders.Include(o => o.Items)
            .Where(o => o.DriverId == DriverId)
            .OrderBy(o => o.Status == OrderStatus.Delivered)
            .ThenByDescending(o => o.DriverAssignedAt)
            .ToListAsync();
        return View(orders);
    }

    public async Task<IActionResult> Details(int id)
    {
        var order = await _delivery.LoadAsync(id);
        if (order == null || order.DriverId != DriverId) return NotFound();
        return View(order);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            TempData["Error"] = "Please give a reason so the admin can reassign the delivery.";
            return RedirectToAction(nameof(Details), new { id });
        }
        var error = await _delivery.RejectAsync(id, DriverId, reason.Trim());
        if (error != null)
        {
            TempData["Error"] = error;
            return RedirectToAction(nameof(Details), new { id });
        }
        TempData["Success"] = $"You rejected order #{id}. The admin has been notified.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Depart(int id)
    {
        var error = await _delivery.DepartAsync(id, DriverId, $"{Request.Scheme}://{Request.Host}");
        TempData[error == null ? "Success" : "Error"] = error ??
            "The customer has been notified that you're on the way. Keep this page open to share your live location.";
        return RedirectToAction(nameof(Details), new { id });
    }

    // Called by the driver's browser every few seconds while out for delivery.
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Location(int id, double lat, double lng)
    {
        if (lat is < -90 or > 90 || lng is < -180 or > 180) return BadRequest();
        return await _delivery.UpdateDriverLocationAsync(id, DriverId, lat, lng) ? Ok() : NotFound();
    }

    public IActionResult Scan() => View("~/Views/Shared/QrScan.cshtml");
}
