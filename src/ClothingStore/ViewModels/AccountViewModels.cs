using System.ComponentModel.DataAnnotations;

namespace ClothingStore.ViewModels;

public class RegisterViewModel
{
    [Required, StringLength(50), Display(Name = "Name")]
    public string FirstName { get; set; } = string.Empty;

    [Required, StringLength(50)]
    public string Surname { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Gender { get; set; } = string.Empty;

    [DataType(DataType.Date), Display(Name = "Date of birth")]
    public DateTime? DateOfBirth { get; set; }

    [Phone, Display(Name = "Phone number")]
    public string? PhoneNumber { get; set; }

    [Required, StringLength(200), Display(Name = "Home address")]
    public string HomeAddress { get; set; } = string.Empty;

    [StringLength(100)]
    public string? City { get; set; }

    [StringLength(100)]
    public string? Province { get; set; }

    [StringLength(10), Display(Name = "Postal code")]
    public string? PostalCode { get; set; }

    public double? HomeLatitude { get; set; }
    public double? HomeLongitude { get; set; }

    [Required, StringLength(100, MinimumLength = 6), DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), Display(Name = "Confirm password")]
    [Compare(nameof(Password), ErrorMessage = "The passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [MustBeTrue(ErrorMessage = "You must accept the terms to register.")]
    [Display(Name = "I accept the terms and conditions")]
    public bool AcceptTerms { get; set; }
}

/// <summary>
/// Requires a checkbox to be ticked. Checked on the server only: the built-in [Range]
/// trick breaks jQuery's client-side validation for checkboxes.
/// </summary>
public class MustBeTrueAttribute : ValidationAttribute
{
    public override bool IsValid(object? value) => value is true;
}

public class LoginViewModel
{
    [Required, Display(Name = "Email / Username")]
    public string Email { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Remember me")]
    public bool RememberMe { get; set; }
}

public class ProfileViewModel
{
    [Required, StringLength(50), Display(Name = "Name")]
    public string FirstName { get; set; } = string.Empty;

    [Required, StringLength(50)]
    public string Surname { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    [Required]
    public string Gender { get; set; } = string.Empty;

    [DataType(DataType.Date), Display(Name = "Date of birth")]
    public DateTime? DateOfBirth { get; set; }

    [Phone, Display(Name = "Phone number")]
    public string? PhoneNumber { get; set; }

    [Required, StringLength(200), Display(Name = "Home address")]
    public string HomeAddress { get; set; } = string.Empty;

    [StringLength(100)]
    public string? City { get; set; }

    [StringLength(100)]
    public string? Province { get; set; }

    [StringLength(10), Display(Name = "Postal code")]
    public string? PostalCode { get; set; }

    public double? HomeLatitude { get; set; }
    public double? HomeLongitude { get; set; }

    // Drivers only
    [StringLength(100), Display(Name = "Vehicle (make, model, colour)")]
    public string? VehicleDescription { get; set; }

    [StringLength(20), Display(Name = "Registration number")]
    public string? VehicleRegistration { get; set; }
}

public class DriverCreateViewModel : ProfileViewModel
{
    [Required, EmailAddress]
    public new string Email { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 6), DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), Display(Name = "Confirm password")]
    [Compare(nameof(Password), ErrorMessage = "The passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
