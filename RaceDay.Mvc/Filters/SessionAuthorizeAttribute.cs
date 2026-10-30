using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using RaceDay.Mvc.Services;

namespace RaceDay.Mvc.Filters;

// Put [SessionAuthorize] on a controller or action to protect it.
//   [SessionAuthorize]                      -> any logged-in user
//   [SessionAuthorize(UserRoles.Organiser)] -> Organisers only
// Not logged in -> redirected to Login.
// Wrong role    -> redirected to the Access Denied page.
public class SessionAuthorizeAttribute : ActionFilterAttribute
{
    private readonly string? _requiredRole;

    public SessionAuthorizeAttribute(string? requiredRole = null)
    {
        _requiredRole = requiredRole;
    }

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var session = context.HttpContext.Session;

        if (session.GetInt32(SessionKeys.UserId) == null)
        {
            if (context.Controller is Controller controller)
            {
                controller.TempData["Error"] = "Please log in to view that page.";
            }

            context.Result = new RedirectToActionResult("Login", "Auth", null);
            return;
        }

        if (_requiredRole != null &&
            !string.Equals(session.GetString(SessionKeys.Role),
                           _requiredRole,
                           StringComparison.OrdinalIgnoreCase))
        {
            context.Result = new RedirectToActionResult("AccessDenied", "Home", null);
        }
    }
}