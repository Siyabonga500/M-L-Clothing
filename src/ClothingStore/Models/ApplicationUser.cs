using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace ClothingStore.Models;

public class ApplicationUser : IdentityUser
{
    [Required, StringLength(50)]
    public string FirstName { get; set; } = string.Empty;

    [Required, StringLength(50)]
    public string Surname { get; set; } = string.Empty;

    [Required, StringLength(20)]
    public string Gender { get; set; } = string.Empty;

    public DateTime? DateOfBirth { get; set; }

    [Required, StringLength(200)]
    public string HomeAddress { get; set; } = string.Empty;

    [StringLength(100)]
    public string? City { get; set; }

    [StringLength(100)]
    public string? Province { get; set; }

    [StringLength(10)]
    public string? PostalCode { get; set; }

    // Home location picked on the map at registration.
    public double? HomeLatitude { get; set; }
    public double? HomeLongitude { get; set; }

    // Drivers only.
    [StringLength(100)]
    public string? VehicleDescription { get; set; }

    [StringLength(20)]
    public string? VehicleRegistration { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string FullName => $"{FirstName} {Surname}";
}
