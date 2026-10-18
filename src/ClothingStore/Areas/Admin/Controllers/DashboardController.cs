using ClothingStore.Data;
using ClothingStore.Models;
using ClothingStore.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClothingStore.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Roles = Roles.Admin)]
public class DashboardController : Controller
{
    private readonly ApplicationDbContext _db;

    public DashboardController(ApplicationDbContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        var paidStatuses = new[] { OrderStatus.Paid, OrderStatus.Processing, OrderStatus.Shipped, OrderStatus.Delivered };
        // SQLite can't sum decimals in SQL, so add them up in memory.
        var paidTotals = await _db.Orders.Where(o => paidStatuses.Contains(o.Status)).Select(o => o.Total).ToListAsync();

        var model = new AdminDashboardViewModel
        {
            UserCount = await _db.Users.CountAsync(),
            ProductCount = await _db.Products.CountAsync(),
            OrderCount = await _db.Orders.CountAsync(),
            PendingOrders = await _db.Orders.CountAsync(o => o.Status == OrderStatus.Paid || o.Status == OrderStatus.Processing),
            Revenue = paidTotals.Sum(),
            RecentOrders = await _db.Orders.Include(o => o.User)
                .OrderByDescending(o => o.CreatedAt).Take(8).ToListAsync()
        };
        return View(model);
    }
}
