using ClothingStore.Data;
using ClothingStore.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClothingStore.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Roles = Roles.Admin)]
public class OrdersController : Controller
{
    private readonly ApplicationDbContext _db;

    public OrdersController(ApplicationDbContext db) => _db = db;

    public async Task<IActionResult> Index(OrderStatus? status)
    {
        var query = _db.Orders.Include(o => o.User).Include(o => o.Items).AsQueryable();
        if (status.HasValue) query = query.Where(o => o.Status == status);
        ViewData["Status"] = status;
        return View(await query.OrderByDescending(o => o.CreatedAt).ToListAsync());
    }

    public async Task<IActionResult> Details(int id)
    {
        var order = await _db.Orders.Include(o => o.User).Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id);
        return order == null ? NotFound() : View(order);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int id, OrderStatus status)
    {
        var order = await _db.Orders.FindAsync(id);
        if (order == null) return NotFound();
        order.Status = status;
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Order #{id} is now {status}.";
        return RedirectToAction(nameof(Details), new { id });
    }
}
