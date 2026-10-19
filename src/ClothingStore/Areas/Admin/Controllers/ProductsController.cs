using ClothingStore.Data;
using ClothingStore.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClothingStore.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Roles = Roles.Admin)]
public class ProductsController : Controller
{
    private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
    private const long MaxFileBytes = 5 * 1024 * 1024;

    private readonly ApplicationDbContext _db;
    private readonly IWebHostEnvironment _env;

    public ProductsController(ApplicationDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    public async Task<IActionResult> Index() =>
        View(await _db.Products.Include(p => p.Images).OrderBy(p => p.Category).ThenBy(p => p.Name).ToListAsync());

    [HttpGet]
    public IActionResult Create() => View("Edit", new Product());

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var product = await _db.Products.Include(p => p.Images).FirstOrDefaultAsync(p => p.Id == id);
        return product == null ? NotFound() : View(product);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(Product model, List<IFormFile>? newImages, string? imageUrl)
    {
        if (model.OriginalPrice.HasValue && model.OriginalPrice <= model.Price)
            ModelState.AddModelError(nameof(model.OriginalPrice), "The original price must be higher than the now price, or left empty.");

        Product product;
        if (model.Id == 0)
        {
            product = model;
        }
        else
        {
            product = await _db.Products.Include(p => p.Images).FirstOrDefaultAsync(p => p.Id == model.Id)
                      ?? throw new InvalidOperationException();
        }

        var files = (newImages ?? new()).Where(f => f.Length > 0).ToList();
        var newCount = files.Count + (string.IsNullOrWhiteSpace(imageUrl) ? 0 : 1);
        var existing = model.Id == 0 ? 0 : product.Images.Count;
        if (existing + newCount > Product.MaxImages)
            ModelState.AddModelError("newImages", $"A product can have at most {Product.MaxImages} pictures. It has {existing}; remove some first.");
        foreach (var f in files)
        {
            if (!AllowedExtensions.Contains(Path.GetExtension(f.FileName).ToLowerInvariant()))
                ModelState.AddModelError("newImages", $"{f.FileName}: only JPG, PNG, WEBP or GIF pictures are allowed.");
            else if (f.Length > MaxFileBytes)
                ModelState.AddModelError("newImages", $"{f.FileName}: pictures must be 5 MB or smaller.");
        }
        if (!string.IsNullOrWhiteSpace(imageUrl) && !Uri.TryCreate(imageUrl, UriKind.Absolute, out _))
            ModelState.AddModelError("imageUrl", "Enter a full picture link starting with https://");

        if (!ModelState.IsValid)
        {
            if (model.Id != 0) model.Images = product.Images;
            return View("Edit", model);
        }

        if (model.Id != 0)
        {
            product.Name = model.Name;
            product.Description = model.Description;
            product.Price = model.Price;
            product.OriginalPrice = model.OriginalPrice;
            product.Category = model.Category;
            product.Sizes = model.Sizes;
            product.Stock = model.Stock;
            product.IsActive = model.IsActive;
        }
        else
        {
            _db.Products.Add(product);
        }

        var order = product.Images.Count == 0 ? 0 : product.Images.Max(i => i.SortOrder) + 1;
        foreach (var f in files)
            product.Images.Add(new ProductImage { Url = await SaveUploadAsync(f), SortOrder = order++ });
        if (!string.IsNullOrWhiteSpace(imageUrl))
            product.Images.Add(new ProductImage { Url = imageUrl.Trim(), SortOrder = order });

        await _db.SaveChangesAsync();
        TempData["Success"] = $"\"{product.Name}\" saved.";
        return RedirectToAction(nameof(Edit), new { id = product.Id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteImage(int id)
    {
        var image = await _db.ProductImages.FindAsync(id);
        if (image == null) return NotFound();
        DeleteUploadedFile(image.Url);
        _db.ProductImages.Remove(image);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Edit), new { id = image.ProductId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MakeMainImage(int id)
    {
        var image = await _db.ProductImages.FindAsync(id);
        if (image == null) return NotFound();
        var images = await _db.ProductImages.Where(i => i.ProductId == image.ProductId).OrderBy(i => i.SortOrder).ToListAsync();
        var n = 1;
        foreach (var i in images) i.SortOrder = i.Id == id ? 0 : n++;
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Edit), new { id = image.ProductId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var product = await _db.Products.Include(p => p.Images).FirstOrDefaultAsync(p => p.Id == id);
        if (product == null) return NotFound();

        if (await _db.OrderItems.AnyAsync(i => i.ProductId == id))
        {
            // Keep order history intact: hide the product instead of deleting it.
            product.IsActive = false;
            TempData["Success"] = $"\"{product.Name}\" has orders, so it was hidden from the shop instead of deleted.";
        }
        else
        {
            foreach (var img in product.Images) DeleteUploadedFile(img.Url);
            _db.Products.Remove(product);
            TempData["Success"] = $"\"{product.Name}\" deleted.";
        }
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private async Task<string> SaveUploadAsync(IFormFile file)
    {
        var folder = Path.Combine(_env.WebRootPath, "uploads", "products");
        Directory.CreateDirectory(folder);
        var name = $"{Guid.NewGuid():N}{Path.GetExtension(file.FileName).ToLowerInvariant()}";
        await using var stream = System.IO.File.Create(Path.Combine(folder, name));
        await file.CopyToAsync(stream);
        return $"/uploads/products/{name}";
    }

    private void DeleteUploadedFile(string url)
    {
        if (!url.StartsWith("/uploads/products/")) return;
        var path = Path.Combine(_env.WebRootPath, "uploads", "products", Path.GetFileName(url));
        if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
    }
}
