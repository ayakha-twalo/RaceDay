using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Models;
using RaceDay.Contracts.DTOs.Categories;
using Swashbuckle.AspNetCore.Annotations;

namespace RaceDay.Api.Controllers
{
    // Routes follow the Part 1 endpoint plan:
    //   GET/POST /api/events/{eventId}/categories
    //   PUT/DELETE /api/categories/{id}
    [ApiController]
    public class CategoriesController : RaceDayControllerBase
    {
        private readonly RaceDayDbContext _context;

        public CategoriesController(RaceDayDbContext context)
        {
            _context = context;
        }

        [HttpGet("api/events/{eventId:int}/categories")]
        [SwaggerOperation(
            Summary = "View the categories of an event",
            Description = "Returns every category defined for the event. Open to everyone, as in the Part 1 plan.")]
        [SwaggerResponse(200, "List of categories.", typeof(List<CategoryResponseDto>))]
        [SwaggerResponse(404, "Event not found.")]
        public async Task<IActionResult> GetEventCategories(int eventId)
        {
            bool eventExists = await _context.Events
                .AnyAsync(e => e.EventID == eventId);

            if (!eventExists)
            {
                return NotFound(new { message = "Event not found." });
            }

            var categories = await _context.Categories
                .Where(c => c.EventID == eventId)
                .OrderBy(c => c.CategoryName)
                .Select(c => new CategoryResponseDto
                {
                    CategoryId = c.CategoryID,
                    EventId = c.EventID,
                    CategoryName = c.CategoryName,
                    Description = c.Description
                })
                .ToListAsync();

            return Ok(categories);
        }

        [HttpPost("api/events/{eventId:int}/categories")]
        [SwaggerOperation(
            Summary = "Create a category (owning Organiser)",
            Description = "Adds an age or distance category (for example Under 20, Senior, 10km) to an event the Organiser owns.")]
        [SwaggerResponse(201, "Category created.", typeof(CategoryResponseDto))]
        [SwaggerResponse(400, "Invalid details.")]
        [SwaggerResponse(401, "Not logged in.")]
        [SwaggerResponse(403, "Not an Organiser, or not the owner of this event.")]
        [SwaggerResponse(404, "Event not found.")]
        [SwaggerResponse(409, "This event already has a category with that name.")]
        public async Task<IActionResult> CreateCategory(
            int eventId,
            [FromBody] CreateCategoryRequestDto request)
        {
            var denied = CheckAccess(
                OrganiserRole,
                "Only Organisers can create categories.");
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
                    message = "You can only add categories to your own events."
                });
            }

            string name = request.CategoryName.Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                return BadRequest(new { message = "Category name is required." });
            }

            bool nameTaken = await _context.Categories
                .AnyAsync(c => c.EventID == eventId && c.CategoryName == name);

            if (nameTaken)
            {
                return Conflict(new
                {
                    message = "This event already has a category with that name."
                });
            }

            var category = new Category
            {
                EventID = eventId,
                CategoryName = name,
                Description = request.Description
            };

            _context.Categories.Add(category);
            await _context.SaveChangesAsync();

            return CreatedAtAction(
                nameof(GetEventCategories),
                new { eventId },
                ToResponse(category));
        }

        [HttpPut("api/categories/{id:int}")]
        [SwaggerOperation(
            Summary = "Update a category (owning Organiser)",
            Description = "Changes the name or description of a category on an event the Organiser owns.")]
        [SwaggerResponse(200, "The updated category.", typeof(CategoryResponseDto))]
        [SwaggerResponse(400, "Invalid details.")]
        [SwaggerResponse(401, "Not logged in.")]
        [SwaggerResponse(403, "Not an Organiser, or not the owner of this event.")]
        [SwaggerResponse(404, "Category not found.")]
        [SwaggerResponse(409, "This event already has a category with that name.")]
        public async Task<IActionResult> UpdateCategory(
            int id,
            [FromBody] CreateCategoryRequestDto request)
        {
            var denied = CheckAccess(
                OrganiserRole,
                "Only Organisers can update categories.");
            if (denied != null) return denied;

            int userId = CurrentUserId!.Value;

            var category = await _context.Categories
                .Include(c => c.Event)
                .FirstOrDefaultAsync(c => c.CategoryID == id);

            if (category == null)
            {
                return NotFound(new { message = "Category not found." });
            }

            if (category.Event.OrganiserID != userId)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    message = "You can only update categories on your own events."
                });
            }

            string name = request.CategoryName.Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                return BadRequest(new { message = "Category name is required." });
            }

            bool nameTaken = await _context.Categories.AnyAsync(c =>
                c.EventID == category.EventID &&
                c.CategoryName == name &&
                c.CategoryID != id);

            if (nameTaken)
            {
                return Conflict(new
                {
                    message = "This event already has a category with that name."
                });
            }

            category.CategoryName = name;
            category.Description = request.Description;

            await _context.SaveChangesAsync();

            return Ok(ToResponse(category));
        }

        [HttpDelete("api/categories/{id:int}")]
        [SwaggerOperation(
            Summary = "Delete a category (owning Organiser)",
            Description = "Deletes a category on an event the Organiser owns. Categories that already have enrolments cannot be deleted.")]
        [SwaggerResponse(204, "Category deleted.")]
        [SwaggerResponse(401, "Not logged in.")]
        [SwaggerResponse(403, "Not an Organiser, or not the owner of this event.")]
        [SwaggerResponse(404, "Category not found.")]
        [SwaggerResponse(409, "The category has enrolments and cannot be deleted.")]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            var denied = CheckAccess(
                OrganiserRole,
                "Only Organisers can delete categories.");
            if (denied != null) return denied;

            int userId = CurrentUserId!.Value;

            var category = await _context.Categories
                .Include(c => c.Event)
                .FirstOrDefaultAsync(c => c.CategoryID == id);

            if (category == null)
            {
                return NotFound(new { message = "Category not found." });
            }

            if (category.Event.OrganiserID != userId)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    message = "You can only delete categories on your own events."
                });
            }

            bool hasEnrolments = await _context.Enrolments
                .AnyAsync(e => e.CategoryID == id);

            if (hasEnrolments)
            {
                return Conflict(new
                {
                    message = "Cannot delete a category that has enrolments."
                });
            }

            _context.Categories.Remove(category);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private static CategoryResponseDto ToResponse(Category category)
        {
            return new CategoryResponseDto
            {
                CategoryId = category.CategoryID,
                EventId = category.EventID,
                CategoryName = category.CategoryName,
                Description = category.Description
            };
        }
    }
}
