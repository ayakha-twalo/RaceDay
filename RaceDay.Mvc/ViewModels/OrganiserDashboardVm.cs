namespace RaceDay.Mvc.ViewModels;

// Everything the Organiser dashboard page displays.
public class OrganiserDashboardVm
{
    public List<OrganiserEventSummaryVm> Events { get; set; } = new();

    public int TotalEvents => Events.Count;
    public int UpcomingEvents => Events.Count(e => e.IsUpcoming);
    public int TotalEnrolments => Events.Sum(e => e.EnrolmentCount);
}

// One row of the dashboard table.
public class OrganiserEventSummaryVm
{
    public int EventId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public string Location { get; set; } = string.Empty;
    public string EventTypeName { get; set; } = string.Empty;
    public int EnrolmentCount { get; set; }

    // An event is "upcoming" if its date is today or later.
    public bool IsUpcoming => EventDate.Date >= DateTime.Today;
}