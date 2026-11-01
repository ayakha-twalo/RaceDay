using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Models
{
    public class User
    {
        public int UserID { get; set; }

        public string FirstName { get; set; } = string.Empty;

        public string LastName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string PasswordHash { get; set; } = string.Empty;

        public string? PhoneNumber { get; set; }

        public string Role { get; set; } = string.Empty;

        // Stores the public Azure URL of the user's profile picture
        // This stays empty until the user uploads a picture
        [StringLength(500)]
        public string? ProfileImageUrl { get; set; }

        public ICollection<Event> OrganisedEvents { get; set; }
            = new List<Event>();

        public ICollection<Enrolment> Enrolments { get; set; }
            = new List<Enrolment>();
    }
}