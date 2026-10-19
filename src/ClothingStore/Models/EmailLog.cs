using System.ComponentModel.DataAnnotations;

namespace ClothingStore.Models;

/// <summary>Copy of every email the system sends, so the admin can see what went out.</summary>
public class EmailLog
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Required, StringLength(256)]
    public string To { get; set; } = string.Empty;

    [Required, StringLength(300)]
    public string Subject { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public bool Sent { get; set; }

    [StringLength(1000)]
    public string? Error { get; set; }
}
