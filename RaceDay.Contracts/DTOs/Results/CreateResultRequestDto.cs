using System.ComponentModel.DataAnnotations;

namespace RaceDay.Contracts.DTOs.Results
{
    /// <summary>A Participant's finish time and finishing position.</summary>
    public class CreateResultRequestDto
    {
        /// <summary>Finish time as hh:mm:ss, for example 00:45:30.</summary>
        public TimeSpan FinishTime { get; set; }

        /// <summary>Finishing position, 1 or higher.</summary>
        [Range(1, int.MaxValue)]
        public int FinishPosition { get; set; }
    }
}
