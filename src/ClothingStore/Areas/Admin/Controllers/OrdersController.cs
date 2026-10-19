using ClothingStore.Data;
using ClothingStore.Models;
using ClothingStore.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClothingStore.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Roles = Roles.Admin)]
public class OrdersController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly DeliveryService _delivery;
    private readonly UserManager<ApplicationUser> _users;

    public OrdersController(ApplicationDbContext db, DeliveryService delivery, UserManager<ApplicationUser> users)
    {
        _db = db;
        _delivery = delivery;
        _users = users;
    }

    private string BaseUrl => $"{Request.Scheme}://{Request.Host}";

    public async Task<IActionResult> Index(OrderStatus? status, string? filter)
    {
        var query = _db.Orders.Include(o => o.User).Include(o => o.Driver).Include(o => o.Items)
            .Where(o => o.Status != OrderStatus.PendingPayment || status == OrderStatus.PendingPayment);
        if (status.HasValue) query = query.Where(o => o.Status == status);
        if (filter == "needs-driver")
            query = query.Where(o => o.Fulfilment == FulfilmentMethod.Delivery && o.DriverId == null &&
                                     (o.Status == OrderStatus.Paid || o.Status == OrderStatus.Processing));
        ViewData["Status"] = status;
        ViewData["Filter"] = filter;
        return View(await query.OrderByDescending(o => o.CreatedAt).ToListAsync());
    }

    public async Task<IActionResult> Details(int id)
    {
        var order = await _delivery.LoadAsync(id);
        if (order == null) return NotFound();

        var active = new[] { OrderStatus.DriverAssigned, OrderStatus.OutForDelivery };
        var drivers = await _users.GetUsersInRoleAsync(Roles.Driver);
        var load = await _db.Orders.Where(o => o.DriverId != null && active.Contains(o.Status))
            .GroupBy(o => o.DriverId!).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);
        ViewData["Drivers"] = drivers
            .Where(d => !(d.LockoutEnd > DateTimeOffset.UtcNow))
            .OrderBy(d => d.FirstName)
            .Select(d => (d, load.GetValueOrDefault(d.Id)))
            .ToList();
        return View(order);
    }

    // Camera scanner for confirming collections at the counter.
    public IActionResult Scan() => View("~/Views/Shared/QrScan.cshtml");

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignDriver(int id, string driverId)
    {
        var error = await _delivery.AssignDriverAsync(id, driverId, BaseUrl);
        TempData[error == null ? "Success" : "Error"] = error ??
            "Driver assigned and accepted automatically. The customer was emailed their 3-day delivery window and QR code.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ReadyForCollection(int id)
    {
        var error = await _delivery.MarkReadyForCollectionAsync(id, BaseUrl);
        TempData[error == null ? "Success" : "Error"] = error ?? "Marked ready. The customer was emailed their collection QR code.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RegenerateQr(int id)
    {
        var error = await _delivery.RegenerateQrAsync(id, BaseUrl);
        TempData[error == null ? "Success" : "Error"] = error ?? "New QR code issued and emailed to the customer. The old one no longer works.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int id, OrderStatus status)
    {
        var order = await _db.Orders.FindAsync(id);
        if (order == null) return NotFound();
        order.Status = status;
        _delivery.AddEvent(order, $"Status changed to {status.Display()} by admin.");
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Order #{id} is now {status.Display()}.";
        return RedirectToAction(nameof(Details), new { id });
    }
}
