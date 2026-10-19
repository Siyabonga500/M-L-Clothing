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
            .Include(c => c.Product).ThenInclude(p => p!.Colors)
            .Where(c => c.UserId == userId)
            .OrderBy(c => c.AddedAt)
            .ToListAsync();
        return new CartViewModel { Items = items };
    }

    public Task<int> CountAsync(string userId) =>
        _db.CartItems.Where(c => c.UserId == userId).SumAsync(c => c.Quantity);

    public async Task<string?> AddAsync(string userId, int productId, string? size, int quantity, string? color = null)
    {
        var product = await _db.Products.Include(p => p.Sizes).Include(p => p.Colors)
            .FirstOrDefaultAsync(p => p.Id == productId && p.IsActive);
        if (product == null) return "Product not found.";
        if (quantity < 1) quantity = 1;

        var sizeStock = product.FindSize(size) ?? product.OrderedSizes.FirstOrDefault(s => s.Quantity > 0);
        if (sizeStock == null || sizeStock.Quantity <= 0)
            return $"Sorry, \"{product.Name}\" is out of stock in size {size}.";
        size = sizeStock.Size;

        if (product.HasColors)
        {
            var colorStock = product.FindColor(color) ?? product.OrderedColors.FirstOrDefault(c => c.Quantity > 0);
            if (colorStock == null || colorStock.Quantity <= 0)
                return $"Sorry, \"{product.Name}\" is out of stock in {color ?? "that colour"}.";
            color = colorStock.Name;
        }
        else color = null;

        var existing = await _db.CartItems.FirstOrDefaultAsync(c =>
            c.UserId == userId && c.ProductId == productId && c.Size == size && c.Color == color);
        var error = await CheckStockAsync(userId, product, size, color, existing?.Id, (existing?.Quantity ?? 0) + quantity);
        if (error != null) return error;

        if (existing != null)
            existing.Quantity += quantity;
        else
            _db.CartItems.Add(new CartItem { UserId = userId, ProductId = productId, Size = size, Color = color, Quantity = quantity });

        await _db.SaveChangesAsync();
        return null;
    }

    public async Task<string?> UpdateQuantityAsync(string userId, int cartItemId, int quantity)
    {
        var item = await _db.CartItems.Include(c => c.Product).ThenInclude(p => p!.Sizes)
            .Include(c => c.Product).ThenInclude(p => p!.Colors)
            .FirstOrDefaultAsync(c => c.Id == cartItemId && c.UserId == userId);
        if (item == null) return "Item not found.";

        if (quantity <= 0)
        {
            _db.CartItems.Remove(item);
        }
        else
        {
            // Check the new quantity against stock, ignoring this line's current quantity.
            var error = await CheckStockAsync(userId, item.Product!, item.Size, item.Color, item.Id, quantity);
            if (error != null) return error;
            item.Quantity = quantity;
        }

        await _db.SaveChangesAsync();
        return null;
    }

    /// <summary>
    /// The same size can be in the cart in several colours (and the same colour in several sizes),
    /// so stock is checked against everything in the cart for that size and that colour.
    /// <paramref name="lineId"/> is the cart line being changed (null for a new line) and
    /// <paramref name="lineQty"/> the quantity it will have.
    /// </summary>
    private async Task<string?> CheckStockAsync(string userId, Product product, string? size, string? color, int? lineId, int lineQty)
    {
        var others = await _db.CartItems
            .Where(c => c.UserId == userId && c.ProductId == product.Id && c.Id != lineId).ToListAsync();

        string? Check(string what, int left, int inOtherLines) =>
            inOtherLines + lineQty <= left ? null
            : $"Only {left} of \"{product.Name}\" left in {what}" +
              (inOtherLines > 0 ? $" and you already have {inOtherLines} in your cart." : ".");

        return Check($"size {size}", product.FindSize(size)?.Quantity ?? 0, others.Where(c => c.Size == size).Sum(c => c.Quantity))
            ?? (color == null ? null
                : Check(color, product.FindColor(color)?.Quantity ?? 0, others.Where(c => c.Color == color).Sum(c => c.Quantity)));
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
