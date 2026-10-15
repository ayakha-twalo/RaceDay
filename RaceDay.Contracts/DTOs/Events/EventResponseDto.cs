namespace RaceDay.Contracts.DTOs.Events
{
    public class EventResponseDto
    {
        public int EventId { get; set; }
        public string EventName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime EventDate { get; set; }
        public string Location { get; set; } = string.Empty;
        public decimal Distance { get; set; }
        public int EventTypeId { get; set; }
        public string EventTypeName { get; set; } = string.Empty;
        public int OrganiserId { get; set; }
    }
}
