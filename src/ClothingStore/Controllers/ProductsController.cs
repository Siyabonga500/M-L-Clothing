using ClothingStore.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClothingStore.Controllers;

public class ProductsController : Controller
{
    private readonly ApplicationDbContext _db;

    public ProductsController(ApplicationDbContext db) => _db = db;

    public async Task<IActionResult> Details(int id)
    {
        var product = await _db.Products.Include(p => p.Images).Include(p => p.Sizes).FirstOrDefaultAsync(p => p.Id == id && p.IsActive);
        return product == null ? NotFound() : View(product);
    }
}
