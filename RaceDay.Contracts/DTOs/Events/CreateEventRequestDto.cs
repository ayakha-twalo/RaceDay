using System.ComponentModel.DataAnnotations;

namespace RaceDay.Contracts.DTOs.Events
{
    /// <summary>Details needed to create an event.</summary>
    public class CreateEventRequestDto
    {
        [Required]
        [StringLength(100)]
        public string EventName { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        [Required]
        public DateTime EventDate { get; set; }

        [Required]
        [StringLength(150)]
        public string Location { get; set; } = string.Empty;

        /// <summary>Distance in kilometres.</summary>
        [Range(0.01, 9999.99)]
        public decimal Distance { get; set; }

        /// <summary>1 = Run, 2 = Walk, 3 = Cycle.</summary>
        public int EventTypeId { get; set; }
    }
}
