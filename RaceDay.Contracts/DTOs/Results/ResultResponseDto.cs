namespace RaceDay.Contracts.DTOs.Results
{
    // What the API returns for a race result.
    // EventId, EventDate and CategoryName were added in Part 3 so the
    // Participant's "My Results" page can show everything it needs
    // without the MVC project touching the database.
    public class ResultResponseDto
    {
        public int ResultId { get; set; }
        public int EnrolmentId { get; set; }
        public int EventId { get; set; }
        public string EventName { get; set; } = string.Empty;
        public DateTime EventDate { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public string ParticipantName { get; set; } = string.Empty;
        public TimeSpan FinishTime { get; set; }
        public int FinishPosition { get; set; }
    }
}