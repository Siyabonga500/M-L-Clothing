using System.ComponentModel.DataAnnotations;

namespace ClothingStore.Models;

/// <summary>A customer's rating of the driver who delivered one of their orders (one per order).</summary>
public class DriverReview
{
    public int Id { get; set; }

    public int OrderId { get; set; }
    public Order? Order { get; set; }

    [Required]
    public string DriverId { get; set; } = string.Empty;
    public ApplicationUser? Driver { get; set; }

    [Required]
    public string CustomerId { get; set; } = string.Empty;
    public ApplicationUser? Customer { get; set; }

    [Range(1, 5)]
    public int Rating { get; set; }

    [StringLength(1000)]
    public string? Comment { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<ReviewReaction> Reactions { get; set; } = new();
}

/// <summary>A thumbs up (like) or thumbs down (dislike) on a review. One per user per review.</summary>
public class ReviewReaction
{
    public int Id { get; set; }

    public int ReviewId { get; set; }
    public DriverReview? Review { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    public bool IsLike { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
