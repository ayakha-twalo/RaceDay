using Microsoft.AspNetCore.Mvc;
using RaceDay.Mvc.Filters;
using RaceDay.Mvc.Services;

namespace RaceDay.Mvc.Controllers;

// Class-level filter: every action here needs a logged-in Organiser.
[SessionAuthorize(UserRoles.Organiser)]
public class OrganiserController : Controller
{
    public IActionResult Index() => View();
}