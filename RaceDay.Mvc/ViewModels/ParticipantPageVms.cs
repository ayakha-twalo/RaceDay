using System.ComponentModel.DataAnnotations;
using RaceDay.Contracts.DTOs.Categories;
using RaceDay.Contracts.DTOs.Enrolments;
using RaceDay.Contracts.DTOs.Events;

namespace RaceDay.Mvc.ViewModels;

// ---------- Upcoming events page (with filters) ----------
public class EventBrowseVm
{
    public List<EventResponseDto> Events { get; set; } = new();

    // The filters the Participant chose (kept so the form stays filled in).
    public string? Type { get; set; }
    public string? Search { get; set; }
}

// ---------- Event details page ----------
public class EventDetailsVm
{
    public EventResponseDto Event { get; set; } = new();
    public List<CategoryResponseDto> Categories { get; set; } = new();

    // Filled in when this Participant has already entered the event.
    public EnrolmentResponseDto? MyEnrolment { get; set; }

    // Entries close once the event date has passed.
    public bool EntriesOpen => Event.EventDate > DateTime.Now;
}

// ---------- My Events page ----------
// One row = one enrolment, combined with details of its event.
public class MyEventRowVm
{
    public int EnrolmentId { get; set; }
    public int EventId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public DateTime? EventDate { get; set; }
    public string Location { get; set; } = string.Empty;
    public decimal? Distance { get; set; }
    public string EventTypeName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

// ---------- Profile page ----------
public class ProfileFormVm
{
    [Required(ErrorMessage = "Please enter your first name.")]
    [StringLength(50)]
    [Display(Name = "First name")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter your last name.")]
    [StringLength(50)]
    [Display(Name = "Last name")]
    public string LastName { get; set; } = string.Empty;

    [Phone, StringLength(20)]
    [Display(Name = "Phone number")]
    public string? PhoneNumber { get; set; }

    // Display only: shown in read-only boxes and ignored by the API.
    public string? Email { get; set; }
    public string? Role { get; set; }
}