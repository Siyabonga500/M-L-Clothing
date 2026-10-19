using ClothingStore.Data;
using ClothingStore.Models;
using Microsoft.EntityFrameworkCore;

namespace ClothingStore.Services;

public class OrderService
{
    private readonly ApplicationDbContext _db;
    private readonly ReceiptService _receipts;
    private readonly IHttpContextAccessor _http;

    public OrderService(ApplicationDbContext db, ReceiptService receipts, IHttpContextAccessor http)
    {
        _db = db;
        _receipts = receipts;
        _http = http;
    }

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
        var products = await _db.Products.Include(p => p.Sizes).Where(p => productIds.Contains(p.Id)).ToListAsync();
        foreach (var item in order.Items)
        {
            // Take the stock from the size that was bought, then update the product total.
            var product = products.First(p => p.Id == item.ProductId);
            var size = product.FindSize(item.Size);
            if (size != null) size.Quantity = Math.Max(0, size.Quantity - item.Quantity);
            product.RecalculateStock();
        }

        _db.OrderEvents.Add(new OrderEvent { OrderId = order.Id, Message = order.IsDelivery
            ? "Payment received. We'll assign a driver shortly."
            : "Payment received. We'll let you know when your order is ready to collect." });

        var cart = _db.CartItems.Where(c => c.UserId == order.UserId);
        _db.CartItems.RemoveRange(cart);

        await _db.SaveChangesAsync();

        // Email the customer their proof of payment (PDF attached). Runs once, when the order first becomes paid.
        var paid = await _receipts.LoadAsync(order.Id);
        var request = _http.HttpContext?.Request;
        await _receipts.SendAsync(paid!, request == null ? null : $"{request.Scheme}://{request.Host}");
        return order;
    }
}
