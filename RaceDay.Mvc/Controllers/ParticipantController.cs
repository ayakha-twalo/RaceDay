using Microsoft.AspNetCore.Mvc;
using RaceDay.Mvc.Filters;
using RaceDay.Mvc.Services;

namespace RaceDay.Mvc.Controllers;

// Class-level filter: every action here needs a logged-in Participant.
[SessionAuthorize(UserRoles.Participant)]
public class ParticipantController : Controller
{
    public IActionResult Index() => View();
}