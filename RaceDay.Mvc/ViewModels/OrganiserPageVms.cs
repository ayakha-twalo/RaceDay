using System.ComponentModel.DataAnnotations;
using RaceDay.Contracts.DTOs.Categories;
using RaceDay.Contracts.DTOs.Enrolments;
using RaceDay.Contracts.DTOs.Events;

namespace RaceDay.Mvc.ViewModels;

// ---------- Shared by the three pages: the "choose an event" drop-down ----------
public class EventPickerVm
{
    public List<EventResponseDto> MyEvents { get; set; } = new();
    public int? SelectedEventId { get; set; }
    public string? SelectedEventName { get; set; }
}

// ---------- Categories page ----------
public class CategoriesPageVm : EventPickerVm
{
    public List<CategoryResponseDto> Categories { get; set; } = new();
}

// Used by the Add Category and Edit Category forms.
public class CategoryFormVm
{
    public int CategoryId { get; set; }          // 0 when adding
    public int EventId { get; set; }

    [Required(ErrorMessage = "Please enter a category name.")]
    [StringLength(100)]
    [Display(Name = "Category name")]
    public string CategoryName { get; set; } = string.Empty;

    [StringLength(255)]
    public string? Description { get; set; }
}

// ---------- Enrolments page ----------
public class EnrolmentsPageVm : EventPickerVm
{
    public List<EnrolmentResponseDto> Enrolments { get; set; } = new();
}

// ---------- Results page ----------
// One row = one enrolled participant, with their result if captured.
public class ResultRowVm
{
    public int EnrolmentId { get; set; }
    public string ParticipantName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public TimeSpan? FinishTime { get; set; }
    public int? FinishPosition { get; set; }
    public bool HasResult => FinishTime.HasValue;
}

public class ResultsPageVm : EventPickerVm
{
    public List<ResultRowVm> Rows { get; set; } = new();
}

// Posted by the inline "capture result" form on the Results page.
public class CaptureResultVm
{
    public int EnrolmentId { get; set; }
    public int EventId { get; set; }

    // Typed as text (hh:mm:ss) so "45:30" cannot be misread as 45 hours.
    [Required(ErrorMessage = "Please enter a finish time.")]
    [RegularExpression(@"^\d{1,2}:[0-5]\d:[0-5]\d$",
        ErrorMessage = "Enter the finish time as hh:mm:ss, for example 00:45:30.")]
    public string FinishTime { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "The finishing position must be 1 or higher.")]
    public int FinishPosition { get; set; }
}