namespace RaceDay.Contracts.DTOs.Enrolments
{
    public class EnrolmentResponseDto
    {
        public int EnrolmentId { get; set; }

    public int UserId { get; set; }

        public int EventId { get; set; }

        public int CategoryId { get; set; }

        public DateTime EnrolmentDate { get; set; }

        public string Status { get; set; } = string.Empty;
    }

}
