namespace RaceDay.Api.Models
{
    public class EventType
    {
        public int EventTypeID { get; set; }

        public string TypeName { get; set; } = string.Empty;

        public string? Description { get; set; }

        public ICollection<Event> Events { get; set; }
            = new List<Event>();
    }
}