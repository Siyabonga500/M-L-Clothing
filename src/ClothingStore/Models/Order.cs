using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ClothingStore.Models;

public enum OrderStatus
{
    PendingPayment,
    Paid,
    Processing,
    Shipped,
    Delivered,
    Cancelled
}

public class Order
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PaidAt { get; set; }

    public OrderStatus Status { get; set; } = OrderStatus.PendingPayment;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Total { get; set; }

    [Required, StringLength(100)]
    public string RecipientName { get; set; } = string.Empty;

    [Required, StringLength(300)]
    public string ShippingAddress { get; set; } = string.Empty;

    [StringLength(30)]
    public string? Phone { get; set; }

    [StringLength(200)]
    public string? StripeSessionId { get; set; }

    [StringLength(200)]
    public string? StripePaymentIntentId { get; set; }

    public List<OrderItem> Items { get; set; } = new();
}

public class OrderItem
{
    public int Id { get; set; }

    public int OrderId { get; set; }
    public Order? Order { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    // Copied at checkout so the order keeps its history if the product changes later.
    [Required, StringLength(100)]
    public string ProductName { get; set; } = string.Empty;

    [StringLength(10)]
    public string? Size { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal UnitPrice { get; set; }

    public int Quantity { get; set; }

    public decimal LineTotal => UnitPrice * Quantity;
}
