using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Services;
using RaceDay.Contracts.DTOs.Images;
using RaceDay.Contracts.DTOs.Profile;
using Swashbuckle.AspNetCore.Annotations;

namespace RaceDay.Api.Controllers
{
    // This controller handles the profile of the currently logged-in user.
    [ApiController]
    [Route("api/users")]
    public class ProfileController : RaceDayControllerBase
    {
        private readonly RaceDayDbContext _context;

    public ProfileController(RaceDayDbContext context)
        {
            _context = context;
        }

        // GET: api/users/me
        // Gets the profile details of the logged-in user.
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
                    Role = u.Role,

                    // Include the profile picture URL.
                    ProfileImageUrl = u.ProfileImageUrl
                })
                .FirstOrDefaultAsync();

            if (profile == null)
            {
                return NotFound(new { message = "User profile not found." });
            }

            return Ok(profile);
        }

        // PUT: api/users/me
        // Updates the details of the currently logged-in user.
        [HttpPut("me")]
        [SwaggerOperation(
            Summary = "Update my profile",
            Description =
                "Updates first name, last name and phone number of the " +
                "logged-in user. The user id comes from the session.")]
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
                Role = user.Role,

                // Keep the existing profile picture URL.
                ProfileImageUrl = user.ProfileImageUrl
            });
        }

        // POST: api/users/me/photo
        // Uploads a profile picture for the currently logged-in user.
        [HttpPost("me/photo")]
        [Consumes("multipart/form-data")]
        [SwaggerOperation(
            Summary = "Upload my profile picture",
            Description =
                "Uploads a JPG, PNG, GIF or WEBP image (maximum 5 MB) " +
                "to Azure Blob Storage and saves its URL to the user's profile.")]
        [SwaggerResponse(200, "Picture uploaded.", typeof(ImageUploadResponseDto))]
        [SwaggerResponse(400, "Invalid image file.")]
        [SwaggerResponse(401, "Not logged in.")]
        [SwaggerResponse(404, "Profile not found.")]
        [SwaggerResponse(500, "The image could not be stored.")]
        public async Task<IActionResult> UploadMyPhoto(
            [FromForm] IFormFile? file,
            [FromServices] BlobStorageService blobStorage)
        {
            // Make sure the user is logged in.
            var denied = CheckAccess(null, string.Empty);
            if (denied != null) return denied;

            int userId = CurrentUserId!.Value;

            // Find the currently logged-in user's account.
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.UserID == userId);

            if (user == null)
            {
                return NotFound(new { message = "User profile not found." });
            }

            // Check that the selected file is an acceptable image.
            string? problem = blobStorage.Validate(file);

            if (problem != null)
            {
                return BadRequest(new { message = problem });
            }

            try
            {
                // Upload the image to the profile-pictures container.
                string imageUrl = await blobStorage.UploadAsync(
                    file!,
                    "profile-pictures");

                // Save the returned Azure image URL to the user's record.
                user.ProfileImageUrl = imageUrl;

                await _context.SaveChangesAsync();

                return Ok(new ImageUploadResponseDto
                {
                    ImageUrl = imageUrl
                });
            }
            catch (Exception)
            {
                // Return a friendly message if the upload fails.
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new
                    {
                        message = "The profile picture could not be uploaded. Please try again."
                    });
            }
        }
    }

}
