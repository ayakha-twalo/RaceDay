using System.ComponentModel.DataAnnotations;

namespace RaceDay.Contracts.DTOs.Profile
{
    public class UpdateProfileRequestDto
    {
        [Required]
        [StringLength(100)]
        public string FullName { get; set; } = string.Empty;

    [EmailAddress]
        [StringLength(150)]
        public string Email { get; set; } = string.Empty;

        [Phone]
        [StringLength(20)]
        public string? PhoneNumber { get; set; }
    }

}
