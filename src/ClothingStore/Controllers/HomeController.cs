using System.Diagnostics;
using ClothingStore.Data;
using ClothingStore.Models;
using ClothingStore.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClothingStore.Controllers;

public class HomeController : Controller
{
    private readonly ApplicationDbContext _db;

    public HomeController(ApplicationDbContext db) => _db = db;

    public async Task<IActionResult> Index(string? category, string? search, decimal? minPrice, decimal? maxPrice)
    {
        var query = _db.Products.Include(p => p.Images).Include(p => p.Sizes).Where(p => p.IsActive);
        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(p => p.Category == category);
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(p => p.Name.Contains(search) || (p.Description != null && p.Description.Contains(search)));

        // Swap the numbers if someone types the bigger one first.
        if (minPrice > maxPrice) (minPrice, maxPrice) = (maxPrice, minPrice);

        // SQLite can't compare decimal prices in SQL, so the price range is applied in memory.
        var products = (await query.OrderByDescending(p => p.CreatedAt).ToListAsync())
            .Where(p => (!minPrice.HasValue || p.Price >= minPrice) && (!maxPrice.HasValue || p.Price <= maxPrice))
            .ToList();

        var model = new HomeViewModel
        {
            Products = products,
            MinPrice = minPrice,
            MaxPrice = maxPrice,
            Categories = await _db.Products.Where(p => p.IsActive).Select(p => p.Category).Distinct().OrderBy(c => c).ToListAsync(),
            Category = category,
            Search = search
        };
        return View(model);
    }

    public IActionResult Privacy() => View();

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() =>
        View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
