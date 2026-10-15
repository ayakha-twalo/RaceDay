using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Models;
using RaceDay.Contracts.DTOs.Results;
using Swashbuckle.AspNetCore.Annotations;

namespace RaceDay.Api.Controllers
{
    // Routes follow the Part 1 endpoint plan:
    //   POST /api/enrolments/{enrolmentId}/result
    //   GET  /api/results/me
    // plus GET /api/results/event/{eventId} so Organisers can review results.
    [ApiController]
    public class ResultsController : RaceDayControllerBase
    {
        private readonly RaceDayDbContext _context;

        public ResultsController(RaceDayDbContext context)
        {
            _context = context;
        }

        private static IQueryable<ResultResponseDto> ToResponses(
            IQueryable<Result> results)
        {
            return results.Select(r => new ResultResponseDto
            {
                ResultId = r.ResultID,
                EnrolmentId = r.EnrolmentID,
                EventName = r.Enrolment.Event.EventName,
                ParticipantName =
                    r.Enrolment.Participant.FirstName + " " +
                    r.Enrolment.Participant.LastName,
                FinishTime = r.FinishTime,
                FinishPosition = r.FinishPosition
            });
        }

        [HttpPost("api/enrolments/{enrolmentId:int}/result")]
        [SwaggerOperation(
            Summary = "Capture a result (owning Organiser)",
            Description =
                "Records the finish time (hh:mm:ss) and finishing position for " +
                "a Participant's enrolment. The Organiser must own the event.")]
        [SwaggerResponse(201, "Result recorded.", typeof(ResultResponseDto))]
        [SwaggerResponse(400, "Finish time or position is not valid.")]
        [SwaggerResponse(401, "Not logged in.")]
        [SwaggerResponse(403, "Not an Organiser, or not the owner of this event.")]
        [SwaggerResponse(404, "Enrolment not found.")]
        [SwaggerResponse(409, "A result already exists for this enrolment.")]
        public async Task<IActionResult> RecordResult(
            int enrolmentId,
            [FromBody] CreateResultRequestDto request)
        {
            var denied = CheckAccess(
                OrganiserRole,
                "Only Organisers can record race results.");
            if (denied != null) return denied;

            int userId = CurrentUserId!.Value;

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
                .Include(e => e.Participant)
                .FirstOrDefaultAsync(e => e.EnrolmentID == enrolmentId);

            if (enrolment == null)
            {
                return NotFound(new { message = "Enrolment not found." });
            }

            if (enrolment.Event.OrganiserID != userId)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    message = "You can only record results for your own events."
                });
            }

            bool resultExists = await _context.Results
                .AnyAsync(r => r.EnrolmentID == enrolmentId);

            if (resultExists)
            {
                return Conflict(new
                {
                    message = "A result has already been recorded for this enrolment."
                });
            }

            var result = new Result
            {
                EnrolmentID = enrolmentId,
                FinishTime = request.FinishTime,
                FinishPosition = request.FinishPosition
            };

            _context.Results.Add(result);
            await _context.SaveChangesAsync();

            return CreatedAtAction(
                nameof(GetEventResults),
                new { eventId = enrolment.EventID },
                new ResultResponseDto
                {
                    ResultId = result.ResultID,
                    EnrolmentId = result.EnrolmentID,
                    EventName = enrolment.Event.EventName,
                    ParticipantName =
                        enrolment.Participant.FirstName + " " +
                        enrolment.Participant.LastName,
                    FinishTime = result.FinishTime,
                    FinishPosition = result.FinishPosition
                });
        }

        [HttpGet("api/results/me")]
        [SwaggerOperation(
            Summary = "View my results (Participant)",
            Description = "Returns the logged-in Participant's own race results.")]
        [SwaggerResponse(200, "The Participant's results.", typeof(List<ResultResponseDto>))]
        [SwaggerResponse(401, "Not logged in.")]
        [SwaggerResponse(403, "Only Participants can use this endpoint.")]
        public async Task<IActionResult> GetMyResults()
        {
            var denied = CheckAccess(
                ParticipantRole,
                "Only Participants can view their own results.");
            if (denied != null) return denied;

            int userId = CurrentUserId!.Value;

            var results = await ToResponses(
                    _context.Results.Where(r => r.Enrolment.ParticipantID == userId))
                .ToListAsync();

            return Ok(results);
        }

        [HttpGet("api/results/event/{eventId:int}")]
        [SwaggerOperation(
            Summary = "View the results of an event (owning Organiser)",
            Description = "Returns all captured results for an event the Organiser owns, ordered by finishing position.")]
        [SwaggerResponse(200, "The event's results.", typeof(List<ResultResponseDto>))]
        [SwaggerResponse(401, "Not logged in.")]
        [SwaggerResponse(403, "Not an Organiser, or not the owner of this event.")]
        [SwaggerResponse(404, "Event not found.")]
        public async Task<IActionResult> GetEventResults(int eventId)
        {
            var denied = CheckAccess(
                OrganiserRole,
                "Only Organisers can view event results.");
            if (denied != null) return denied;

            int userId = CurrentUserId!.Value;

            var eventItem = await _context.Events
                .FirstOrDefaultAsync(e => e.EventID == eventId);

            if (eventItem == null)
            {
                return NotFound(new { message = "Event not found." });
            }

            if (eventItem.OrganiserID != userId)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    message = "You can only view results for your own events."
                });
            }

            var results = await ToResponses(
                    _context.Results.Where(r => r.Enrolment.EventID == eventId))
                .OrderBy(r => r.FinishPosition)
                .ToListAsync();

            return Ok(results);
        }
    }
}
