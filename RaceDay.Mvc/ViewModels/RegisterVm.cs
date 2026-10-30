using System.ComponentModel.DataAnnotations;
using RaceDay.Mvc.Services;

namespace RaceDay.Mvc.ViewModels;

// Rules mirror RegisterRequestDto in the API so most mistakes
// are caught in the browser before any API call is made.
public class RegisterVm
{
    [Required, StringLength(50)]
    [Display(Name = "First name")]
    public string FirstName { get; set; } = string.Empty;

    [Required, StringLength(50)]
    [Display(Name = "Last name")]
    public string LastName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(100)]
    public string Email { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
    public string Password { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "The passwords do not match.")]
    [Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Phone, StringLength(20)]
    [Display(Name = "Phone number")]
    public string? PhoneNumber { get; set; }

    [Required]
    public string Role { get; set; } = UserRoles.Participant;

    // Only needed when registering as an Organiser.
    [Display(Name = "Organiser invite code")]
    public string? OrganiserCode { get; set; }
}