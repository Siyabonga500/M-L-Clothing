using ClothingStore.Data;
using ClothingStore.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClothingStore.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Roles = Roles.Admin)]
public class ProductsController : Controller
{
    private readonly ApplicationDbContext _db;

    public ProductsController(ApplicationDbContext db) => _db = db;

    public async Task<IActionResult> Index() =>
        View(await _db.Products.OrderBy(p => p.Category).ThenBy(p => p.Name).ToListAsync());

    [HttpGet]
    public IActionResult Create() => View("Edit", new Product());

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var product = await _db.Products.FindAsync(id);
        return product == null ? NotFound() : View(product);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(Product model)
    {
        if (!ModelState.IsValid) return View("Edit", model);

        if (model.Id == 0)
        {
            _db.Products.Add(model);
        }
        else
        {
            var product = await _db.Products.FindAsync(model.Id);
            if (product == null) return NotFound();
            product.Name = model.Name;
            product.Description = model.Description;
            product.Price = model.Price;
            product.Category = model.Category;
            product.Sizes = model.Sizes;
            product.ImageUrl = model.ImageUrl;
            product.Stock = model.Stock;
            product.IsActive = model.IsActive;
        }
        await _db.SaveChangesAsync();
        TempData["Success"] = $"\"{model.Name}\" saved.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var product = await _db.Products.FindAsync(id);
        if (product == null) return NotFound();

        if (await _db.OrderItems.AnyAsync(i => i.ProductId == id))
        {
            // Keep order history intact: hide the product instead of deleting it.
            product.IsActive = false;
            TempData["Success"] = $"\"{product.Name}\" has orders, so it was hidden from the shop instead of deleted.";
        }
        else
        {
            _db.Products.Remove(product);
            TempData["Success"] = $"\"{product.Name}\" deleted.";
        }
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }
}
