using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ResultsController : ControllerBase
    {
        private readonly RaceDayDbContext _context;

        public ResultsController(RaceDayDbContext context)
        {
            _context = context;
        }

        // Participants can view their own race results.
        [HttpGet("my")]
        public async Task<IActionResult> GetMyResults()
        {
            int? userId = HttpContext.Session.GetInt32("UserId");
            string? role = HttpContext.Session.GetString("Role");

            if (userId == null || role == null)
                return Unauthorized(new { message = "Please log in first." });

            if (role != "Participant")
                return Forbid();

            var results = await _context.Results
                .Where(r => r.Enrolment.ParticipantID == userId.Value)
                .Select(r => new
                {
                    r.ResultID,
                    r.EnrolmentID,
                    EventName = r.Enrolment.Event.EventName,
                    r.FinishTime,
                    r.FinishPosition
                })
                .ToListAsync();

            return Ok(results);
        }

        // Organisers can record results for participants enrolled in their events.
        [HttpPost]
        public async Task<IActionResult> RecordResult(
            [FromBody] RecordResultRequest request)
        {
            int? userId = HttpContext.Session.GetInt32("UserId");
            string? role = HttpContext.Session.GetString("Role");

            if (userId == null || role == null)
                return Unauthorized(new { message = "Please log in first." });

            if (role != "Organiser")
                return Forbid();

            if (request.FinishTime <= TimeSpan.Zero ||
                request.FinishPosition <= 0)
            {
                return BadRequest(new
                {
                    message = "Finish time and finish position must be greater than zero."
                });
            }

            var enrolment = await _context.Enrolments
                .Include(e => e.Event)
                .FirstOrDefaultAsync(e => e.EnrolmentID == request.EnrolmentID);

            if (enrolment == null)
                return NotFound(new { message = "Enrolment not found." });

            if (enrolment.Event.OrganiserID != userId.Value)
                return Forbid();

            bool resultExists = await _context.Results
                .AnyAsync(r => r.EnrolmentID == request.EnrolmentID);

            if (resultExists)
            {
                return Conflict(new
                {
                    message = "A result has already been recorded for this enrolment."
                });
            }

            var result = new Result
            {
                EnrolmentID = request.EnrolmentID,
                FinishTime = request.FinishTime,
                FinishPosition = request.FinishPosition
            };

            _context.Results.Add(result);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Result recorded successfully.",
                result.ResultID,
                result.EnrolmentID,
                result.FinishTime,
                result.FinishPosition
            });
        }

        // Organisers can view results for their own events.
        [HttpGet("event/{eventId:int}")]
        public async Task<IActionResult> GetEventResults(int eventId)
        {
            int? userId = HttpContext.Session.GetInt32("UserId");
            string? role = HttpContext.Session.GetString("Role");

            if (userId == null || role == null)
                return Unauthorized(new { message = "Please log in first." });

            if (role != "Organiser")
                return Forbid();

            var eventItem = await _context.Events
                .FirstOrDefaultAsync(e => e.EventID == eventId);

            if (eventItem == null)
                return NotFound(new { message = "Event not found." });

            if (eventItem.OrganiserID != userId.Value)
                return Forbid();

            var results = await _context.Results
                .Where(r => r.Enrolment.EventID == eventId)
                .Select(r => new
                {
                    r.ResultID,
                    r.EnrolmentID,
                    ParticipantName = r.Enrolment.Participant.FirstName + " " +
                                      r.Enrolment.Participant.LastName,
                    r.FinishTime,
                    r.FinishPosition
                })
                .OrderBy(r => r.FinishPosition)
                .ToListAsync();

            return Ok(results);
        }
    }

    public class RecordResultRequest
    {
        public int EnrolmentID { get; set; }
        public TimeSpan FinishTime { get; set; }
        public int FinishPosition { get; set; }
    }
}