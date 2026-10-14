using System.ComponentModel.DataAnnotations;

namespace RaceDay.Contracts.DTOs.Events
{
    public class UpdateEventRequestDto
    {
        [Required]
        [StringLength(150)]
        public string EventName { get; set; } = string.Empty;

    [Required]
        public int EventTypeId { get; set; }

        [Required]
        public DateTime EventDate { get; set; }

        [Required]
        [StringLength(150)]
        public string Location { get; set; } = string.Empty;

        public string? Description { get; set; }
    }

}
