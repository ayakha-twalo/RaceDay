using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers
{
    public class EventRequest
    {
        public string EventName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime EventDate { get; set; }
        public string Location { get; set; } = string.Empty;
        public decimal Distance { get; set; }
        public int EventTypeID { get; set; }
    }

[ApiController]
    [Route("api/[controller]")]
    public class EventsController : ControllerBase
    {
        private readonly RaceDayDbContext _context;

        public EventsController(RaceDayDbContext context)
        {
            _context = context;
        }

        // Anyone can browse events.
        [HttpGet]
        public async Task<IActionResult> GetEvents()
        {
            var events = await _context.Events
                .Select(e => new
                {
                    e.EventID,
                    e.EventName,
                    e.Description,
                    e.EventDate,
                    e.Location,
                    e.Distance,
                    e.OrganiserID,
                    e.EventTypeID,
                    EventType = e.EventType.TypeName
                })
                .ToListAsync();

            return Ok(events);
        }

        // View one event.
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetEvent(int id)
        {
            var eventItem = await _context.Events
                .Where(e => e.EventID == id)
                .Select(e => new
                {
                    e.EventID,
                    e.EventName,
                    e.Description,
                    e.EventDate,
                    e.Location,
                    e.Distance,
                    e.OrganiserID,
                    e.EventTypeID,
                    EventType = e.EventType.TypeName
                })
                .FirstOrDefaultAsync();

            if (eventItem == null)
                return NotFound(new { message = "Event not found." });

            return Ok(eventItem);
        }

        // Organisers can create events.
        [HttpPost]
        public async Task<IActionResult> CreateEvent(
            [FromBody] EventRequest request)
        {
            int? userId = HttpContext.Session.GetInt32("UserId");
            string? role = HttpContext.Session.GetString("Role");

            if (userId == null || role == null)
                return Unauthorized(new { message = "Please log in first." });

            if (role != "Organiser")
                return Forbid();

            if (request.EventDate <= DateTime.UtcNow || request.Distance <= 0)
                return BadRequest(new { message = "Check the event date and distance." });

            bool typeExists = await _context.EventTypes
                .AnyAsync(t => t.EventTypeID == request.EventTypeID);

            if (!typeExists)
                return BadRequest(new { message = "Event type does not exist." });

            var eventItem = new Event
            {
                EventName = request.EventName.Trim(),
                Description = request.Description,
                EventDate = request.EventDate,
                Location = request.Location.Trim(),
                Distance = request.Distance,
                OrganiserID = userId.Value,
                EventTypeID = request.EventTypeID
            };

            _context.Events.Add(eventItem);
            await _context.SaveChangesAsync();

            return CreatedAtAction(
                nameof(GetEvent),
                new { id = eventItem.EventID },
                eventItem);
        }

        // Only the owning Organiser can update an event.
        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateEvent(
            int id, [FromBody] EventRequest request)
        {
            int? userId = HttpContext.Session.GetInt32("UserId");
            string? role = HttpContext.Session.GetString("Role");

            if (userId == null || role == null)
                return Unauthorized(new { message = "Please log in first." });

            if (role != "Organiser")
                return Forbid();

            var eventItem = await _context.Events
                .FirstOrDefaultAsync(e => e.EventID == id);

            if (eventItem == null)
                return NotFound(new { message = "Event not found." });

            if (eventItem.OrganiserID != userId.Value)
                return Forbid();

            if (request.EventDate <= DateTime.UtcNow || request.Distance <= 0)
                return BadRequest(new { message = "Check the event date and distance." });

            bool typeExists = await _context.EventTypes
                .AnyAsync(t => t.EventTypeID == request.EventTypeID);

            if (!typeExists)
                return BadRequest(new { message = "Event type does not exist." });

            eventItem.EventName = request.EventName.Trim();
            eventItem.Description = request.Description;
            eventItem.EventDate = request.EventDate;
            eventItem.Location = request.Location.Trim();
            eventItem.Distance = request.Distance;
            eventItem.EventTypeID = request.EventTypeID;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Event updated successfully." });
        }

        // Only the owning Organiser can delete an event.
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteEvent(int id)
        {
            int? userId = HttpContext.Session.GetInt32("UserId");
            string? role = HttpContext.Session.GetString("Role");

            if (userId == null || role == null)
                return Unauthorized(new { message = "Please log in first." });

            if (role != "Organiser")
                return Forbid();

            var eventItem = await _context.Events
                .FirstOrDefaultAsync(e => e.EventID == id);

            if (eventItem == null)
                return NotFound(new { message = "Event not found." });

            if (eventItem.OrganiserID != userId.Value)
                return Forbid();

            _context.Events.Remove(eventItem);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }

}
