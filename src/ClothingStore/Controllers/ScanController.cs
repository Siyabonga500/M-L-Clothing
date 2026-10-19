using ClothingStore.Data;
using ClothingStore.Models;
using ClothingStore.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClothingStore.Controllers;

/// <summary>
/// The URL inside every QR code. A driver (delivery) or admin (collection) scans it,
/// sees the order and taps Confirm. Customers can't confirm their own order.
/// </summary>
[Authorize]
public class ScanController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly DeliveryService _delivery;
    private readonly UserManager<ApplicationUser> _users;

    public ScanController(ApplicationDbContext db, DeliveryService delivery, UserManager<ApplicationUser> users)
    {
        _db = db;
        _delivery = delivery;
        _users = users;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? token)
    {
        if (!User.IsInRole(Roles.Driver) && !User.IsInRole(Roles.Admin))
        {
            TempData["Info"] = "This QR code is for your driver to scan when they deliver your order.";
            return RedirectToAction("Index", "Orders");
        }
        if (string.IsNullOrWhiteSpace(token)) return View(model: null);

        var order = await _db.Orders.Include(o => o.Items).Include(o => o.User)
            .FirstOrDefaultAsync(o => o.QrToken == token.Trim().ToLower());
        ViewData["Token"] = token;
        if (order == null)
            ViewData["Error"] = "This QR code isn't valid. It may have been replaced by a newer one.";
        else if (User.IsInRole(Roles.Driver) && !User.IsInRole(Roles.Admin) && order.DriverId != _users.GetUserId(User))
        {
            ViewData["Error"] = "This order is not assigned to you.";
            order = null;
        }
        return View(order);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirm(string token)
    {
        if (!User.IsInRole(Roles.Driver) && !User.IsInRole(Roles.Admin)) return Forbid();
        var me = await _users.GetUserAsync(User);
        var (order, error) = await _delivery.ConfirmByQrAsync(token ?? "", me!, User.IsInRole(Roles.Admin));

        if (error != null)
        {
            TempData["Error"] = error;
            return RedirectToAction(nameof(Index), new { token });
        }

        TempData["Success"] = $"Order #{order!.Id} confirmed as {order.Status.Display().ToLower()}.";
        return User.IsInRole(Roles.Admin)
            ? RedirectToAction("Details", "Orders", new { area = "Admin", id = order.Id })
            : RedirectToAction("Index", "Deliveries", new { area = "Driver" });
    }
}
