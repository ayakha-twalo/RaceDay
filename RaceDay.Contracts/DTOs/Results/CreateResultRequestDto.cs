using System.ComponentModel.DataAnnotations;

namespace RaceDay.Contracts.DTOs.Results
{
    public class CreateResultRequestDto
    {
        [Required]
        public int EnrolmentId { get; set; }

    public TimeSpan? FinishTime { get; set; }

        [Range(1, int.MaxValue)]
        public int? Position { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }
    }

}
