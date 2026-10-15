namespace RaceDay.Contracts.DTOs.Results
{
    public class ResultResponseDto
    {
        public int ResultId { get; set; }
        public int EnrolmentId { get; set; }
        public string EventName { get; set; } = string.Empty;
        public string ParticipantName { get; set; } = string.Empty;
        public TimeSpan FinishTime { get; set; }
        public int FinishPosition { get; set; }
    }
}
