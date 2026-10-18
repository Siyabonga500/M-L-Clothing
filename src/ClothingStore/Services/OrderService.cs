using ClothingStore.Data;
using ClothingStore.Models;
using Microsoft.EntityFrameworkCore;

namespace ClothingStore.Services;

public class OrderService
{
    private readonly ApplicationDbContext _db;

    public OrderService(ApplicationDbContext db) => _db = db;

    /// <summary>
    /// Marks an order as paid, reduces stock and empties the buyer's cart.
    /// Safe to call more than once (from the success page and the webhook).
    /// </summary>
    public async Task<Order?> MarkPaidAsync(string stripeSessionId, string? paymentIntentId)
    {
        var order = await _db.Orders.Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.StripeSessionId == stripeSessionId);
        if (order == null || order.Status != OrderStatus.PendingPayment) return order;

        order.Status = OrderStatus.Paid;
        order.PaidAt = DateTime.UtcNow;
        order.StripePaymentIntentId = paymentIntentId;

        var productIds = order.Items.Select(i => i.ProductId).ToList();
        var products = await _db.Products.Where(p => productIds.Contains(p.Id)).ToListAsync();
        foreach (var item in order.Items)
        {
            var product = products.First(p => p.Id == item.ProductId);
            product.Stock = Math.Max(0, product.Stock - item.Quantity);
        }

        var cart = _db.CartItems.Where(c => c.UserId == order.UserId);
        _db.CartItems.RemoveRange(cart);

        await _db.SaveChangesAsync();
        return order;
    }
}
