using Microsoft.AspNetCore.Mvc;
using RaceDay.Contracts.DTOs.Auth;
using RaceDay.Mvc.Services;
using RaceDay.Mvc.ViewModels;

namespace RaceDay.Mvc.Controllers;

public class AuthController : Controller
{
    private readonly RaceDayApiClient _api;

    public AuthController(RaceDayApiClient api)
    {
        _api = api;
    }

    // ---------------- Register ----------------

    [HttpGet]
    public IActionResult Register()
    {
        if (IsLoggedIn()) return RedirectToDashboard();
        return View(new RegisterVm());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterVm vm)
    {
        // Organisers must supply the invite code.
        if (vm.Role == UserRoles.Organiser &&
            string.IsNullOrWhiteSpace(vm.OrganiserCode))
        {
            ModelState.AddModelError(nameof(vm.OrganiserCode),
                "The invite code is required to register as an Organiser.");
        }

        if (!ModelState.IsValid) return View(vm);

        var request = new RegisterRequestDto
        {
            FirstName = vm.FirstName,
            LastName = vm.LastName,
            Email = vm.Email,
            Password = vm.Password,
            PhoneNumber = vm.PhoneNumber,
            Role = vm.Role,
            OrganiserCode = vm.Role == UserRoles.Organiser
                ? vm.OrganiserCode
                : null
        };

        var result = await _api.PostAsync("api/auth/register", request);

        if (!result.Success)
        {
            // Friendly message from the API (e.g. "email already exists").
            ModelState.AddModelError(string.Empty,
                result.Error ?? "Registration failed. Please try again.");
            return View(vm);
        }

        TempData["Success"] = "Registration successful. Please log in.";
        return RedirectToAction(nameof(Login));
    }

    // ---------------- Login ----------------

    [HttpGet]
    public IActionResult Login()
    {
        if (IsLoggedIn()) return RedirectToDashboard();
        return View(new LoginVm());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginVm vm)
    {
        if (!ModelState.IsValid) return View(vm);

        // Start from a clean MVC session BEFORE calling the API.
        // If an old API cookie were sent, the API would reuse that
        // session and NOT issue a new cookie, and we would lose track
        // of it.
        HttpContext.Session.Clear();

        var result = await _api.PostAsync<AuthResponseDto>(
            "api/auth/login",
            new LoginRequestDto { Email = vm.Email, Password = vm.Password });

        if (!result.Success || result.Data == null)
        {
            ModelState.AddModelError(string.Empty,
                result.StatusCode == 401
                    ? "Invalid email or password."
                    : result.Error ?? "Login failed. Please try again.");
            return View(vm);
        }

        if (string.IsNullOrWhiteSpace(result.SessionCookie))
        {
            ModelState.AddModelError(string.Empty,
                "Login worked but a session could not be started. " +
                "Please try again.");
            return View(vm);
        }

        // Remember the API cookie (for later API calls) and who the
        // user is (for menus and page protection).
        HttpContext.Session.SetString(SessionKeys.ApiCookie, result.SessionCookie);
        HttpContext.Session.SetInt32(SessionKeys.UserId, result.Data.UserId);
        HttpContext.Session.SetString(SessionKeys.Role, result.Data.Role);
        HttpContext.Session.SetString(SessionKeys.DisplayName,
            string.IsNullOrWhiteSpace(result.Data.FullName)
                ? result.Data.Email
                : result.Data.FullName);

        return RedirectToDashboard();
    }

    // ---------------- Logout ----------------

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        // End the API session first (needs the cookie), then ours.
        await _api.PostAsync("api/auth/logout");
        HttpContext.Session.Clear();

        TempData["Success"] = "You have been logged out.";
        return RedirectToAction("Index", "Home");
    }

    // ---------------- Helpers ----------------

    private bool IsLoggedIn()
        => HttpContext.Session.GetInt32(SessionKeys.UserId).HasValue;

    private IActionResult RedirectToDashboard()
    {
        string? role = HttpContext.Session.GetString(SessionKeys.Role)
                       ?? string.Empty;
        // Used straight after login too, so read the role we just stored.
        return string.Equals(role, UserRoles.Organiser,
                StringComparison.OrdinalIgnoreCase)
            ? RedirectToAction("Index", "Organiser")
            : RedirectToAction("Index", "Participant");
    }
}