using ClothingStore.Data;
using ClothingStore.Models;
using ClothingStore.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace ClothingStore.Services;

public class CartService
{
    private readonly ApplicationDbContext _db;

    public CartService(ApplicationDbContext db) => _db = db;

    public async Task<CartViewModel> GetCartAsync(string userId)
    {
        var items = await _db.CartItems
            .Include(c => c.Product).ThenInclude(p => p!.Images)
            .Include(c => c.Product).ThenInclude(p => p!.Sizes)
            .Where(c => c.UserId == userId)
            .OrderBy(c => c.AddedAt)
            .ToListAsync();
        return new CartViewModel { Items = items };
    }

    public Task<int> CountAsync(string userId) =>
        _db.CartItems.Where(c => c.UserId == userId).SumAsync(c => c.Quantity);

    public async Task<string?> AddAsync(string userId, int productId, string? size, int quantity)
    {
        var product = await _db.Products.Include(p => p.Sizes).FirstOrDefaultAsync(p => p.Id == productId && p.IsActive);
        if (product == null) return "Product not found.";
        if (quantity < 1) quantity = 1;

        var sizeStock = product.FindSize(size) ?? product.OrderedSizes.FirstOrDefault(s => s.Quantity > 0);
        if (sizeStock == null || sizeStock.Quantity <= 0)
            return $"Sorry, \"{product.Name}\" is out of stock in size {size}.";
        size = sizeStock.Size;

        var existing = await _db.CartItems.FirstOrDefaultAsync(c =>
            c.UserId == userId && c.ProductId == productId && c.Size == size);
        var newQty = (existing?.Quantity ?? 0) + quantity;
        if (newQty > sizeStock.Quantity) return $"Only {sizeStock.Quantity} of \"{product.Name}\" left in size {size}.";

        if (existing != null)
            existing.Quantity = newQty;
        else
            _db.CartItems.Add(new CartItem { UserId = userId, ProductId = productId, Size = size, Quantity = quantity });

        await _db.SaveChangesAsync();
        return null;
    }

    public async Task<string?> UpdateQuantityAsync(string userId, int cartItemId, int quantity)
    {
        var item = await _db.CartItems.Include(c => c.Product).ThenInclude(p => p!.Sizes)
            .FirstOrDefaultAsync(c => c.Id == cartItemId && c.UserId == userId);
        if (item == null) return "Item not found.";
        var available = item.Product!.FindSize(item.Size)?.Quantity ?? 0;

        if (quantity <= 0)
            _db.CartItems.Remove(item);
        else if (quantity > available)
            return $"Only {available} left in size {item.Size}.";
        else
            item.Quantity = quantity;

        await _db.SaveChangesAsync();
        return null;
    }

    public async Task RemoveAsync(string userId, int cartItemId)
    {
        var item = await _db.CartItems.FirstOrDefaultAsync(c => c.Id == cartItemId && c.UserId == userId);
        if (item != null)
        {
            _db.CartItems.Remove(item);
            await _db.SaveChangesAsync();
        }
    }
}
