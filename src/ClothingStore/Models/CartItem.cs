using System.ComponentModel.DataAnnotations;

namespace ClothingStore.Models;

public class CartItem
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    [StringLength(10)]
    public string? Size { get; set; }

    [StringLength(30)]
    public string? Color { get; set; }

    [Range(1, 100)]
    public int Quantity { get; set; } = 1;

    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}
