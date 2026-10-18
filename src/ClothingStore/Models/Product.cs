using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ClothingStore.Models;

public class Product
{
    public int Id { get; set; }

    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    [Required, Range(0.01, 100000)]
    [Column(TypeName = "decimal(18,2)")]
    public decimal Price { get; set; }

    [Required, StringLength(50)]
    public string Category { get; set; } = string.Empty;

    [Display(Name = "Available sizes"), StringLength(100)]
    public string Sizes { get; set; } = "S,M,L,XL";

    [Display(Name = "Image URL"), StringLength(500)]
    public string? ImageUrl { get; set; }

    [Range(0, 100000)]
    public int Stock { get; set; }

    [Display(Name = "Visible in shop")]
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public IEnumerable<string> SizeList =>
        Sizes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
