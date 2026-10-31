using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Contracts.DTOs.Categories;
using RaceDay.Contracts.DTOs.Enrolments;
using RaceDay.Contracts.DTOs.Events;
using RaceDay.Contracts.DTOs.Results;
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

    // =====================================================
    // STAGE 2B: CATEGORIES
    // =====================================================

    public async Task<IActionResult> Categories(int? eventId)
    {
        var vm = new CategoriesPageVm();

        var redirect = await FillEventPickerAsync(vm, eventId);
        if (redirect != null) return redirect;

        if (vm.SelectedEventId != null)
        {
            var result = await _api.GetAsync<List<CategoryResponseDto>>(
                $"api/events/{vm.SelectedEventId}/categories");

            var expired = HandleExpiredSession(result);
            if (expired != null) return expired;

            if (!result.Success) TempData["Error"] = result.Error;
            vm.Categories = result.Data ?? new List<CategoryResponseDto>();
        }

        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddCategory(CategoryFormVm form)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = FirstModelError() ?? "Please check the category details.";
            return RedirectToAction(nameof(Categories), new { eventId = form.EventId });
        }

        var result = await _api.PostAsync(
            $"api/events/{form.EventId}/categories",
            new CreateCategoryRequestDto
            {
                CategoryName = form.CategoryName,
                Description = form.Description
            });

        var expired = HandleExpiredSession(result);
        if (expired != null) return expired;

        if (result.Success)
            TempData["Success"] = "Category added.";
        else
            // e.g. "This event already has a category with that name."
            TempData["Error"] = result.Error ?? "The category could not be added.";

        return RedirectToAction(nameof(Categories), new { eventId = form.EventId });
    }

    [HttpGet]
    public async Task<IActionResult> EditCategory(int id, int eventId)
    {
        // The API has no "get one category" endpoint, so fetch the
        // event's categories and pick the one we need.
        var result = await _api.GetAsync<List<CategoryResponseDto>>(
            $"api/events/{eventId}/categories");

        var expired = HandleExpiredSession(result);
        if (expired != null) return expired;

        var category = result.Data?.FirstOrDefault(c => c.CategoryId == id);

        if (!result.Success || category == null)
        {
            TempData["Error"] = result.Error ?? "Category not found.";
            return RedirectToAction(nameof(Categories), new { eventId });
        }

        return View(new CategoryFormVm
        {
            CategoryId = category.CategoryId,
            EventId = category.EventId,
            CategoryName = category.CategoryName,
            Description = category.Description
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditCategory(CategoryFormVm form)
    {
        if (!ModelState.IsValid) return View(form);

        var result = await _api.PutAsync(
            $"api/categories/{form.CategoryId}",
            new CreateCategoryRequestDto
            {
                CategoryName = form.CategoryName,
                Description = form.Description
            });

        var expired = HandleExpiredSession(result);
        if (expired != null) return expired;

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty,
                result.Error ?? "The category could not be updated.");
            return View(form);
        }

        TempData["Success"] = "Category updated.";
        return RedirectToAction(nameof(Categories), new { eventId = form.EventId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCategory(int id, int eventId)
    {
        var result = await _api.DeleteAsync($"api/categories/{id}");

        var expired = HandleExpiredSession(result);
        if (expired != null) return expired;

        if (result.Success)
            TempData["Success"] = "Category deleted.";
        else
            // e.g. "Cannot delete a category that has enrolments."
            TempData["Error"] = result.Error ?? "The category could not be deleted.";

        return RedirectToAction(nameof(Categories), new { eventId });
    }

    // =====================================================
    // STAGE 2B: ENROLMENTS
    // =====================================================

    public async Task<IActionResult> Enrolments(int? eventId)
    {
        var vm = new EnrolmentsPageVm();

        var redirect = await FillEventPickerAsync(vm, eventId);
        if (redirect != null) return redirect;

        if (vm.SelectedEventId != null)
        {
            var result = await _api.GetAsync<List<EnrolmentResponseDto>>(
                $"api/events/{vm.SelectedEventId}/enrolments");

            var expired = HandleExpiredSession(result);
            if (expired != null) return expired;

            if (!result.Success) TempData["Error"] = result.Error;

            vm.Enrolments = (result.Data ?? new List<EnrolmentResponseDto>())
                .OrderBy(e => e.ParticipantName)
                .ToList();
        }

        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateEnrolmentStatus(int id, int eventId, string status)
    {
        // Only accept the three statuses that exist in the database.
        string[] allowed = { "Pending", "Confirmed", "Cancelled" };

        if (!allowed.Contains(status))
        {
            TempData["Error"] = "That is not a valid enrolment status.";
            return RedirectToAction(nameof(Enrolments), new { eventId });
        }

        var result = await _api.PutAsync(
            $"api/enrolments/{id}/status",
            new UpdateEnrolmentStatusRequestDto { StatusName = status });

        var expired = HandleExpiredSession(result);
        if (expired != null) return expired;

        if (result.Success)
            TempData["Success"] = $"Enrolment marked as {status}.";
        else
            TempData["Error"] = result.Error ?? "The status could not be changed.";

        return RedirectToAction(nameof(Enrolments), new { eventId });
    }

    // =====================================================
    // STAGE 2B: RESULTS
    // =====================================================

    public async Task<IActionResult> Results(int? eventId)
    {
        var vm = new ResultsPageVm();

        var redirect = await FillEventPickerAsync(vm, eventId);
        if (redirect != null) return redirect;

        if (vm.SelectedEventId != null)
        {
            // Two API calls: who is enrolled, and which results exist.
            var enrolments = await _api.GetAsync<List<EnrolmentResponseDto>>(
                $"api/events/{vm.SelectedEventId}/enrolments");

            var expired = HandleExpiredSession(enrolments);
            if (expired != null) return expired;

            var results = await _api.GetAsync<List<ResultResponseDto>>(
                $"api/results/event/{vm.SelectedEventId}");

            if (!enrolments.Success) TempData["Error"] = enrolments.Error;
            else if (!results.Success) TempData["Error"] = results.Error;

            // Match each result to its enrolment (one result per enrolment).
            var resultByEnrolment = (results.Data ?? new List<ResultResponseDto>())
                .ToDictionary(r => r.EnrolmentId);

            vm.Rows = (enrolments.Data ?? new List<EnrolmentResponseDto>())
                .Where(e => e.Status != "Cancelled")   // cancelled entries cannot finish
                .Select(e =>
                {
                    resultByEnrolment.TryGetValue(e.EnrolmentId, out var r);

                    return new ResultRowVm
                    {
                        EnrolmentId = e.EnrolmentId,
                        ParticipantName = e.ParticipantName,
                        CategoryName = e.CategoryName,
                        FinishTime = r?.FinishTime,
                        FinishPosition = r?.FinishPosition
                    };
                })
                // Finished runners by position, then those still waiting.
                .OrderBy(r => r.FinishPosition ?? int.MaxValue)
                .ThenBy(r => r.ParticipantName)
                .ToList();
        }

        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CaptureResult(CaptureResultVm form)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = FirstModelError() ?? "Please check the result details.";
            return RedirectToAction(nameof(Results), new { eventId = form.EventId });
        }

        // Read "hh:mm:ss" exactly (so 45:30 is never read as 45 hours).
        if (!TimeSpan.TryParseExact(form.FinishTime, @"h\:mm\:ss",
                CultureInfo.InvariantCulture, out var finishTime) ||
            finishTime <= TimeSpan.Zero)
        {
            TempData["Error"] = "Enter a finish time above zero as hh:mm:ss, for example 00:45:30.";
            return RedirectToAction(nameof(Results), new { eventId = form.EventId });
        }

        var result = await _api.PostAsync(
            $"api/enrolments/{form.EnrolmentId}/result",
            new CreateResultRequestDto
            {
                FinishTime = finishTime,
                FinishPosition = form.FinishPosition
            });

        var expired = HandleExpiredSession(result);
        if (expired != null) return expired;

        if (result.Success)
            TempData["Success"] = "Result saved.";
        else
            // e.g. "A result has already been recorded for this enrolment."
            TempData["Error"] = result.Error ?? "The result could not be saved.";

        return RedirectToAction(nameof(Results), new { eventId = form.EventId });
    }

    // =====================================================
    // Helpers used by the Stage 2B pages
    // =====================================================

    // Loads this Organiser's events into the drop-down and decides which
    // one is selected (the chosen one, or the first if none chosen yet).
    // Returns null when all is well, or a redirect if the session expired.
    private async Task<IActionResult?> FillEventPickerAsync(EventPickerVm vm, int? eventId)
    {
        var result = await _api.GetAsync<List<EventResponseDto>>("api/events/mine");

        var expired = HandleExpiredSession(result);
        if (expired != null) return expired;

        if (!result.Success)
        {
            TempData["Error"] = result.Error;
            return null;
        }

        vm.MyEvents = result.Data ?? new List<EventResponseDto>();

        var selected = vm.MyEvents.FirstOrDefault(e => e.EventId == eventId)
                       ?? vm.MyEvents.FirstOrDefault();

        vm.SelectedEventId = selected?.EventId;
        vm.SelectedEventName = selected?.EventName;
        return null;
    }

    // The first validation message from a failed form, as plain text.
    private string? FirstModelError()
    {
        return ModelState.Values
            .SelectMany(v => v.Errors)
            .Select(e => e.ErrorMessage)
            .FirstOrDefault(m => !string.IsNullOrWhiteSpace(m));
    }
}