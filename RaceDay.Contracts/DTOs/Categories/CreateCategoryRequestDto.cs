using System.ComponentModel.DataAnnotations;

namespace RaceDay.Contracts.DTOs.Categories
{
    public class CreateCategoryRequestDto
    {
        [Required]
        public int EventId { get; set; }

    [Required]
        [StringLength(100)]
        public string CategoryName { get; set; } = string.Empty;

        [Range(0.01, 1000)]
        public decimal DistanceKm { get; set; }

        [Range(0, 100000)]
        public decimal EntryFee { get; set; }

        [Range(1, 100000)]
        public int MaxParticipants { get; set; }
    }

}
