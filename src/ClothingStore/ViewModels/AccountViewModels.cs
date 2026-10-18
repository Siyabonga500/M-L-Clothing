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

    [Required, StringLength(100, MinimumLength = 6), DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), Display(Name = "Confirm password")]
    [Compare(nameof(Password), ErrorMessage = "The passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Range(typeof(bool), "true", "true", ErrorMessage = "You must accept the terms to register.")]
    [Display(Name = "I accept the terms and conditions")]
    public bool AcceptTerms { get; set; }
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
}
