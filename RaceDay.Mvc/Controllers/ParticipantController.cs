using Microsoft.AspNetCore.Mvc;
using RaceDay.Contracts.DTOs.Categories;
using RaceDay.Contracts.DTOs.Enrolments;
using RaceDay.Contracts.DTOs.Events;
using RaceDay.Contracts.DTOs.Profile;
using RaceDay.Contracts.DTOs.Results;
using RaceDay.Mvc.Filters;
using RaceDay.Mvc.Services;
using RaceDay.Mvc.ViewModels;

namespace RaceDay.Mvc.Controllers;

// Every action in this controller needs a logged-in Participant.
[SessionAuthorize(UserRoles.Participant)]
public class ParticipantController : AppControllerBase
{
    private readonly RaceDayApiClient _api;

    public ParticipantController(RaceDayApiClient api)
    {
        _api = api;
    }

    // =====================================================
    // UPCOMING EVENTS (with filters)
    // =====================================================

    public async Task<IActionResult> Index(string? type, string? search)
    {
        var vm = new EventBrowseVm { Type = type, Search = search };

        var result = await _api.GetAsync<List<EventResponseDto>>("api/events");

        var expired = HandleExpiredSession(result);
        if (expired != null) return expired;

        if (!result.Success) TempData["Error"] = result.Error;

        // Only events that have not happened yet.
        var events = (result.Data ?? new List<EventResponseDto>())
            .Where(e => e.EventDate.Date >= DateTime.Today);

        // Filter 1: event type (Run, Walk or Cycle).
        if (!string.IsNullOrWhiteSpace(type))
        {
            events = events.Where(e => string.Equals(
                e.EventTypeName, type, StringComparison.OrdinalIgnoreCase));
        }

        // Filter 2: text search in the event name or location.
        if (!string.IsNullOrWhiteSpace(search))
        {
            events = events.Where(e =>
                e.EventName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                e.Location.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        vm.Events = events.OrderBy(e => e.EventDate).ToList();
        return View(vm);
    }

    // =====================================================
    // EVENT DETAILS + ENTER EVENT
    // =====================================================

    public async Task<IActionResult> Details(int id)
    {
        var eventResult = await _api.GetAsync<EventResponseDto>($"api/events/{id}");

        var expired = HandleExpiredSession(eventResult);
        if (expired != null) return expired;

        if (!eventResult.Success || eventResult.Data == null)
        {
            TempData["Error"] = eventResult.Error ?? "Event not found.";
            return RedirectToAction(nameof(Index));
        }

        var categories = await _api.GetAsync<List<CategoryResponseDto>>(
            $"api/events/{id}/categories");

        // My enrolments tell us whether I have already entered this event.
        var mine = await _api.GetAsync<List<EnrolmentResponseDto>>("api/enrolments/me");

        expired = HandleExpiredSession(mine);
        if (expired != null) return expired;

        return View(new EventDetailsVm
        {
            Event = eventResult.Data,
            Categories = categories.Data ?? new List<CategoryResponseDto>(),
            MyEnrolment = mine.Data?.FirstOrDefault(e => e.EventId == id)
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EnterEvent(int eventId, int categoryId)
    {
        if (categoryId <= 0)
        {
            TempData["Error"] = "Please choose a category before entering.";
            return RedirectToAction(nameof(Details), new { id = eventId });
        }

        var result = await _api.PostAsync("api/enrolments",
            new CreateEnrolmentRequestDto
            {
                EventId = eventId,
                CategoryId = categoryId
            });

        var expired = HandleExpiredSession(result);
        if (expired != null) return expired;

        if (!result.Success)
        {
            // e.g. "You are already enrolled in this event."
            TempData["Error"] = result.Error ?? "You could not be entered. Please try again.";
            return RedirectToAction(nameof(Details), new { id = eventId });
        }

        TempData["Success"] = "You have entered the event. The organiser will confirm your entry.";
        return RedirectToAction(nameof(MyEvents));
    }

    // =====================================================
    // MY EVENTS
    // =====================================================

    public async Task<IActionResult> MyEvents()
    {
        var enrolments = await _api.GetAsync<List<EnrolmentResponseDto>>("api/enrolments/me");

        var expired = HandleExpiredSession(enrolments);
        if (expired != null) return expired;

        // The enrolment only knows the event's name, so we also fetch the
        // event list to show each event's date, location and distance.
        var events = await _api.GetAsync<List<EventResponseDto>>("api/events");

        if (!enrolments.Success) TempData["Error"] = enrolments.Error;

        var eventById = (events.Data ?? new List<EventResponseDto>())
            .ToDictionary(e => e.EventId);

        var rows = (enrolments.Data ?? new List<EnrolmentResponseDto>())
            .Select(en =>
            {
                eventById.TryGetValue(en.EventId, out var ev);

                return new MyEventRowVm
                {
                    EnrolmentId = en.EnrolmentId,
                    EventId = en.EventId,
                    EventName = en.EventName,
                    EventDate = ev?.EventDate,
                    Location = ev?.Location ?? string.Empty,
                    Distance = ev?.Distance,
                    EventTypeName = ev?.EventTypeName ?? string.Empty,
                    CategoryName = en.CategoryName,
                    Status = en.Status
                };
            })
            .OrderBy(r => r.EventDate ?? DateTime.MaxValue)   // soonest first
            .ToList();

        return View(rows);
    }

    // =====================================================
    // MY RESULTS
    // =====================================================

    public async Task<IActionResult> MyResults()
    {
        var result = await _api.GetAsync<List<ResultResponseDto>>("api/results/me");

        var expired = HandleExpiredSession(result);
        if (expired != null) return expired;

        if (!result.Success) TempData["Error"] = result.Error;

        // Most recent event first.
        var results = (result.Data ?? new List<ResultResponseDto>())
            .OrderByDescending(r => r.EventDate)
            .ToList();

        return View(results);
    }

    // =====================================================
    // PROFILE
    // =====================================================

    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        var result = await _api.GetAsync<ProfileResponseDto>("api/users/me");

        var expired = HandleExpiredSession(result);
        if (expired != null) return expired;

        if (!result.Success || result.Data == null)
        {
            TempData["Error"] = result.Error ?? "Your profile could not be loaded.";
            return RedirectToAction(nameof(Index));
        }

        return View(new ProfileFormVm
        {
            FirstName = result.Data.FirstName,
            LastName = result.Data.LastName,
            PhoneNumber = result.Data.PhoneNumber,
            Email = result.Data.Email,
            Role = result.Data.Role
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(ProfileFormVm vm)
    {
        if (!ModelState.IsValid) return View(vm);

        // Only the three editable fields are sent to the API.
        var result = await _api.PutAsync<ProfileResponseDto>("api/users/me",
            new UpdateProfileRequestDto
            {
                FirstName = vm.FirstName,
                LastName = vm.LastName,
                PhoneNumber = vm.PhoneNumber
            });

        var expired = HandleExpiredSession(result);
        if (expired != null) return expired;

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty,
                result.Error ?? "Your profile could not be updated.");
            return View(vm);
        }

        // Keep the name shown in the navigation bar up to date.
        if (result.Data != null)
        {
            HttpContext.Session.SetString(SessionKeys.DisplayName,
                $"{result.Data.FirstName} {result.Data.LastName}");
        }

        TempData["Success"] = "Your profile has been updated.";
        return RedirectToAction(nameof(Profile));
    }
}