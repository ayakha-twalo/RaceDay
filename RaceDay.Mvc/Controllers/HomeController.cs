using Microsoft.AspNetCore.Mvc;

namespace RaceDay.Mvc.Controllers;

public class HomeController : Controller
{
    public IActionResult Index() => View();

    // Shown when a logged-in user opens a page for the other role.
    public IActionResult AccessDenied() => View();

    // Friendly error page (see UseExceptionHandler in Program.cs).
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View();
}