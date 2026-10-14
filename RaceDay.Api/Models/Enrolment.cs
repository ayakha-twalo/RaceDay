namespace RaceDay.Api.Models
{
    public class Enrolment
    {
        public int EnrolmentID { get; set; }

        public int ParticipantID { get; set; }

        public int EventID { get; set; }

        public int CategoryID { get; set; }

        public int EnrolmentStatusID { get; set; }

        public DateTime EnrolmentDate { get; set; }

        public User Participant { get; set; } = null!;

        public Event Event { get; set; } = null!;

        public Category Category { get; set; } = null!;

        public EnrolmentStatus EnrolmentStatus { get; set; } = null!;

        public Result? Result { get; set; }
    }
}