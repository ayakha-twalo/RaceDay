namespace RaceDay.Api.Models
{
    public class EnrolmentStatus
    {
        public int EnrolmentStatusID { get; set; }

        public string StatusName { get; set; } = string.Empty;

        public string? Description { get; set; }

        public ICollection<Enrolment> Enrolments { get; set; }
            = new List<Enrolment>();
    }
}