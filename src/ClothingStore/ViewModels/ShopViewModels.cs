using System.ComponentModel.DataAnnotations;
using ClothingStore.Models;

namespace ClothingStore.ViewModels;

public class HomeViewModel
{
    public List<Product> Products { get; set; } = new();
    public List<string> Categories { get; set; } = new();
    public string? Category { get; set; }
    public string? Search { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public bool HasFilters => !string.IsNullOrWhiteSpace(Search) || MinPrice.HasValue || MaxPrice.HasValue || !string.IsNullOrEmpty(Category);
}

public class OrderHistoryFilter
{
    /// <summary>"newest" (default) or "oldest".</summary>
    public string Sort { get; set; } = "newest";

    [DataType(DataType.Date)]
    public DateTime? From { get; set; }

    [DataType(DataType.Date)]
    public DateTime? To { get; set; }

    /// <summary>Only orders in this status (e.g. Delivered, Out for delivery). Null = all.</summary>
    public OrderStatus? Status { get; set; }

    public bool HasDates => From.HasValue || To.HasValue;
    public bool IsFiltered => HasDates || Status.HasValue || Sort == "oldest";

    /// <summary>Filters by status and order date (From/To are whole days, local time) and sorts.</summary>
    public IEnumerable<Order> Apply(IEnumerable<Order> orders)
    {
        if (Status.HasValue) orders = orders.Where(o => o.Status == Status.Value);
        if (From.HasValue) orders = orders.Where(o => o.CreatedAt.ToLocalTime().Date >= From.Value.Date);
        if (To.HasValue) orders = orders.Where(o => o.CreatedAt.ToLocalTime().Date <= To.Value.Date);
        return Sort == "oldest" ? orders.OrderBy(o => o.CreatedAt) : orders.OrderByDescending(o => o.CreatedAt);
    }
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
    public List<ProductSize> LowStock { get; set; } = new();
}

public class AdminUserViewModel
{
    public ApplicationUser User { get; set; } = null!;
    public bool IsAdmin { get; set; }
    public bool IsLocked { get; set; }
    public int OrderCount { get; set; }
    public bool IsDriver { get; set; }
    public int ActiveDeliveries { get; set; }
    public double Rating { get; set; }
    public int ReviewCount { get; set; }
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

public class DriverRatingSummary
{
    public ApplicationUser Driver { get; set; } = null!;
    public double Average { get; set; }
    public int Count { get; set; }
    public int Delivered { get; set; }
}

public class ReviewItem
{
    public DriverReview Review { get; set; } = null!;
    public int Likes { get; set; }
    public int Dislikes { get; set; }
    /// <summary>The viewer's own reaction: true = like, false = dislike, null = none.</summary>
    public bool? Mine { get; set; }
    public bool CanReact { get; set; }

    /// <summary>"Thandi M." – first name and surname initial, so customers aren't fully named in public.</summary>
    public string CustomerName =>
        Review.Customer == null ? "Customer"
        : $"{Review.Customer.FirstName} {(string.IsNullOrEmpty(Review.Customer.Surname) ? "" : Review.Customer.Surname[..1] + ".")}".Trim();
}

public class ReviewsPageViewModel
{
    public List<DriverRatingSummary> Drivers { get; set; } = new();
    public List<ReviewItem> Reviews { get; set; } = new();
    public string? DriverId { get; set; }
    public string Sort { get; set; } = "newest";
    public DriverRatingSummary? Selected => Drivers.FirstOrDefault(d => d.Driver.Id == DriverId);
}

public class ContactViewModel
{
    [Required, StringLength(100), Display(Name = "Your name")]
    public string Name { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(256), Display(Name = "Your email")]
    public string Email { get; set; } = string.Empty;

    [Phone, StringLength(30)]
    public string? Phone { get; set; }

    [Required, StringLength(2000, MinimumLength = 5)]
    public string Message { get; set; } = string.Empty;
}
