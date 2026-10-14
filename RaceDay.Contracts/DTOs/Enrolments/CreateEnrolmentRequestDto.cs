using System.ComponentModel.DataAnnotations;

namespace RaceDay.Contracts.DTOs.Enrolments
{
    public class CreateEnrolmentRequestDto
    {
        [Required]
        public int EventId { get; set; }

    [Required]
        public int CategoryId { get; set; }
    }

}
