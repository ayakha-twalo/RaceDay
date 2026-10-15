using System.ComponentModel.DataAnnotations;

namespace RaceDay.Contracts.DTOs.Auth
{
    /// <summary>Details needed to create a RaceDay account.</summary>
    public class RegisterRequestDto
    {
        [Required]
        [StringLength(50)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string LastName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [StringLength(100)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MinLength(8)]
        public string Password { get; set; } = string.Empty;

        [Phone]
        [StringLength(20)]
        public string? PhoneNumber { get; set; }

        /// <summary>Either "Participant" or "Organiser".</summary>
        [Required]
        public string Role { get; set; } = "Participant";

        /// <summary>
        /// Invite code. Only needed when Role is "Organiser", so that
        /// not just anyone can give themselves Organiser access.
        /// </summary>
        public string? OrganiserCode { get; set; }
    }
}
