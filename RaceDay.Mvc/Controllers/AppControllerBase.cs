using Microsoft.AspNetCore.Mvc;
using RaceDay.Mvc.Services;

namespace RaceDay.Mvc.Controllers;

// Shared helpers for the Organiser and Participant controllers.
public abstract class AppControllerBase : Controller
{
    // The logged-in user's id from the MVC session (0 if nobody).
    protected int CurrentUserId =>
        HttpContext.Session.GetInt32(SessionKeys.UserId) ?? 0;

    // If the API says "401 not logged in", our session is out of date
    // (for example it timed out). Clear it and send the user to Login.
    // Returns null when everything is fine, so callers can write:
    //     var expired = HandleExpiredSession(result);
    //     if (expired != null) return expired;
    protected IActionResult? HandleExpiredSession(ApiResult result)
    {
        if (!result.IsUnauthorized) return null;

        HttpContext.Session.Clear();
        TempData["Error"] = "Your session has expired. Please log in again.";
        return RedirectToAction("Login", "Auth");
    }
}