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

    public async Task<IActionResult> Details(int id)
    {
        var email = await _db.EmailLogs.FindAsync(id);
        return email == null ? NotFound() : View(email);
    }
}
