using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Models
{
    public class Event
    {
        public int EventID { get; set; }

        public string EventName { get; set; } = string.Empty;

        public string? Description { get; set; }

        public DateTime EventDate { get; set; }

        public string Location { get; set; } = string.Empty;

        public decimal Distance { get; set; }

        // Stores the public Azure URL of the event banner.
        // This stays empty until an organiser uploads an image.
        [StringLength(500)]
        public string? BannerImageUrl { get; set; }

        public int OrganiserID { get; set; }

        public int EventTypeID { get; set; }

        public User Organiser { get; set; } = null!;

        public EventType EventType { get; set; } = null!;

        public ICollection<Category> Categories { get; set; }
            = new List<Category>();

        public ICollection<Enrolment> Enrolments { get; set; }
            = new List<Enrolment>();
    }
}