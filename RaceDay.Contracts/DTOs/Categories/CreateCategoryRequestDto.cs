using System.ComponentModel.DataAnnotations;

namespace RaceDay.Contracts.DTOs.Categories
{
    /// <summary>Used to create or update a category for an event.</summary>
    public class CreateCategoryRequestDto
    {
        [Required]
        [StringLength(100)]
        public string CategoryName { get; set; } = string.Empty;

        [StringLength(255)]
        public string? Description { get; set; }
    }
}
