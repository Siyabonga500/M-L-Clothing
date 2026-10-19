using System.Diagnostics;
using ClothingStore.Data;
using ClothingStore.Models;
using ClothingStore.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ClothingStore.Services;

namespace ClothingStore.Controllers;

public class HomeController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly EmailService _email;
    private readonly StoreSettings _store;
    private readonly IConfiguration _config;

    public HomeController(ApplicationDbContext db, EmailService email, IOptions<StoreSettings> store, IConfiguration config)
    {
        _db = db;
        _email = email;
        _store = store.Value;
        _config = config;
    }

    public async Task<IActionResult> Index(string? category, string? search, decimal? minPrice, decimal? maxPrice)
    {
        var query = _db.Products.Include(p => p.Images).Include(p => p.Sizes).Include(p => p.Colors).Where(p => p.IsActive);
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

    [HttpGet]
    public async Task<IActionResult> Contact()
    {
        var model = new ContactViewModel();
        if (User.Identity?.IsAuthenticated == true)
        {
            var me = await _db.Users.FirstOrDefaultAsync(u => u.UserName == User.Identity.Name);
            if (me != null) { model.Name = me.FullName; model.Email = me.Email ?? ""; model.Phone = me.PhoneNumber; }
        }
        return View(model);
    }

    // Sends the message to the store's email (or the admin) and keeps a copy under Admin -> Emails sent.
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Contact(ContactViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var to = !string.IsNullOrWhiteSpace(_store.Email) ? _store.Email : _config["AdminAccount:Email"] ?? "";
        var enc = (string? s) => System.Net.WebUtility.HtmlEncode(s ?? "");
        await _email.SendAsync(to, $"Contact form: message from {model.Name}",
            EmailService.Layout(_store.Name, "New message from the website", $"""
                <p><strong>Name:</strong> {enc(model.Name)}<br>
                   <strong>Email:</strong> <a href="mailto:{enc(model.Email)}">{enc(model.Email)}</a><br>
                   <strong>Phone:</strong> {enc(model.Phone ?? "-")}</p>
                <p style="white-space:pre-line;background:#f5f7f6;padding:12px;border-radius:8px">{enc(model.Message)}</p>
                <p style="font-size:12px;color:#6b7d79">Reply to this customer at {enc(model.Email)}.</p>
                """));

        TempData["Success"] = "Thanks for your message! We'll get back to you soon.";
        return RedirectToAction(nameof(Contact));
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() =>
        View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
