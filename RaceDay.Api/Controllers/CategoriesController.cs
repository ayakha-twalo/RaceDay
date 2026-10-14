using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CategoriesController : ControllerBase
    {
        private readonly RaceDayDbContext _context;

    public CategoriesController(RaceDayDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetCategories()
        {
            var categories = await _context.Categories
                .Select(c => new
                {
                    c.CategoryID,
                    c.EventID,
                    c.CategoryName,
                    c.Description
                })
                .ToListAsync();

            return Ok(categories);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetCategory(int id)
        {
            var category = await _context.Categories
                .Where(c => c.CategoryID == id)
                .Select(c => new
                {
                    c.CategoryID,
                    c.EventID,
                    c.CategoryName,
                    c.Description
                })
                .FirstOrDefaultAsync();

            if (category == null)
                return NotFound(new { message = "Category not found." });

            return Ok(category);
        }

        [HttpPost]
        public async Task<IActionResult> CreateCategory(
            [FromBody] CategoryRequest request)
        {
            int? userId = HttpContext.Session.GetInt32("UserId");
            string? role = HttpContext.Session.GetString("Role");

            if (userId == null || role == null)
                return Unauthorized(new { message = "Please log in first." });

            if (role != "Organiser")
                return Forbid();

            var eventItem = await _context.Events
                .FirstOrDefaultAsync(e => e.EventID == request.EventID);

            if (eventItem == null)
                return NotFound(new { message = "Event not found." });

            if (eventItem.OrganiserID != userId.Value)
                return Forbid();

            var category = new Category
            {
                EventID = request.EventID,
                CategoryName = request.CategoryName.Trim(),
                Description = request.Description
            };

            _context.Categories.Add(category);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Category created successfully.",
                category.CategoryID
            });
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateCategory(
            int id, [FromBody] CategoryRequest request)
        {
            int? userId = HttpContext.Session.GetInt32("UserId");
            string? role = HttpContext.Session.GetString("Role");

            if (userId == null || role == null)
                return Unauthorized(new { message = "Please log in first." });

            if (role != "Organiser")
                return Forbid();

            var category = await _context.Categories
                .Include(c => c.Event)
                .FirstOrDefaultAsync(c => c.CategoryID == id);

            if (category == null)
                return NotFound(new { message = "Category not found." });

            if (category.Event.OrganiserID != userId.Value)
                return Forbid();

            if (category.EventID != request.EventID)
            {
                var targetEvent = await _context.Events
                    .FirstOrDefaultAsync(e => e.EventID == request.EventID);

                if (targetEvent == null)
                    return NotFound(new { message = "Target event not found." });

                if (targetEvent.OrganiserID != userId.Value)
                    return Forbid();
            }

            category.EventID = request.EventID;
            category.CategoryName = request.CategoryName.Trim();
            category.Description = request.Description;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Category updated successfully." });
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            int? userId = HttpContext.Session.GetInt32("UserId");
            string? role = HttpContext.Session.GetString("Role");

            if (userId == null || role == null)
                return Unauthorized(new { message = "Please log in first." });

            if (role != "Organiser")
                return Forbid();

            var category = await _context.Categories
                .Include(c => c.Event)
                .FirstOrDefaultAsync(c => c.CategoryID == id);

            if (category == null)
                return NotFound(new { message = "Category not found." });

            if (category.Event.OrganiserID != userId.Value)
                return Forbid();

            if (await _context.Enrolments.AnyAsync(
                e => e.CategoryID == id))
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
    }

    public class CategoryRequest
    {
        public int EventID { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

}
