using System.ComponentModel.DataAnnotations;

namespace RaceDay.Contracts.DTOs.Profile
{
    /// <summary>The profile fields a user may change.</summary>
    public class UpdateProfileRequestDto
    {
        [Required]
        [StringLength(50)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string LastName { get; set; } = string.Empty;

        [Phone]
        [StringLength(20)]
        public string? PhoneNumber { get; set; }
    }
}
