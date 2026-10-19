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
        View(await _db.Products.Include(p => p.Images).Include(p => p.Sizes).Include(p => p.Colors).OrderBy(p => p.Category).ThenBy(p => p.Name).ToListAsync());

    [HttpGet]
    public IActionResult Create() => View("Edit", new Product());

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var product = await _db.Products.Include(p => p.Images).Include(p => p.Sizes).Include(p => p.Colors).FirstOrDefaultAsync(p => p.Id == id);
        return product == null ? NotFound() : View(product);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(Product model, List<IFormFile>? newImages, string? imageUrl,
        List<string>? sizeName, List<int?>? sizeQty,
        List<string>? colorName, List<string>? colorHex, List<int?>? colorQty)
    {
        // Sizes arrive as two parallel lists: ticked standard sizes plus any "other" sizes typed in.
        var sizes = new List<ProductSize>();
        sizeName ??= new();
        sizeQty ??= new();
        for (var i = 0; i < sizeName.Count; i++)
        {
            var name = sizeName[i]?.Trim();
            var qty = i < sizeQty.Count ? sizeQty[i] : null;
            if (string.IsNullOrEmpty(name) && qty is null or 0) continue; // empty "other" row
            if (string.IsNullOrEmpty(name)) { ModelState.AddModelError("sizes", "Every size needs a name, e.g. XXL or 42."); continue; }
            if (name.Length > 20) { ModelState.AddModelError("sizes", $"Size \"{name}\" is too long (20 characters max)."); continue; }
            if (qty is null or < 0 or > 100000) { ModelState.AddModelError("sizes", $"Enter a quantity of 0 or more for size {name}."); continue; }
            if (sizes.Any(x => string.Equals(x.Size, name, StringComparison.OrdinalIgnoreCase)))
            { ModelState.AddModelError("sizes", $"Size {name} is listed twice."); continue; }
            sizes.Add(new ProductSize { Size = name, Quantity = qty.Value, SortOrder = sizes.Count });
        }
        if (sizes.Count == 0) ModelState.AddModelError("sizes", "Tick at least one size and enter its quantity.");

        // Colours (optional): name + swatch + quantity. Their total can't be more than the sizes total.
        var colors = new List<ProductColor>();
        colorName ??= new(); colorHex ??= new(); colorQty ??= new();
        for (var i = 0; i < colorName.Count; i++)
        {
            var name = colorName[i]?.Trim();
            var qty = i < colorQty.Count ? colorQty[i] : null;
            var hex = i < colorHex.Count ? colorHex[i]?.Trim() : null;
            if (string.IsNullOrEmpty(name) && qty is null or 0) continue; // empty row
            if (string.IsNullOrEmpty(name)) { ModelState.AddModelError("colors", "Every colour needs a name, e.g. Navy."); continue; }
            if (name.Length > 30) { ModelState.AddModelError("colors", $"Colour \"{name}\" is too long (30 characters max)."); continue; }
            if (qty is null or < 0 or > 100000) { ModelState.AddModelError("colors", $"Enter a quantity of 0 or more for {name}."); continue; }
            if (colors.Any(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)))
            { ModelState.AddModelError("colors", $"Colour {name} is listed twice."); continue; }
            if (hex == null || !System.Text.RegularExpressions.Regex.IsMatch(hex, "^#[0-9a-fA-F]{6}$")) hex = "#888888";
            colors.Add(new ProductColor { Name = name, Hex = hex.ToUpperInvariant(), Quantity = qty.Value, SortOrder = colors.Count });
        }
        var sizesTotal = sizes.Sum(x => x.Quantity);
        var colorsTotal = colors.Sum(c => c.Quantity);
        if (colorsTotal > sizesTotal)
            ModelState.AddModelError("colors", $"The colour quantities add up to {colorsTotal}, which is more than the total of {sizesTotal} items (from the sizes). Lower some colour quantities or add more stock to the sizes.");

        if (model.OriginalPrice.HasValue && model.OriginalPrice <= model.Price)
            ModelState.AddModelError(nameof(model.OriginalPrice), "The original price must be higher than the now price, or left empty.");

        Product product;
        if (model.Id == 0)
        {
            product = model;
        }
        else
        {
            product = await _db.Products.Include(p => p.Images).Include(p => p.Sizes).Include(p => p.Colors).FirstOrDefaultAsync(p => p.Id == model.Id)
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
            model.Sizes = sizes; // keep what the admin entered
            model.Colors = colors;
            return View("Edit", model);
        }

        if (model.Id != 0)
        {
            product.Name = model.Name;
            product.Description = model.Description;
            product.Price = model.Price;
            product.OriginalPrice = model.OriginalPrice;
            product.Category = model.Category;
            product.IsActive = model.IsActive;
        }
        else
        {
            _db.Products.Add(product);
        }

        // Update sizes in place so existing rows keep their ids; remove sizes that were unticked.
        foreach (var old in product.Sizes.ToList())
            if (!sizes.Any(x => string.Equals(x.Size, old.Size, StringComparison.OrdinalIgnoreCase)))
                product.Sizes.Remove(old);
        foreach (var size in sizes)
        {
            var existingSize = product.FindSize(size.Size);
            if (existingSize == null) product.Sizes.Add(size);
            else { existingSize.Size = size.Size; existingSize.Quantity = size.Quantity; existingSize.SortOrder = size.SortOrder; }
        }
        product.RecalculateStock();

        foreach (var old in product.Colors.ToList())
            if (!colors.Any(c => string.Equals(c.Name, old.Name, StringComparison.OrdinalIgnoreCase)))
                product.Colors.Remove(old);
        foreach (var c in colors)
        {
            var existingColor = product.FindColor(c.Name);
            if (existingColor == null) product.Colors.Add(c);
            else { existingColor.Name = c.Name; existingColor.Hex = c.Hex; existingColor.Quantity = c.Quantity; existingColor.SortOrder = c.SortOrder; }
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
