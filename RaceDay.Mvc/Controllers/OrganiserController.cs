using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Contracts.DTOs.Categories;
using RaceDay.Contracts.DTOs.Enrolments;
using RaceDay.Contracts.DTOs.Events;
using RaceDay.Contracts.DTOs.Images;
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
        var eventsResult = await _api.GetAsync<List<EventResponseDto>>(
            "api/events/mine");

        var expired = HandleExpiredSession(eventsResult);
        if (expired != null) return expired;

        var vm = new OrganiserDashboardVm();

        if (!eventsResult.Success)
        {
            TempData["Error"] = eventsResult.Error;
            return View(vm);
        }

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

        vm.Events = vm.Events
            .OrderByDescending(e => e.IsUpcoming)
            .ThenBy(e => e.EventDate)
            .ToList();

        return View(vm);
    }

    // ================= EVENTS LIST =================

    public async Task<IActionResult> Events()
    {
        var result = await _api.GetAsync<List<EventResponseDto>>(
            "api/events/mine");

        var expired = HandleExpiredSession(result);
        if (expired != null) return expired;

        if (!result.Success)
            TempData["Error"] = result.Error;

        return View(result.Data ?? new List<EventResponseDto>());
    }

    // ================= CREATE EVENT =================

    [HttpGet]
    public IActionResult CreateEvent()
    {
        return View(new EventFormVm
        {
            EventDate = DateTime.Today.AddDays(30),
            EventTypeId = 1
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateEvent(EventFormVm vm)
    {
        if (!ModelState.IsValid)
            return View(vm);

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
            ModelState.AddModelError(
                string.Empty,
                result.Error ?? "The event could not be created.");

            return View(vm);
        }

        TempData["Success"] = "Event created successfully.";
        return RedirectToAction(nameof(Events));
    }

    // ================= EDIT EVENT =================

    // Loads an event and its current banner information.
    [HttpGet]
    public async Task<IActionResult> EditEvent(int id)
    {
        var result = await _api.GetAsync<EventResponseDto>(
            $"api/events/{id}");

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
            EventTypeId = result.Data.EventTypeId,

            // Part 3: Include the existing event banner URL.
            BannerImageUrl = result.Data.BannerImageUrl
        });
    }

    // Saves changes to the event details.
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditEvent(EventFormVm vm)
    {
        if (!ModelState.IsValid)
            return View(vm);

        var request = new UpdateEventRequestDto
        {
            EventName = vm.EventName,
            Description = vm.Description,
            EventDate = vm.EventDate,
            Location = vm.Location,
            Distance = vm.Distance,
            EventTypeId = vm.EventTypeId
        };

        var result = await _api.PutAsync(
            $"api/events/{vm.EventId}",
            request);

        var expired = HandleExpiredSession(result);
        if (expired != null) return expired;

        if (!result.Success)
        {
            ModelState.AddModelError(
                string.Empty,
                result.Error ?? "The event could not be updated.");

            return View(vm);
        }

        TempData["Success"] = "Event updated successfully.";
        return RedirectToAction(nameof(Events));
    }

    // ================= EVENT BANNER UPLOAD =================

    // Sends the selected banner image to the Part 2 API.
    // The API handles the actual Azure Blob Storage upload.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadBanner(int id, IFormFile? file)
    {
        if (file == null || file.Length == 0)
        {
            TempData["Error"] = "Please choose an image before uploading.";
            return RedirectToAction(nameof(EditEvent), new { id });
        }

        // Maximum image size is 5 MB.
        if (file.Length > 5 * 1024 * 1024)
        {
            TempData["Error"] = "The image must be 5 MB or smaller.";
            return RedirectToAction(nameof(EditEvent), new { id });
        }

        // Check the file extension.
        string extension = Path.GetExtension(file.FileName)
            .ToLowerInvariant();

        string[] allowedExtensions =
        {
        ".jpg",
        ".jpeg",
        ".png",
        ".gif",
        ".webp"
    };

        if (!allowedExtensions.Contains(extension))
        {
            TempData["Error"] =
                "Only JPG, PNG, GIF or WEBP images are allowed.";

            return RedirectToAction(nameof(EditEvent), new { id });
        }

        // Send the file to the API.
        var result = await _api.PostFileAsync<ImageUploadResponseDto>(
            $"api/events/{id}/banner",
            file);

        var expired = HandleExpiredSession(result);
        if (expired != null) return expired;

        if (!result.Success)
        {
            TempData["Error"] = result.Error ??
                "The event banner could not be uploaded.";

            return RedirectToAction(nameof(EditEvent), new { id });
        }

        TempData["Success"] = "Event banner uploaded successfully.";

        return RedirectToAction(nameof(EditEvent), new { id });
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
            TempData["Error"] = result.Error ??
                "The event could not be deleted.";

        return RedirectToAction(nameof(Events));
    }

    // =====================================================
    // CATEGORIES
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

            if (!result.Success)
                TempData["Error"] = result.Error;

            vm.Categories = result.Data ??
                new List<CategoryResponseDto>();
        }

        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddCategory(CategoryFormVm form)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = FirstModelError() ??
                "Please check the category details.";

            return RedirectToAction(
                nameof(Categories),
                new { eventId = form.EventId });
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
            TempData["Error"] = result.Error ??
                "The category could not be added.";

        return RedirectToAction(
            nameof(Categories),
            new { eventId = form.EventId });
    }

    [HttpGet]
    public async Task<IActionResult> EditCategory(int id, int eventId)
    {
        var result = await _api.GetAsync<List<CategoryResponseDto>>(
            $"api/events/{eventId}/categories");

        var expired = HandleExpiredSession(result);
        if (expired != null) return expired;

        var category = result.Data?.FirstOrDefault(
            c => c.CategoryId == id);

        if (!result.Success || category == null)
        {
            TempData["Error"] = result.Error ?? "Category not found.";

            return RedirectToAction(
                nameof(Categories),
                new { eventId });
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
        if (!ModelState.IsValid)
            return View(form);

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
            ModelState.AddModelError(
                string.Empty,
                result.Error ?? "The category could not be updated.");

            return View(form);
        }

        TempData["Success"] = "Category updated.";

        return RedirectToAction(
            nameof(Categories),
            new { eventId = form.EventId });
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
            TempData["Error"] = result.Error ??
                "The category could not be deleted.";

        return RedirectToAction(
            nameof(Categories),
            new { eventId });
    }

    // =====================================================
    // ENROLMENTS
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

            if (!result.Success)
                TempData["Error"] = result.Error;

            vm.Enrolments = (
                result.Data ?? new List<EnrolmentResponseDto>())
                .OrderBy(e => e.ParticipantName)
                .ToList();
        }

        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateEnrolmentStatus(
        int id,
        int eventId,
        string status)
    {
        string[] allowed =
        {
        "Pending",
        "Confirmed",
        "Cancelled"
    };

        if (!allowed.Contains(status))
        {
            TempData["Error"] = "That is not a valid enrolment status.";

            return RedirectToAction(
                nameof(Enrolments),
                new { eventId });
        }

        var result = await _api.PutAsync(
            $"api/enrolments/{id}/status",
            new UpdateEnrolmentStatusRequestDto
            {
                StatusName = status
            });

        var expired = HandleExpiredSession(result);
        if (expired != null) return expired;

        if (result.Success)
            TempData["Success"] = $"Enrolment marked as {status}.";
        else
            TempData["Error"] = result.Error ??
                "The status could not be changed.";

        return RedirectToAction(
            nameof(Enrolments),
            new { eventId });
    }

    // =====================================================
    // RESULTS
    // =====================================================

    public async Task<IActionResult> Results(int? eventId)
    {
        var vm = new ResultsPageVm();

        var redirect = await FillEventPickerAsync(vm, eventId);
        if (redirect != null) return redirect;

        if (vm.SelectedEventId != null)
        {
            var enrolments = await _api.GetAsync<List<EnrolmentResponseDto>>(
                $"api/events/{vm.SelectedEventId}/enrolments");

            var expired = HandleExpiredSession(enrolments);
            if (expired != null) return expired;

            var results = await _api.GetAsync<List<ResultResponseDto>>(
                $"api/results/event/{vm.SelectedEventId}");

            if (!enrolments.Success)
                TempData["Error"] = enrolments.Error;
            else if (!results.Success)
                TempData["Error"] = results.Error;

            var resultByEnrolment =
                (results.Data ?? new List<ResultResponseDto>())
                .ToDictionary(r => r.EnrolmentId);

            vm.Rows = (
                enrolments.Data ?? new List<EnrolmentResponseDto>())
                .Where(e => e.Status != "Cancelled")
                .Select(e =>
                {
                    resultByEnrolment.TryGetValue(
                        e.EnrolmentId,
                        out var r);

                    return new ResultRowVm
                    {
                        EnrolmentId = e.EnrolmentId,
                        ParticipantName = e.ParticipantName,
                        CategoryName = e.CategoryName,
                        FinishTime = r?.FinishTime,
                        FinishPosition = r?.FinishPosition
                    };
                })
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
            TempData["Error"] = FirstModelError() ??
                "Please check the result details.";

            return RedirectToAction(
                nameof(Results),
                new { eventId = form.EventId });
        }

        if (!TimeSpan.TryParseExact(
                form.FinishTime,
                @"h\:mm\:ss",
                CultureInfo.InvariantCulture,
                out var finishTime) ||
            finishTime <= TimeSpan.Zero)
        {
            TempData["Error"] =
                "Enter a finish time above zero as hh:mm:ss, for example 00:45:30.";

            return RedirectToAction(
                nameof(Results),
                new { eventId = form.EventId });
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
            TempData["Error"] = result.Error ??
                "The result could not be saved.";

        return RedirectToAction(
            nameof(Results),
            new { eventId = form.EventId });
    }

    // =====================================================
    // HELPER METHODS
    // =====================================================

    private async Task<IActionResult?> FillEventPickerAsync(
        EventPickerVm vm,
        int? eventId)
    {
        var result = await _api.GetAsync<List<EventResponseDto>>(
            "api/events/mine");

        var expired = HandleExpiredSession(result);
        if (expired != null) return expired;

        if (!result.Success)
        {
            TempData["Error"] = result.Error;
            return null;
        }

        vm.MyEvents = result.Data ?? new List<EventResponseDto>();

        var selected =
            vm.MyEvents.FirstOrDefault(e => e.EventId == eventId)
            ?? vm.MyEvents.FirstOrDefault();

        vm.SelectedEventId = selected?.EventId;
        vm.SelectedEventName = selected?.EventName;

        return null;
    }

    private string? FirstModelError()
    {
        return ModelState.Values
            .SelectMany(v => v.Errors)
            .Select(e => e.ErrorMessage)
            .FirstOrDefault(m => !string.IsNullOrWhiteSpace(m));
    }

}
