using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ClothingStore.Models;

// Numbers are fixed so existing databases keep their meaning.
public enum OrderStatus
{
    PendingPayment = 0,
    Paid = 1,
    Processing = 2,
    OutForDelivery = 3,
    Delivered = 4,
    Cancelled = 5,
    DriverAssigned = 6,
    ReadyForCollection = 7,
    Collected = 8
}

public enum FulfilmentMethod
{
    Delivery = 0,
    Collection = 1
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

    public FulfilmentMethod Fulfilment { get; set; } = FulfilmentMethod.Delivery;

    [Required, StringLength(100)]
    public string RecipientName { get; set; } = string.Empty;

    [Required, StringLength(300)]
    public string ShippingAddress { get; set; } = string.Empty;

    [StringLength(30)]
    public string? Phone { get; set; }

    // Delivery location picked on the map.
    public double? DeliveryLatitude { get; set; }
    public double? DeliveryLongitude { get; set; }
    public double? DistanceKm { get; set; }
    public int? EstimatedMinutes { get; set; }

    // Driver
    public string? DriverId { get; set; }
    public ApplicationUser? Driver { get; set; }
    public DateTime? DriverAssignedAt { get; set; }
    public DateTime? DeliveryWindowEnd { get; set; }
    public DateTime? DepartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    // Live driver position while out for delivery.
    public double? DriverLatitude { get; set; }
    public double? DriverLongitude { get; set; }
    public DateTime? DriverLocationUpdatedAt { get; set; }

    // Proof-of-delivery QR code. Only the customer sees the code itself.
    [StringLength(64)]
    public string? QrToken { get; set; }
    public DateTime? QrGeneratedAt { get; set; }
    public DateTime? QrExpiresAt { get; set; }
    public DateTime? QrUsedAt { get; set; }

    [StringLength(200)]
    public string? StripeSessionId { get; set; }

    [StringLength(200)]
    public string? StripePaymentIntentId { get; set; }

    public List<OrderItem> Items { get; set; } = new();
    public List<OrderEvent> Events { get; set; } = new();

    public bool IsDelivery => Fulfilment == FulfilmentMethod.Delivery;

    public bool IsPaid => Status is not (OrderStatus.PendingPayment or OrderStatus.Cancelled);

    public bool IsCompleted => Status is OrderStatus.Delivered or OrderStatus.Collected;

    public QrState QrState =>
        QrToken == null ? QrState.None
        : QrUsedAt.HasValue ? QrState.Used
        : QrExpiresAt < DateTime.UtcNow ? QrState.Expired
        : QrState.Active;
}

public enum QrState { None, Active, Used, Expired }

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

/// <summary>Timeline entry shown to the customer and admin (e.g. "Driver assigned").</summary>
public class OrderEvent
{
    public int Id { get; set; }

    public int OrderId { get; set; }
    public Order? Order { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Required, StringLength(500)]
    public string Message { get; set; } = string.Empty;
}
