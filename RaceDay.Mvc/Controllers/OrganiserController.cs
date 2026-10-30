using Microsoft.AspNetCore.Mvc;
using RaceDay.Contracts.DTOs.Enrolments;
using RaceDay.Contracts.DTOs.Events;
using RaceDay.Mvc.Filters;
using RaceDay.Mvc.Services;
using RaceDay.Mvc.ViewModels;

namespace RaceDay.Mvc.Controllers;

// Every action in this controller needs a logged-in Organiser.
[SessionAuthorize(UserRoles.Organiser)]
public class OrganiserController : AppControllerBase
{
    private readonly RaceDayApiClient _api;

    public OrganiserController(RaceDayApiClient api)
    {
        _api = api;
    }

    // ================= DASHBOARD =================

    public async Task<IActionResult> Index()
    {
        // 1. Get the events this Organiser created.
        var eventsResult = await _api.GetAsync<List<EventResponseDto>>("api/events/mine");

        var expired = HandleExpiredSession(eventsResult);
        if (expired != null) return expired;

        var vm = new OrganiserDashboardVm();

        if (!eventsResult.Success)
        {
            TempData["Error"] = eventsResult.Error;
            return View(vm);
        }

        // 2. For each event, ask the API how many people enrolled.
        foreach (var e in eventsResult.Data ?? new List<EventResponseDto>())
        {
            var enrolments = await _api.GetAsync<List<EnrolmentResponseDto>>(
                $"api/events/{e.EventId}/enrolments");

            vm.Events.Add(new OrganiserEventSummaryVm
            {
                EventId = e.EventId,
                EventName = e.EventName,
                EventDate = e.EventDate,
                Location = e.Location,
                EventTypeName = e.EventTypeName,
                EnrolmentCount = enrolments.Data?.Count ?? 0
            });
        }

        // Upcoming events first, then completed ones.
        vm.Events = vm.Events
            .OrderByDescending(e => e.IsUpcoming)
            .ThenBy(e => e.EventDate)
            .ToList();

        return View(vm);
    }

    // ================= EVENTS LIST =================

    public async Task<IActionResult> Events()
    {
        var result = await _api.GetAsync<List<EventResponseDto>>("api/events/mine");

        var expired = HandleExpiredSession(result);
        if (expired != null) return expired;

        if (!result.Success) TempData["Error"] = result.Error;

        return View(result.Data ?? new List<EventResponseDto>());
    }

    // ================= CREATE EVENT =================

    [HttpGet]
    public IActionResult CreateEvent()
    {
        // Pre-fill a sensible default: an event a month from now.
        return View(new EventFormVm
        {
            EventDate = DateTime.Today.AddDays(30),
            EventTypeId = 1
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateEvent(EventFormVm vm)
    {
        if (!ModelState.IsValid) return View(vm);

        var request = new CreateEventRequestDto
        {
            EventName = vm.EventName,
            Description = vm.Description,
            EventDate = vm.EventDate,
            Location = vm.Location,
            Distance = vm.Distance,
            EventTypeId = vm.EventTypeId
        };

        var result = await _api.PostAsync("api/events", request);

        var expired = HandleExpiredSession(result);
        if (expired != null) return expired;

        if (!result.Success)
        {
            // e.g. "The event date must be in the future..."
            ModelState.AddModelError(string.Empty,
                result.Error ?? "The event could not be created.");
            return View(vm);
        }

        TempData["Success"] = "Event created successfully.";
        return RedirectToAction(nameof(Events));
    }

    // ================= EDIT EVENT =================

    [HttpGet]
    public async Task<IActionResult> EditEvent(int id)
    {
        var result = await _api.GetAsync<EventResponseDto>($"api/events/{id}");

        var expired = HandleExpiredSession(result);
        if (expired != null) return expired;

        if (!result.Success || result.Data == null)
        {
            TempData["Error"] = result.Error ?? "Event not found.";
            return RedirectToAction(nameof(Events));
        }

        // Organisers may only edit their own events.
        if (result.Data.OrganiserId != CurrentUserId)
        {
            TempData["Error"] = "You can only edit your own events.";
            return RedirectToAction(nameof(Events));
        }

        return View(new EventFormVm
        {
            EventId = result.Data.EventId,
            EventName = result.Data.EventName,
            Description = result.Data.Description,
            EventDate = result.Data.EventDate,
            Location = result.Data.Location,
            Distance = result.Data.Distance,
            EventTypeId = result.Data.EventTypeId
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditEvent(EventFormVm vm)
    {
        if (!ModelState.IsValid) return View(vm);

        var request = new UpdateEventRequestDto
        {
            EventName = vm.EventName,
            Description = vm.Description,
            EventDate = vm.EventDate,
            Location = vm.Location,
            Distance = vm.Distance,
            EventTypeId = vm.EventTypeId
        };

        var result = await _api.PutAsync($"api/events/{vm.EventId}", request);

        var expired = HandleExpiredSession(result);
        if (expired != null) return expired;

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty,
                result.Error ?? "The event could not be updated.");
            return View(vm);
        }

        TempData["Success"] = "Event updated successfully.";
        return RedirectToAction(nameof(Events));
    }

    // ================= DELETE EVENT =================

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteEvent(int id)
    {
        var result = await _api.DeleteAsync($"api/events/{id}");

        var expired = HandleExpiredSession(result);
        if (expired != null) return expired;

        if (result.Success)
            TempData["Success"] = "Event deleted.";
        else
            // e.g. "This event already has enrolments and cannot be deleted."
            TempData["Error"] = result.Error ?? "The event could not be deleted.";

        return RedirectToAction(nameof(Events));
    }
}