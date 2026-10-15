using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Models;
using RaceDay.Contracts.DTOs.Enrolments;
using Swashbuckle.AspNetCore.Annotations;

namespace RaceDay.Api.Controllers
{
    // Routes follow the Part 1 endpoint plan:
    //   POST /api/enrolments
    //   GET  /api/enrolments/me
    //   GET  /api/events/{eventId}/enrolments
    // plus PUT /api/enrolments/{id}/status for status management.
    [ApiController]
    public class EnrolmentsController : RaceDayControllerBase
    {
        private readonly RaceDayDbContext _context;

        public EnrolmentsController(RaceDayDbContext context)
        {
            _context = context;
        }

        // Shared query that turns Enrolment rows into EnrolmentResponseDto objects.
        private static IQueryable<EnrolmentResponseDto> ToResponses(
            IQueryable<Enrolment> enrolments)
        {
            return enrolments.Select(e => new EnrolmentResponseDto
            {
                EnrolmentId = e.EnrolmentID,
                ParticipantId = e.ParticipantID,
                ParticipantName =
                    e.Participant.FirstName + " " + e.Participant.LastName,
                EventId = e.EventID,
                EventName = e.Event.EventName,
                CategoryId = e.CategoryID,
                CategoryName = e.Category.CategoryName,
                EnrolmentDate = e.EnrolmentDate,
                Status = e.EnrolmentStatus.StatusName
            });
        }

        [HttpPost("api/enrolments")]
        [SwaggerOperation(
            Summary = "Enrol in an event (Participant)",
            Description =
                "Enters the logged-in Participant into an event using one of " +
                "that event's categories. The enrolment starts as Pending and " +
                "records the Participant, the event and the category.")]
        [SwaggerResponse(201, "Enrolment created.", typeof(EnrolmentResponseDto))]
        [SwaggerResponse(400, "Event already started, or the category does not belong to the event.")]
        [SwaggerResponse(401, "Not logged in.")]
        [SwaggerResponse(403, "Only Participants can enrol.")]
        [SwaggerResponse(404, "Event not found.")]
        [SwaggerResponse(409, "Already enrolled in this event.")]
        public async Task<IActionResult> CreateEnrolment(
            [FromBody] CreateEnrolmentRequestDto request)
        {
            var denied = CheckAccess(
                ParticipantRole,
                "Only Participants can enrol in events.");
            if (denied != null) return denied;

            int userId = CurrentUserId!.Value;

            var eventItem = await _context.Events
                .FirstOrDefaultAsync(e => e.EventID == request.EventId);

            if (eventItem == null)
            {
                return NotFound(new { message = "Event not found." });
            }

            if (eventItem.EventDate <= DateTime.UtcNow)
            {
                return BadRequest(new
                {
                    message = "You cannot enrol in an event that has already started."
                });
            }

            var category = await _context.Categories
                .FirstOrDefaultAsync(c =>
                    c.CategoryID == request.CategoryId &&
                    c.EventID == request.EventId);

            if (category == null)
            {
                return BadRequest(new
                {
                    message = "The selected category does not belong to this event."
                });
            }

            bool alreadyEnrolled = await _context.Enrolments
                .AnyAsync(e =>
                    e.ParticipantID == userId &&
                    e.EventID == request.EventId);

            if (alreadyEnrolled)
            {
                return Conflict(new
                {
                    message = "You are already enrolled in this event."
                });
            }

            var pendingStatus = await _context.EnrolmentStatuses
                .FirstOrDefaultAsync(s => s.StatusName == "Pending");

            if (pendingStatus == null)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    message = "The Pending enrolment status has not been configured."
                });
            }

            var enrolment = new Enrolment
            {
                ParticipantID = userId,
                EventID = request.EventId,
                CategoryID = request.CategoryId,
                EnrolmentStatusID = pendingStatus.EnrolmentStatusID,
                EnrolmentDate = DateTime.UtcNow
            };

            _context.Enrolments.Add(enrolment);
            await _context.SaveChangesAsync();

            string participantName = await _context.Users
                .Where(u => u.UserID == userId)
                .Select(u => u.FirstName + " " + u.LastName)
                .FirstOrDefaultAsync() ?? string.Empty;

            return CreatedAtAction(
                nameof(GetMyEnrolments),
                null,
                new EnrolmentResponseDto
                {
                    EnrolmentId = enrolment.EnrolmentID,
                    ParticipantId = userId,
                    ParticipantName = participantName,
                    EventId = eventItem.EventID,
                    EventName = eventItem.EventName,
                    CategoryId = category.CategoryID,
                    CategoryName = category.CategoryName,
                    EnrolmentDate = enrolment.EnrolmentDate,
                    Status = pendingStatus.StatusName
                });
        }

        [HttpGet("api/enrolments/me")]
        [SwaggerOperation(
            Summary = "View my enrolments (Participant)",
            Description = "Returns the logged-in Participant's enrolments with the event, category and status.")]
        [SwaggerResponse(200, "The Participant's enrolments.", typeof(List<EnrolmentResponseDto>))]
        [SwaggerResponse(401, "Not logged in.")]
        [SwaggerResponse(403, "Only Participants can use this endpoint.")]
        public async Task<IActionResult> GetMyEnrolments()
        {
            var denied = CheckAccess(
                ParticipantRole,
                "Only Participants can view their own enrolments.");
            if (denied != null) return denied;

            int userId = CurrentUserId!.Value;

            var enrolments = await ToResponses(
                    _context.Enrolments.Where(e => e.ParticipantID == userId))
                .ToListAsync();

            return Ok(enrolments);
        }

        [HttpGet("api/events/{eventId:int}/enrolments")]
        [SwaggerOperation(
            Summary = "View the enrolments for an event (owning Organiser)",
            Description = "Returns every enrolment for an event the Organiser owns.")]
        [SwaggerResponse(200, "The event's enrolments.", typeof(List<EnrolmentResponseDto>))]
        [SwaggerResponse(401, "Not logged in.")]
        [SwaggerResponse(403, "Not an Organiser, or not the owner of this event.")]
        [SwaggerResponse(404, "Event not found.")]
        public async Task<IActionResult> GetEventEnrolments(int eventId)
        {
            var denied = CheckAccess(
                OrganiserRole,
                "Only Organisers can view event enrolments.");
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
                    message = "You can only view enrolments for your own events."
                });
            }

            var enrolments = await ToResponses(
                    _context.Enrolments.Where(e => e.EventID == eventId))
                .ToListAsync();

            return Ok(enrolments);
        }

        [HttpPut("api/enrolments/{id:int}/status")]
        [SwaggerOperation(
            Summary = "Change an enrolment's status (owning Organiser)",
            Description = "Sets the status to Pending, Confirmed or Cancelled for an enrolment on an event the Organiser owns.")]
        [SwaggerResponse(200, "Status updated.")]
        [SwaggerResponse(400, "Invalid status name.")]
        [SwaggerResponse(401, "Not logged in.")]
        [SwaggerResponse(403, "Not an Organiser, or not the owner of this event.")]
        [SwaggerResponse(404, "Enrolment not found.")]
        public async Task<IActionResult> UpdateEnrolmentStatus(
            int id,
            [FromBody] UpdateEnrolmentStatusRequestDto request)
        {
            var denied = CheckAccess(
                OrganiserRole,
                "Only Organisers can update enrolment statuses.");
            if (denied != null) return denied;

            int userId = CurrentUserId!.Value;

            var enrolment = await _context.Enrolments
                .Include(e => e.Event)
                .FirstOrDefaultAsync(e => e.EnrolmentID == id);

            if (enrolment == null)
            {
                return NotFound(new { message = "Enrolment not found." });
            }

            if (enrolment.Event.OrganiserID != userId)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    message = "You can only update enrolments for your own events."
                });
            }

            var status = await _context.EnrolmentStatuses
                .FirstOrDefaultAsync(s => s.StatusName == request.StatusName);

            if (status == null)
            {
                return BadRequest(new { message = "Invalid enrolment status." });
            }

            enrolment.EnrolmentStatusID = status.EnrolmentStatusID;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Enrolment status updated successfully." });
        }
    }
}
