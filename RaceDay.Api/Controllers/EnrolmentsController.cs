using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EnrolmentsController : ControllerBase
    {
        private readonly RaceDayDbContext _context;

        public EnrolmentsController(RaceDayDbContext context)
        {
            _context = context;
        }

        // Participants can view their own enrolments.
        [HttpGet("my")]
        public async Task<IActionResult> GetMyEnrolments()
        {
            int? userId = HttpContext.Session.GetInt32("UserId");
            string? role = HttpContext.Session.GetString("Role");

            if (userId == null || role == null)
                return Unauthorized(new { message = "Please log in first." });

            if (role != "Participant")
                return Forbid();

            var enrolments = await _context.Enrolments
                .Where(e => e.ParticipantID == userId.Value)
                .Select(e => new
                {
                    e.EnrolmentID,
                    e.EventID,
                    EventName = e.Event.EventName,
                    e.CategoryID,
                    CategoryName = e.Category.CategoryName,
                    e.EnrolmentDate,
                    Status = e.EnrolmentStatus.StatusName
                })
                .ToListAsync();

            return Ok(enrolments);
        }

        // Participants can enrol in an event and select a category.
        [HttpPost]
        public async Task<IActionResult> CreateEnrolment(
            [FromBody] CreateEnrolmentRequest request)
        {
            int? userId = HttpContext.Session.GetInt32("UserId");
            string? role = HttpContext.Session.GetString("Role");

            if (userId == null || role == null)
                return Unauthorized(new { message = "Please log in first." });

            if (role != "Participant")
                return Forbid();

            var eventItem = await _context.Events
                .FirstOrDefaultAsync(e => e.EventID == request.EventID);

            if (eventItem == null)
                return NotFound(new { message = "Event not found." });

            if (eventItem.EventDate <= DateTime.UtcNow)
            {
                return BadRequest(new
                {
                    message = "You cannot enrol in an event that has already started."
                });
            }

            var category = await _context.Categories
                .FirstOrDefaultAsync(c =>
                    c.CategoryID == request.CategoryID &&
                    c.EventID == request.EventID);

            if (category == null)
            {
                return BadRequest(new
                {
                    message = "The selected category does not belong to this event."
                });
            }

            bool alreadyEnrolled = await _context.Enrolments
                .AnyAsync(e =>
                    e.ParticipantID == userId.Value &&
                    e.EventID == request.EventID);

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
                return StatusCode(500, new
                {
                    message = "The Pending enrolment status has not been configured."
                });
            }

            var enrolment = new Enrolment
            {
                ParticipantID = userId.Value,
                EventID = request.EventID,
                CategoryID = request.CategoryID,
                EnrolmentStatusID = pendingStatus.EnrolmentStatusID,
                EnrolmentDate = DateTime.UtcNow
            };

            _context.Enrolments.Add(enrolment);
            await _context.SaveChangesAsync();

            return CreatedAtAction(
                nameof(GetMyEnrolments),
                new
                {
                    message = "Enrolment created successfully.",
                    enrolment.EnrolmentID,
                    enrolment.EventID,
                    enrolment.CategoryID,
                    enrolment.EnrolmentDate
                });
        }

        // Organisers can view enrolments for their own events.
        [HttpGet("event/{eventId:int}")]
        public async Task<IActionResult> GetEventEnrolments(int eventId)
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

            var enrolments = await _context.Enrolments
                .Where(e => e.EventID == eventId)
                .Select(e => new
                {
                    e.EnrolmentID,
                    e.ParticipantID,
                    ParticipantName = e.Participant.FirstName + " " +
                                      e.Participant.LastName,
                    e.CategoryID,
                    CategoryName = e.Category.CategoryName,
                    e.EnrolmentDate,
                    Status = e.EnrolmentStatus.StatusName
                })
                .ToListAsync();

            return Ok(enrolments);
        }

        // Organisers can update enrolment status for their own events.
        [HttpPut("{id:int}/status")]
        public async Task<IActionResult> UpdateEnrolmentStatus(
            int id, [FromBody] UpdateEnrolmentStatusRequest request)
        {
            int? userId = HttpContext.Session.GetInt32("UserId");
            string? role = HttpContext.Session.GetString("Role");

            if (userId == null || role == null)
                return Unauthorized(new { message = "Please log in first." });

            if (role != "Organiser")
                return Forbid();

            var enrolment = await _context.Enrolments
                .Include(e => e.Event)
                .FirstOrDefaultAsync(e => e.EnrolmentID == id);

            if (enrolment == null)
                return NotFound(new { message = "Enrolment not found." });

            if (enrolment.Event.OrganiserID != userId.Value)
                return Forbid();

            var status = await _context.EnrolmentStatuses
                .FirstOrDefaultAsync(s => s.StatusName == request.StatusName);

            if (status == null)
            {
                return BadRequest(new
                {
                    message = "Invalid enrolment status."
                });
            }

            enrolment.EnrolmentStatusID = status.EnrolmentStatusID;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Enrolment status updated successfully." });
        }
    }

    public class CreateEnrolmentRequest
    {
        public int EventID { get; set; }
        public int CategoryID { get; set; }
    }

    public class UpdateEnrolmentStatusRequest
    {
        public string StatusName { get; set; } = string.Empty;
    }
}