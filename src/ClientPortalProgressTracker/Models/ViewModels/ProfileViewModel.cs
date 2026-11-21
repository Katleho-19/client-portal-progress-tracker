using System.ComponentModel.DataAnnotations;

namespace ClientPortalProgressTracker.Models.ViewModels;

public class BusinessProfileViewModel
{
    [Required(ErrorMessage = "Business name is required.")]
    [Display(Name = "Business name")]
    public string BusinessName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Full name is required.")]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    public string? LogoUrl { get; set; }

    [Display(Name = "Logo image")]
    public IFormFile? LogoFile { get; set; }

    [Display(Name = "Contact phone")]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Contact email is required.")]
    [EmailAddress]
    [Display(Name = "Contact email")]
    public string Email { get; set; } = string.Empty;

    [Display(Name = "Bank name")]
    public string? BankName { get; set; }

    [Display(Name = "Account number")]
    public string? AccountNumber { get; set; }

    [Display(Name = "Branch code")]
    public string? BranchCode { get; set; }
}

public class ChangePasswordViewModel
{
    [Required(ErrorMessage = "Current password is required.")]
    [DataType(DataType.Password)]
    [Display(Name = "Current password")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "New password is required.")]
    [MinLength(8, ErrorMessage = "Min. 8 characters")]
    [DataType(DataType.Password)]
    [Display(Name = "New password")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please confirm your new password.")]
    [Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match.")]
    [DataType(DataType.Password)]
    [Display(Name = "Confirm new password")]
    public string ConfirmNewPassword { get; set; } = string.Empty;
}
