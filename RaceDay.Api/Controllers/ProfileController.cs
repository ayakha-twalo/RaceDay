using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Contracts.DTOs.Profile;
using Swashbuckle.AspNetCore.Annotations;

namespace RaceDay.Api.Controllers
{
    // Route follows the Part 1 endpoint plan: /api/users/me
    [ApiController]
    [Route("api/users")]
    public class ProfileController : RaceDayControllerBase
    {
        private readonly RaceDayDbContext _context;

        public ProfileController(RaceDayDbContext context)
        {
            _context = context;
        }

        [HttpGet("me")]
        [SwaggerOperation(
            Summary = "View my profile",
            Description = "Returns the profile of the logged-in user (either role).")]
        [SwaggerResponse(200, "The user's profile.", typeof(ProfileResponseDto))]
        [SwaggerResponse(401, "Not logged in.")]
        [SwaggerResponse(404, "Profile not found.")]
        public async Task<IActionResult> GetMyProfile()
        {
            var denied = CheckAccess(null, string.Empty);
            if (denied != null) return denied;

            int userId = CurrentUserId!.Value;

            var profile = await _context.Users
                .Where(u => u.UserID == userId)
                .Select(u => new ProfileResponseDto
                {
                    UserId = u.UserID,
                    FirstName = u.FirstName,
                    LastName = u.LastName,
                    Email = u.Email,
                    PhoneNumber = u.PhoneNumber,
                    Role = u.Role
                })
                .FirstOrDefaultAsync();

            if (profile == null)
            {
                return NotFound(new { message = "User profile not found." });
            }

            return Ok(profile);
        }

        [HttpPut("me")]
        [SwaggerOperation(
            Summary = "Update my profile",
            Description =
                "Updates first name, last name and phone number of the " +
                "logged-in user. The user id comes from the session, so " +
                "nobody can edit someone else's profile.")]
        [SwaggerResponse(200, "The updated profile.", typeof(ProfileResponseDto))]
        [SwaggerResponse(400, "Invalid details.")]
        [SwaggerResponse(401, "Not logged in.")]
        [SwaggerResponse(404, "Profile not found.")]
        public async Task<IActionResult> UpdateMyProfile(
            [FromBody] UpdateProfileRequestDto request)
        {
            var denied = CheckAccess(null, string.Empty);
            if (denied != null) return denied;

            int userId = CurrentUserId!.Value;

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.UserID == userId);

            if (user == null)
            {
                return NotFound(new { message = "User profile not found." });
            }

            user.FirstName = request.FirstName.Trim();
            user.LastName = request.LastName.Trim();
            user.PhoneNumber = request.PhoneNumber?.Trim();

            await _context.SaveChangesAsync();

            return Ok(new ProfileResponseDto
            {
                UserId = user.UserID,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                Role = user.Role
            });
        }
    }
}
