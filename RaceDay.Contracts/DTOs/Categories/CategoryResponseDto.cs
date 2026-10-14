namespace RaceDay.Contracts.DTOs.Categories
{
    public class CategoryResponseDto
    {
        public int CategoryId { get; set; }

    public int EventId { get; set; }

        public string CategoryName { get; set; } = string.Empty;

        public decimal DistanceKm { get; set; }

        public decimal EntryFee { get; set; }

        public int MaxParticipants { get; set; }
    }

}
