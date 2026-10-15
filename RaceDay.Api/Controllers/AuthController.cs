using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Models;
using RaceDay.Contracts.DTOs.Auth;
using Swashbuckle.AspNetCore.Annotations;

namespace RaceDay.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly RaceDayDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly PasswordHasher<User> _passwordHasher;

        public AuthController(
            RaceDayDbContext context,
            IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
            _passwordHasher = new PasswordHasher<User>();
        }

        [HttpPost("register")]
        [SwaggerOperation(
            Summary = "Register a new account",
            Description =
                "Creates a Participant or Organiser account. The password " +
                "is hashed before it is stored. Registering as an Organiser " +
                "also needs the organiser invite code (OrganiserCode).")]
        [SwaggerResponse(201, "Account created.", typeof(AuthResponseDto))]
        [SwaggerResponse(400, "Invalid details or invalid role.")]
        [SwaggerResponse(403, "Organiser invite code missing or incorrect.")]
        [SwaggerResponse(409, "An account with this email already exists.")]
        public async Task<IActionResult> Register(
            [FromBody] RegisterRequestDto request)
        {
            // 1. Confirm the selected role is valid.
            string? requestedRole = request.Role?.Trim();
            string role;

            if (string.Equals(requestedRole, ParticipantRole,
                    StringComparison.OrdinalIgnoreCase))
            {
                role = ParticipantRole;
            }
            else if (string.Equals(requestedRole, OrganiserRole,
                         StringComparison.OrdinalIgnoreCase))
            {
                role = OrganiserRole;
            }
            else
            {
                return BadRequest(new
                {
                    message = "Role must be either 'Organiser' or 'Participant'."
                });
            }

            // 2. Organisers need the invite code, so nobody can simply
            //    give themselves Organiser access.
            if (role == OrganiserRole)
            {
                string? expectedCode =
                    _configuration["Registration:OrganiserCode"];

                if (string.IsNullOrWhiteSpace(expectedCode) ||
                    request.OrganiserCode != expectedCode)
                {
                    return StatusCode(StatusCodes.Status403Forbidden, new
                    {
                        message =
                            "A valid organiser invite code is required " +
                            "to register as an Organiser."
                    });
                }
            }

            // 3. Check whether the email address already exists.
            string email = request.Email.Trim().ToLowerInvariant();

            bool emailExists = await _context.Users
                .AnyAsync(user => user.Email == email);

            if (emailExists)
            {
                return Conflict(new
                {
                    message = "An account with this email already exists."
                });
            }

            // 4. Hash the password and save the user.
            var user = new User
            {
                FirstName = request.FirstName.Trim(),
                LastName = request.LastName.Trim(),
                Email = email,
                PhoneNumber = request.PhoneNumber?.Trim(),
                Role = role
            };

            user.PasswordHash = _passwordHasher.HashPassword(
                user,
                request.Password);

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return Created("/api/users/me", ToAuthResponse(
                user,
                "Registration successful."));
        }

        [HttpPost("login")]
        [SwaggerOperation(
            Summary = "Log in",
            Description =
                "Checks the email and password. On success the user's id " +
                "and role are stored in the server-side session, which " +
                "protects every other endpoint.")]
        [SwaggerResponse(200, "Logged in.", typeof(AuthResponseDto))]
        [SwaggerResponse(401, "Invalid email or password.")]
        public async Task<IActionResult> Login(
            [FromBody] LoginRequestDto request)
        {
            string email = request.Email.Trim().ToLowerInvariant();

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == email);

            if (user == null)
            {
                return Unauthorized(new
                {
                    message = "Invalid email or password."
                });
            }

            var verificationResult = _passwordHasher.VerifyHashedPassword(
                user,
                user.PasswordHash,
                request.Password);

            if (verificationResult == PasswordVerificationResult.Failed)
            {
                return Unauthorized(new
                {
                    message = "Invalid email or password."
                });
            }

            // Store the user's identity and role in the session.
            HttpContext.Session.SetInt32("UserId", user.UserID);
            HttpContext.Session.SetString("Role", user.Role);

            return Ok(ToAuthResponse(user, "Login successful."));
        }

        [HttpPost("logout")]
        [SwaggerOperation(
            Summary = "Log out",
            Description = "Clears the session so protected endpoints can no longer be used.")]
        [SwaggerResponse(200, "Logged out.")]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();

            return Ok(new { message = "Logout successful." });
        }

        private const string ParticipantRole = "Participant";
        private const string OrganiserRole = "Organiser";

        private static AuthResponseDto ToAuthResponse(
            User user,
            string message)
        {
            return new AuthResponseDto
            {
                UserId = user.UserID,
                FullName = $"{user.FirstName} {user.LastName}",
                Email = user.Email,
                Role = user.Role,
                Message = message
            };
        }
    }
}
