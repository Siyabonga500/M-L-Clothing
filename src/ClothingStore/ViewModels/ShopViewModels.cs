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

    [Required, StringLength(100), Display(Name = "Recipient name")]
    public string RecipientName { get; set; } = string.Empty;

    [Required, StringLength(300), Display(Name = "Delivery address")]
    public string ShippingAddress { get; set; } = string.Empty;

    [Phone, StringLength(30)]
    public string? Phone { get; set; }
}

public class AdminDashboardViewModel
{
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
}
