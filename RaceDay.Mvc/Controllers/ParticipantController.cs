using Microsoft.AspNetCore.Mvc;
using RaceDay.Contracts.DTOs.Categories;
using RaceDay.Contracts.DTOs.Enrolments;
using RaceDay.Contracts.DTOs.Events;
using RaceDay.Contracts.DTOs.Images;
using RaceDay.Contracts.DTOs.Profile;
using RaceDay.Contracts.DTOs.Results;
using RaceDay.Mvc.Filters;
using RaceDay.Mvc.Services;
using RaceDay.Mvc.ViewModels;

namespace RaceDay.Mvc.Controllers;

// Only logged-in Participants can access this controller.
[SessionAuthorize(UserRoles.Participant)]
public class ParticipantController : AppControllerBase
{
    private readonly RaceDayApiClient _api;

    public ParticipantController(RaceDayApiClient api)
    {
        _api = api;
    }

    // =====================================================
    // UPCOMING EVENTS
    // =====================================================

    public async Task<IActionResult> Index(string? type, string? search)
    {
        var vm = new EventBrowseVm
        {
            Type = type,
            Search = search
        };

        var result = await _api.GetAsync<List<EventResponseDto>>(
            "api/events");

        var expired = HandleExpiredSession(result);
        if (expired != null) return expired;

        if (!result.Success)
            TempData["Error"] = result.Error;

        var events = (result.Data ?? new List<EventResponseDto>())
            .Where(e => e.EventDate.Date >= DateTime.Today);

        if (!string.IsNullOrWhiteSpace(type))
        {
            events = events.Where(e => string.Equals(
                e.EventTypeName,
                type,
                StringComparison.OrdinalIgnoreCase));
        }

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
    // EVENT DETAILS
    // =====================================================

    public async Task<IActionResult> Details(int id)
    {
        var eventResult = await _api.GetAsync<EventResponseDto>(
            $"api/events/{id}");

        var expired = HandleExpiredSession(eventResult);
        if (expired != null) return expired;

        if (!eventResult.Success || eventResult.Data == null)
        {
            TempData["Error"] = eventResult.Error ?? "Event not found.";
            return RedirectToAction(nameof(Index));
        }

        var categories = await _api.GetAsync<List<CategoryResponseDto>>(
            $"api/events/{id}/categories");

        var mine = await _api.GetAsync<List<EnrolmentResponseDto>>(
            "api/enrolments/me");

        expired = HandleExpiredSession(mine);
        if (expired != null) return expired;

        return View(new EventDetailsVm
        {
            Event = eventResult.Data,
            Categories = categories.Data ?? new List<CategoryResponseDto>(),
            MyEnrolment = mine.Data?.FirstOrDefault(e => e.EventId == id)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EnterEvent(int eventId, int categoryId)
    {
        if (categoryId <= 0)
        {
            TempData["Error"] =
                "Please choose a category before entering.";

            return RedirectToAction(nameof(Details), new { id = eventId });
        }

        var result = await _api.PostAsync(
            "api/enrolments",
            new CreateEnrolmentRequestDto
            {
                EventId = eventId,
                CategoryId = categoryId
            });

        var expired = HandleExpiredSession(result);
        if (expired != null) return expired;

        if (!result.Success)
        {
            TempData["Error"] = result.Error ??
                "You could not be entered. Please try again.";

            return RedirectToAction(nameof(Details), new { id = eventId });
        }

        TempData["Success"] =
            "You have entered the event. The organiser will confirm your entry.";

        return RedirectToAction(nameof(MyEvents));
    }

    // =====================================================
    // MY EVENTS
    // =====================================================

    public async Task<IActionResult> MyEvents()
    {
        var enrolments = await _api.GetAsync<List<EnrolmentResponseDto>>(
            "api/enrolments/me");

        var expired = HandleExpiredSession(enrolments);
        if (expired != null) return expired;

        var events = await _api.GetAsync<List<EventResponseDto>>(
            "api/events");

        if (!enrolments.Success)
            TempData["Error"] = enrolments.Error;

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
            .OrderBy(r => r.EventDate ?? DateTime.MaxValue)
            .ToList();

        return View(rows);
    }

    // =====================================================
    // MY RESULTS
    // =====================================================

    public async Task<IActionResult> MyResults()
    {
        var result = await _api.GetAsync<List<ResultResponseDto>>(
            "api/results/me");

        var expired = HandleExpiredSession(result);
        if (expired != null) return expired;

        if (!result.Success)
            TempData["Error"] = result.Error;

        var results = (result.Data ?? new List<ResultResponseDto>())
            .OrderByDescending(r => r.EventDate)
            .ToList();

        return View(results);
    }

    // =====================================================
    // PROFILE
    // =====================================================

    // Loads the current Participant's profile.
    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        var result = await _api.GetAsync<ProfileResponseDto>(
            "api/users/me");

        var expired = HandleExpiredSession(result);
        if (expired != null) return expired;

        if (!result.Success || result.Data == null)
        {
            TempData["Error"] =
                result.Error ?? "Your profile could not be loaded.";

            return RedirectToAction(nameof(Index));
        }

        return View(new ProfileFormVm
        {
            FirstName = result.Data.FirstName,
            LastName = result.Data.LastName,
            PhoneNumber = result.Data.PhoneNumber,
            Email = result.Data.Email,
            Role = result.Data.Role,
            ProfileImageUrl = result.Data.ProfileImageUrl
        });
    }

    // Updates the Participant's personal details.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(ProfileFormVm vm)
    {
        if (!ModelState.IsValid)
            return View(vm);

        var result = await _api.PutAsync<ProfileResponseDto>(
            "api/users/me",
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
            ModelState.AddModelError(
                string.Empty,
                result.Error ?? "Your profile could not be updated.");

            return View(vm);
        }

        if (result.Data != null)
        {
            HttpContext.Session.SetString(
                SessionKeys.DisplayName,
                $"{result.Data.FirstName} {result.Data.LastName}");

            vm.ProfileImageUrl = result.Data.ProfileImageUrl;
        }

        TempData["Success"] = "Your profile has been updated.";

        return RedirectToAction(nameof(Profile));
    }

    // =====================================================
    // PROFILE PICTURE UPLOAD
    // =====================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadProfilePhoto(IFormFile? file)
    {
        if (file == null || file.Length == 0)
        {
            TempData["Error"] = "Please choose a profile picture.";
            return RedirectToAction(nameof(Profile));
        }

        // The maximum image size is 5 MB.
        if (file.Length > 5 * 1024 * 1024)
        {
            TempData["Error"] = "The image must be 5 MB or smaller.";
            return RedirectToAction(nameof(Profile));
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

            return RedirectToAction(nameof(Profile));
        }

        // Send the image to the Part 2 API.
        // The API is responsible for uploading it to Azure.
        var result = await _api.PostFileAsync<ImageUploadResponseDto>(
            "api/users/me/photo",
            file);

        var expired = HandleExpiredSession(result);
        if (expired != null) return expired;

        if (!result.Success)
        {
            TempData["Error"] = result.Error ??
                "Your profile picture could not be uploaded.";

            return RedirectToAction(nameof(Profile));
        }

        TempData["Success"] =
            "Your profile picture has been uploaded successfully.";

        return RedirectToAction(nameof(Profile));
    }
}