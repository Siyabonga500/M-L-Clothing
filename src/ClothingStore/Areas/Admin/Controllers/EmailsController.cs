using ClothingStore.Data;
using ClothingStore.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClothingStore.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Roles = Roles.Admin)]
public class EmailsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly EmailService _email;

    public EmailsController(ApplicationDbContext db, EmailService email)
    {
        _db = db;
        _email = email;
    }

    public async Task<IActionResult> Index()
    {
        ViewData["Configured"] = _email.IsConfigured;
        return View(await _db.EmailLogs.OrderByDescending(e => e.CreatedAt).Take(200).ToListAsync());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SendTest(string to)
    {
        if (string.IsNullOrWhiteSpace(to)) to = User.Identity?.Name ?? "";
        await _email.SendAsync(to, "Test email from your store",
            EmailService.Layout("Email test", "It works!", "<p>If you can read this, your store's email settings are correct.</p>"));
        var log = await _db.EmailLogs.OrderByDescending(e => e.Id).FirstAsync();
        TempData[log.Sent ? "Success" : "Error"] = log.Sent
            ? $"Test email sent to {to}. Check the inbox (and spam folder)."
            : $"Test email failed: {log.Error}";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Details(int id)
    {
        var email = await _db.EmailLogs.FindAsync(id);
        return email == null ? NotFound() : View(email);
    }
}
