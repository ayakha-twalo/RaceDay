using System.ComponentModel.DataAnnotations;

namespace RaceDay.Contracts.DTOs.Enrolments
{
    /// <summary>New status for an enrolment: Pending, Confirmed or Cancelled.</summary>
    public class UpdateEnrolmentStatusRequestDto
    {
        [Required]
        public string StatusName { get; set; } = string.Empty;
    }
}
