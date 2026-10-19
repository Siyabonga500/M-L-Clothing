using System.Security.Claims;
using ClothingStore.Data;
using ClothingStore.Filters;
using ClothingStore.Models;
using ClothingStore.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClothingStore.Controllers;

[Authorize, ShoppersOnly]
public class OrdersController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ReceiptService _receipts;

    public OrdersController(ApplicationDbContext db, ReceiptService receipts)
    {
        _db = db;
        _receipts = receipts;
    }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    public async Task<IActionResult> Index()
    {
        var orders = await _db.Orders.Include(o => o.Items)
            .Where(o => o.UserId == UserId && o.Status != OrderStatus.PendingPayment)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();
        return View(orders);
    }

    public async Task<IActionResult> Details(int id)
    {
        var order = await _db.Orders.Include(o => o.Items).Include(o => o.Driver)
            .Include(o => o.Events.OrderByDescending(e => e.CreatedAt))
            .FirstOrDefaultAsync(o => o.Id == id && o.UserId == UserId);
        return order == null ? NotFound() : View(order);
    }

    // Proof of payment as a PDF (download / save).
    public async Task<IActionResult> Receipt(int id)
    {
        var order = await _receipts.LoadAsync(id);
        if (order == null || order.UserId != UserId || !order.IsPaid) return NotFound();
        return File(_receipts.CreatePdf(order), "application/pdf", ReceiptService.FileName(order));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EmailReceipt(int id)
    {
        var order = await _receipts.LoadAsync(id);
        if (order == null || order.UserId != UserId || !order.IsPaid) return NotFound();
        await _receipts.SendAsync(order, $"{Request.Scheme}://{Request.Host}");
        TempData["Success"] = $"Your proof of payment was sent to {order.User!.Email}.";
        return RedirectToAction(nameof(Details), new { id });
    }

    // The QR image. Only the customer who owns the order can load it, and only while it's active.
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Qr(int id)
    {
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == id && o.UserId == UserId);
        if (order == null || order.QrState != QrState.Active) return NotFound();
        var url = DeliveryService.ScanUrl($"{Request.Scheme}://{Request.Host}", order.QrToken!);
        return File(QrService.Png(url), "image/png");
    }

    public async Task<IActionResult> Track(int id)
    {
        var order = await _db.Orders.Include(o => o.Driver)
            .FirstOrDefaultAsync(o => o.Id == id && o.UserId == UserId);
        if (order == null || !order.IsDelivery) return NotFound();
        return View(order);
    }

    // Polled by the tracking map every few seconds.
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> TrackData(int id)
    {
        var o = await _db.Orders.FirstOrDefaultAsync(o => o.Id == id && o.UserId == UserId);
        if (o == null) return NotFound();
        return Json(new
        {
            status = o.Status.ToString(),
            statusText = o.Status.Display(),
            driverLat = o.Status == OrderStatus.OutForDelivery ? o.DriverLatitude : null,
            driverLng = o.Status == OrderStatus.OutForDelivery ? o.DriverLongitude : null,
            updated = o.DriverLocationUpdatedAt?.ToLocalTime().ToString("HH:mm:ss")
        });
    }
}
