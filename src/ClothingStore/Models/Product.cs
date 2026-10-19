using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ClothingStore.Models;

public class Product
{
    public const int MaxImages = 4;

    public int Id { get; set; }

    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    /// <summary>The price the customer pays now (the sale price when on sale).</summary>
    [Required, Range(0.01, 100000), Display(Name = "Now price")]
    [Column(TypeName = "decimal(18,2)")]
    public decimal Price { get; set; }

    /// <summary>Price before the sale. Leave empty when the item isn't on sale.</summary>
    [Range(0.01, 100000), Display(Name = "Original price")]
    [Column(TypeName = "decimal(18,2)")]
    public decimal? OriginalPrice { get; set; }

    [Required, StringLength(50)]
    public string Category { get; set; } = string.Empty;

    [Display(Name = "Available sizes"), StringLength(100)]
    public string Sizes { get; set; } = "S,M,L,XL";

    [Range(0, 100000), Display(Name = "Available quantity")]
    public int Stock { get; set; }

    [Display(Name = "Visible in shop")]
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<ProductImage> Images { get; set; } = new();

    public IEnumerable<string> SizeList =>
        Sizes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public string? MainImageUrl => Images.OrderBy(i => i.SortOrder).FirstOrDefault()?.Url;

    public bool IsOnSale => OriginalPrice.HasValue && OriginalPrice.Value > Price;

    public decimal SavingAmount => IsOnSale ? OriginalPrice!.Value - Price : 0;

    public int SavingPercent => IsOnSale ? (int)Math.Round(SavingAmount / OriginalPrice!.Value * 100m) : 0;
}

public class ProductImage
{
    public int Id { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    [Required, StringLength(500)]
    public string Url { get; set; } = string.Empty;

    public int SortOrder { get; set; }
}
