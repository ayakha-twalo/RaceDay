using Microsoft.AspNetCore.Mvc;

namespace RaceDay.Api.Controllers
{
    /// <summary>
    /// Shared session and role checks used by the RaceDay controllers.
    /// The logged-in user's id and role are stored in the server-side
    /// session when they log in.
    /// </summary>
    public abstract class RaceDayControllerBase : ControllerBase
    {
        protected const string OrganiserRole = "Organiser";
        protected const string ParticipantRole = "Participant";

        protected int? CurrentUserId => HttpContext.Session.GetInt32("UserId");

        protected string? CurrentRole => HttpContext.Session.GetString("Role");

        /// <summary>
        /// Returns null when the caller may continue. Otherwise returns the
        /// response to send back: 401 if nobody is logged in, or 403 if the
        /// logged-in user does not have the required role.
        /// Pass null as requiredRole when any logged-in user is allowed.
        /// </summary>
        protected IActionResult? CheckAccess(
            string? requiredRole,
            string forbiddenMessage)
        {
            if (CurrentUserId == null || CurrentRole == null)
            {
                return Unauthorized(new { message = "Please log in first." });
            }

            if (requiredRole != null && CurrentRole != requiredRole)
            {
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    new { message = forbiddenMessage });
            }

            return null;
        }
    }
}
