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

    /// <summary>Total of all size quantities. Kept in sync whenever sizes change or stock is sold.</summary>
    [Display(Name = "Total quantity")]
    public int Stock { get; set; }

    [Display(Name = "Visible in shop")]
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<ProductImage> Images { get; set; } = new();

    /// <summary>Quantity in stock for each size (e.g. S = 5, M = 3, 32 = 2).</summary>
    public List<ProductSize> Sizes { get; set; } = new();

    [NotMapped]
    public IEnumerable<ProductSize> OrderedSizes => Sizes.OrderBy(s => s.SortOrder).ThenBy(s => s.Id);

    public ProductSize? FindSize(string? size) =>
        Sizes.FirstOrDefault(s => string.Equals(s.Size, size, StringComparison.OrdinalIgnoreCase));

    public void RecalculateStock() => Stock = Sizes.Sum(s => s.Quantity);

    /// <summary>Optional colours with their own quantities. Their total may not exceed <see cref="Stock"/>.</summary>
    public List<ProductColor> Colors { get; set; } = new();

    [NotMapped]
    public IEnumerable<ProductColor> OrderedColors => Colors.OrderBy(c => c.SortOrder).ThenBy(c => c.Id);

    public bool HasColors => Colors.Count > 0;

    public ProductColor? FindColor(string? color) =>
        Colors.FirstOrDefault(c => string.Equals(c.Name, color, StringComparison.OrdinalIgnoreCase));

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

public class ProductSize
{
    public int Id { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    /// <summary>Size label: letters (S, XL) or numbers (32, 9).</summary>
    [Required, StringLength(20)]
    public string Size { get; set; } = string.Empty;

    [Range(0, 100000)]
    public int Quantity { get; set; }

    public int SortOrder { get; set; }
}

public class ProductColor
{
    public int Id { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    [Required, StringLength(30)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Swatch colour shown to customers, e.g. #1F3A5F.</summary>
    [StringLength(7)]
    public string Hex { get; set; } = "#888888";

    [Range(0, 100000)]
    public int Quantity { get; set; }

    public int SortOrder { get; set; }
}
