using ClothingStore.Data;
using ClothingStore.Models;
using ClothingStore.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClothingStore.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Roles = Roles.Admin)]
public class DashboardController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public DashboardController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        var paidStatuses = new[] { OrderStatus.Paid, OrderStatus.Processing, OrderStatus.DriverAssigned, OrderStatus.OutForDelivery,
                                   OrderStatus.Delivered, OrderStatus.ReadyForCollection, OrderStatus.Collected };
        // SQLite can't sum decimals in SQL, so add them up in memory.
        var paidTotals = await _db.Orders.Where(o => paidStatuses.Contains(o.Status)).Select(o => o.Total).ToListAsync();

        var model = new AdminDashboardViewModel
        {
            UserCount = await _db.Users.CountAsync(),
            ProductCount = await _db.Products.CountAsync(),
            OrderCount = await _db.Orders.CountAsync(),
            PendingOrders = await _db.Orders.CountAsync(o => o.Status == OrderStatus.Paid || o.Status == OrderStatus.Processing),
            NeedsDriver = await _db.Orders.CountAsync(o => o.Fulfilment == FulfilmentMethod.Delivery && o.DriverId == null &&
                                                        (o.Status == OrderStatus.Paid || o.Status == OrderStatus.Processing)),
            ReadyToPrepare = await _db.Orders.CountAsync(o => o.Fulfilment == FulfilmentMethod.Collection &&
                                                           (o.Status == OrderStatus.Paid || o.Status == OrderStatus.Processing)),
            OutForDelivery = await _db.Orders.CountAsync(o => o.Status == OrderStatus.OutForDelivery),
            DriverCount = (await _userManager.GetUsersInRoleAsync(Roles.Driver)).Count,
            Revenue = paidTotals.Sum(),
            RecentOrders = await _db.Orders.Include(o => o.User)
                .Where(o => o.Status != OrderStatus.PendingPayment)
                .OrderByDescending(o => o.CreatedAt).Take(8).ToListAsync()
        };
        return View(model);
    }
}
