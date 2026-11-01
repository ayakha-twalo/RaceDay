using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Models;
using RaceDay.Api.Services;
using RaceDay.Contracts.DTOs.Events;
using RaceDay.Contracts.DTOs.Images;
using Swashbuckle.AspNetCore.Annotations;

namespace RaceDay.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EventsController : RaceDayControllerBase
    {
        private readonly RaceDayDbContext _context;

        public EventsController(RaceDayDbContext context)
        {
            _context = context;
        }

        // Shared query that turns Event rows into EventResponseDto objects.
        // The banner image URL is also included in the response.
        private IQueryable<EventResponseDto> EventQuery()
        {
            return _context.Events.Select(e => new EventResponseDto
            {
                EventId = e.EventID,
                EventName = e.EventName,
                Description = e.Description,
                EventDate = e.EventDate,
                Location = e.Location,
                Distance = e.Distance,
                EventTypeId = e.EventTypeID,
                EventTypeName = e.EventType.TypeName,
                OrganiserId = e.OrganiserID,

                // Return the Azure event banner URL.
                BannerImageUrl = e.BannerImageUrl
            });
        }

        // GET: api/events
        // Allows users to browse all RaceDay events.
        [HttpGet]
        [SwaggerOperation(
            Summary = "Browse events",
            Description = "Returns all RaceDay events. Open to everyone, as in the Part 1 plan.")]
        [SwaggerResponse(200, "List of events.", typeof(List<EventResponseDto>))]
        public async Task<IActionResult> GetEvents()
        {
            var events = await EventQuery()
                .OrderBy(e => e.EventDate)
                .ToListAsync();

            return Ok(events);
        }

        // GET: api/events/mine
        // Returns only events belonging to the logged-in organiser.
        [HttpGet("mine")]
        [SwaggerOperation(
            Summary = "View my events (Organiser)",
            Description = "Returns only the events created by the logged-in Organiser.")]
        [SwaggerResponse(200, "The Organiser's events.", typeof(List<EventResponseDto>))]
        [SwaggerResponse(401, "Not logged in.")]
        [SwaggerResponse(403, "Only Organisers can use this endpoint.")]
        public async Task<IActionResult> GetMyEvents()
        {
            var denied = CheckAccess(
                OrganiserRole,
                "Only Organisers can view their own events.");

            if (denied != null)
            {
                return denied;
            }

            int userId = CurrentUserId!.Value;

            var events = await EventQuery()
                .Where(e => e.OrganiserId == userId)
                .OrderBy(e => e.EventDate)
                .ToListAsync();

            return Ok(events);
        }

        // GET: api/events/{id}
        // Returns information about one event.
        [HttpGet("{id:int}")]
        [SwaggerOperation(
            Summary = "View one event",
            Description = "Returns the details of a single event by its id.")]
        [SwaggerResponse(200, "The event.", typeof(EventResponseDto))]
        [SwaggerResponse(404, "Event not found.")]
        public async Task<IActionResult> GetEvent(int id)
        {
            var eventItem = await EventQuery()
                .Where(e => e.EventId == id)
                .FirstOrDefaultAsync();

            if (eventItem == null)
            {
                return NotFound(new { message = "Event not found." });
            }

            return Ok(eventItem);
        }

        // POST: api/events
        // Allows an organiser to create a new event.
        [HttpPost]
        [SwaggerOperation(
            Summary = "Create an event (Organiser)",
            Description =
                "Creates an event owned by the logged-in Organiser. " +
                "EventTypeId: 1 = Run, 2 = Walk, 3 = Cycle. " +
                "The date must be in the future and the distance above zero.")]
        [SwaggerResponse(201, "Event created.", typeof(EventResponseDto))]
        [SwaggerResponse(400, "Invalid date, distance or event type.")]
        [SwaggerResponse(401, "Not logged in.")]
        [SwaggerResponse(403, "Only Organisers can create events.")]
        public async Task<IActionResult> CreateEvent(
            [FromBody] CreateEventRequestDto request)
        {
            var denied = CheckAccess(
                OrganiserRole,
                "Only Organisers can create events.");

            if (denied != null)
            {
                return denied;
            }

            int userId = CurrentUserId!.Value;

            if (request.EventDate <= DateTime.UtcNow ||
                request.Distance <= 0)
            {
                return BadRequest(new
                {
                    message = "The event date must be in the future and the distance must be above zero."
                });
            }

            var eventType = await _context.EventTypes
                .FirstOrDefaultAsync(t =>
                    t.EventTypeID == request.EventTypeId);

            if (eventType == null)
            {
                return BadRequest(new
                {
                    message = "Event type does not exist."
                });
            }

            var eventItem = new Event
            {
                EventName = request.EventName.Trim(),
                Description = request.Description,
                EventDate = request.EventDate,
                Location = request.Location.Trim(),
                Distance = request.Distance,
                OrganiserID = userId,
                EventTypeID = request.EventTypeId
            };

            _context.Events.Add(eventItem);

            await _context.SaveChangesAsync();

            return CreatedAtAction(
                nameof(GetEvent),
                new { id = eventItem.EventID },
                ToResponse(eventItem, eventType.TypeName));
        }

        // PUT: api/events/{id}
        // Allows the organiser who owns an event to update it.
        [HttpPut("{id:int}")]
        [SwaggerOperation(
            Summary = "Update an event (owning Organiser)",
            Description = "Only the Organiser who created the event can update it.")]
        [SwaggerResponse(200, "The updated event.", typeof(EventResponseDto))]
        [SwaggerResponse(400, "Invalid date, distance or event type.")]
        [SwaggerResponse(401, "Not logged in.")]
        [SwaggerResponse(403, "Not an Organiser, or not the owner of this event.")]
        [SwaggerResponse(404, "Event not found.")]
        public async Task<IActionResult> UpdateEvent(
            int id,
            [FromBody] UpdateEventRequestDto request)
        {
            var denied = CheckAccess(
                OrganiserRole,
                "Only Organisers can update events.");

            if (denied != null)
            {
                return denied;
            }

            int userId = CurrentUserId!.Value;

            var eventItem = await _context.Events
                .FirstOrDefaultAsync(e => e.EventID == id);

            if (eventItem == null)
            {
                return NotFound(new { message = "Event not found." });
            }

            if (eventItem.OrganiserID != userId)
            {
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    new
                    {
                        message = "You can only update your own events."
                    });
            }

            // A new date must be in the future.
            // Keeping the existing date is allowed.
            bool dateChanged =
                request.EventDate.Date != eventItem.EventDate.Date;

            if ((dateChanged && request.EventDate <= DateTime.UtcNow) ||
                request.Distance <= 0)
            {
                return BadRequest(new
                {
                    message = "The event date must be in the future and the distance must be above zero."
                });
            }

            var eventType = await _context.EventTypes
                .FirstOrDefaultAsync(t =>
                    t.EventTypeID == request.EventTypeId);

            if (eventType == null)
            {
                return BadRequest(new
                {
                    message = "Event type does not exist."
                });
            }

            eventItem.EventName = request.EventName.Trim();
            eventItem.Description = request.Description;
            eventItem.EventDate = request.EventDate;
            eventItem.Location = request.Location.Trim();
            eventItem.Distance = request.Distance;
            eventItem.EventTypeID = request.EventTypeId;

            await _context.SaveChangesAsync();

            return Ok(ToResponse(eventItem, eventType.TypeName));
        }

        // POST: api/events/{id}/banner
        // Allows the organiser who owns an event to upload its banner.
        [HttpPost("{id:int}/banner")]
        [Consumes("multipart/form-data")]
        [SwaggerOperation(
            Summary = "Upload an event banner",
            Description = "Uploads an event banner to Azure Blob Storage. Only the event's organiser can do this.")]
        [SwaggerResponse(200, "Event banner uploaded successfully.", typeof(ImageUploadResponseDto))]
        [SwaggerResponse(400, "Invalid image file.")]
        [SwaggerResponse(401, "Not logged in.")]
        [SwaggerResponse(403, "Only the event's organiser can upload its banner.")]
        [SwaggerResponse(404, "Event not found.")]
        public async Task<IActionResult> UploadBanner(
            int id,
            [FromForm] IFormFile? file,
            [FromServices] BlobStorageService blobStorage)
        {
            // Check that the logged-in user is an organiser.
            var denied = CheckAccess(
                OrganiserRole,
                "Only Organisers can upload event banners.");

            if (denied != null)
            {
                return denied;
            }

            int userId = CurrentUserId!.Value;

            // Find the event in the database.
            var eventItem = await _context.Events
                .FirstOrDefaultAsync(e => e.EventID == id);

            if (eventItem == null)
            {
                return NotFound(new
                {
                    message = "Event not found."
                });
            }

            // Check that this organiser owns the event.
            if (eventItem.OrganiserID != userId)
            {
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    new
                    {
                        message = "You can only upload banners for your own events."
                    });
            }

            // Check whether the selected image is valid.
            string? validationError = blobStorage.Validate(file);

            if (validationError != null)
            {
                return BadRequest(new
                {
                    message = validationError
                });
            }

            try
            {
                // Upload the image to the event-banners container.
                string imageUrl = await blobStorage.UploadAsync(
                    file!,
                    "event-banners");

                // Save the returned Azure URL in the database.
                eventItem.BannerImageUrl = imageUrl;

                await _context.SaveChangesAsync();

                // Return the uploaded image URL.
                return Ok(new ImageUploadResponseDto
                {
                    ImageUrl = imageUrl
                });
            }
            catch (Exception)
            {
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new
                    {
                        message = "The event banner could not be uploaded. Please try again."
                    });
            }
        }

        // DELETE: api/events/{id}
        // Allows the organiser who owns an event to delete it.
        [HttpDelete("{id:int}")]
        [SwaggerOperation(
            Summary = "Delete an event (owning Organiser)",
            Description =
                "Only the Organiser who created the event can delete it. " +
                "Events that already have enrolments cannot be deleted.")]
        [SwaggerResponse(204, "Event deleted.")]
        [SwaggerResponse(401, "Not logged in.")]
        [SwaggerResponse(403, "Not an Organiser, or not the owner of this event.")]
        [SwaggerResponse(404, "Event not found.")]
        [SwaggerResponse(409, "The event has enrolments and cannot be deleted.")]
        public async Task<IActionResult> DeleteEvent(int id)
        {
            var denied = CheckAccess(
                OrganiserRole,
                "Only Organisers can delete events.");

            if (denied != null)
            {
                return denied;
            }

            int userId = CurrentUserId!.Value;

            var eventItem = await _context.Events
                .FirstOrDefaultAsync(e => e.EventID == id);

            if (eventItem == null)
            {
                return NotFound(new
                {
                    message = "Event not found."
                });
            }

            if (eventItem.OrganiserID != userId)
            {
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    new
                    {
                        message = "You can only delete your own events."
                    });
            }

            bool hasEnrolments = await _context.Enrolments
                .AnyAsync(e => e.EventID == id);

            if (hasEnrolments)
            {
                return Conflict(new
                {
                    message = "This event already has enrolments and cannot be deleted."
                });
            }

            _context.Events.Remove(eventItem);

            await _context.SaveChangesAsync();

            return NoContent();
        }

        // Converts an Event database record into an EventResponseDto.
        // The banner URL is included in the response.
        private static EventResponseDto ToResponse(
            Event eventItem,
            string eventTypeName)
        {
            return new EventResponseDto
            {
                EventId = eventItem.EventID,
                EventName = eventItem.EventName,
                Description = eventItem.Description,
                EventDate = eventItem.EventDate,
                Location = eventItem.Location,
                Distance = eventItem.Distance,
                EventTypeId = eventItem.EventTypeID,
                EventTypeName = eventTypeName,
                OrganiserId = eventItem.OrganiserID,

                // Include the event banner URL.
                BannerImageUrl = eventItem.BannerImageUrl
            };
        }
    }
}