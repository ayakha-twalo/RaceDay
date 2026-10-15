namespace RaceDay.Contracts.DTOs.Enrolments
{
    /// <summary>The event and category a Participant wants to enter.</summary>
    public class CreateEnrolmentRequestDto
    {
        public int EventId { get; set; }

        public int CategoryId { get; set; }
    }
}
