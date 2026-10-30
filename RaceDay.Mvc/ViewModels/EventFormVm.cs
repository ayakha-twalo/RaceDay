using System.ComponentModel.DataAnnotations;

namespace RaceDay.Mvc.ViewModels;

// The data behind the Create Event and Edit Event forms.
// Rules mirror CreateEventRequestDto in the API.
public class EventFormVm
{
    // 0 when creating a new event; the real id when editing.
    public int EventId { get; set; }

    [Required, StringLength(100)]
    [Display(Name = "Event name")]
    public string EventName { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    [Required]
    [DataType(DataType.Date)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
    [Display(Name = "Event date")]
    public DateTime EventDate { get; set; }

    [Required, StringLength(150)]
    public string Location { get; set; } = string.Empty;

    [Range(0.01, 9999.99, ErrorMessage = "Distance must be between 0.01 and 9999.99 km.")]
    [Display(Name = "Distance (km)")]
    public decimal Distance { get; set; }

    // 1 = Run, 2 = Walk, 3 = Cycle (as set up in your API).
    [Range(1, 3, ErrorMessage = "Please choose an event type.")]
    [Display(Name = "Event type")]
    public int EventTypeId { get; set; } = 1;

    // The shared form uses this to decide which action to post to.
    public bool IsEdit => EventId > 0;
}