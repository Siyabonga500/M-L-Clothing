using System.ComponentModel.DataAnnotations;
using ClothingStore.Models;

namespace ClothingStore.ViewModels;

public class HomeViewModel
{
    public List<Product> Products { get; set; } = new();
    public List<string> Categories { get; set; } = new();
    public string? Category { get; set; }
    public string? Search { get; set; }
}

public class CartViewModel
{
    public List<CartItem> Items { get; set; } = new();
    public decimal Total => Items.Sum(i => (i.Product?.Price ?? 0) * i.Quantity);
    public int Count => Items.Sum(i => i.Quantity);
}

public class CheckoutViewModel
{
    public CartViewModel Cart { get; set; } = new();

    public FulfilmentMethod Fulfilment { get; set; } = FulfilmentMethod.Delivery;

    [Required, StringLength(100), Display(Name = "Recipient name")]
    public string RecipientName { get; set; } = string.Empty;

    [StringLength(300), Display(Name = "Delivery address")]
    public string? ShippingAddress { get; set; }

    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    [Phone, StringLength(30)]
    public string? Phone { get; set; }
}

public class AdminDashboardViewModel
{
    public int NeedsDriver { get; set; }
    public int ReadyToPrepare { get; set; }
    public int OutForDelivery { get; set; }
    public int DriverCount { get; set; }
    public int UserCount { get; set; }
    public int ProductCount { get; set; }
    public int OrderCount { get; set; }
    public int PendingOrders { get; set; }
    public decimal Revenue { get; set; }
    public List<Order> RecentOrders { get; set; } = new();
}

public class AdminUserViewModel
{
    public ApplicationUser User { get; set; } = null!;
    public bool IsAdmin { get; set; }
    public bool IsLocked { get; set; }
    public int OrderCount { get; set; }
    public bool IsDriver { get; set; }
    public int ActiveDeliveries { get; set; }
}

/// <summary>Settings for the shared map address picker partial.</summary>
public class MapPickerModel
{
    public string LatName { get; set; } = "Latitude";
    public string LngName { get; set; } = "Longitude";
    public double? Lat { get; set; }
    public double? Lng { get; set; }
    /// <summary>Ids of inputs to fill from the picked location (any may be null).</summary>
    public string? AddressId { get; set; }
    public string? CityId { get; set; }
    public string? ProvinceId { get; set; }
    public string? PostalId { get; set; }
    /// <summary>Show the store and a live distance/time estimate (checkout).</summary>
    public bool ShowEta { get; set; }
}
