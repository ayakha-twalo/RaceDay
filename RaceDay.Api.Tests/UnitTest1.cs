using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using RaceDay.Api.Controllers;
using RaceDay.Api.Data;
using RaceDay.Api.Models;
using RaceDay.Contracts.DTOs.Auth;
using RaceDay.Contracts.DTOs.Categories;
using RaceDay.Contracts.DTOs.Enrolments;
using RaceDay.Contracts.DTOs.Events;
using RaceDay.Contracts.DTOs.Profile;
using RaceDay.Contracts.DTOs.Results;

namespace RaceDay.Api.Tests
{
    public class UnitTest1
    {
        private const string OrganiserCode = "TEST-ORG-CODE";
        private const string Password = "RaceDayTest123!";

        // =====================================================
        // AUTHENTICATION: registration, login, logout
        // =====================================================

        [Fact]
        public void PasswordHasher_HashesAndVerifiesPassword()
        {
            var hasher = new PasswordHasher<User>();
            var user = new User { Email = "test@example.com" };

            string hash = hasher.HashPassword(user, Password);

            Assert.NotEqual(Password, hash);
            Assert.Equal(
                PasswordVerificationResult.Success,
                hasher.VerifyHashedPassword(user, hash, Password));
        }

        [Fact]
        public async Task Register_ValidParticipant_ReturnsCreated_AndHashesPassword()
        {
            using var context = NewContext();

            var result = await NewAuth(context).Register(new RegisterRequestDto
            {
                FirstName = "Ayakha",
                LastName = "Twalo",
                Email = "NewPerson@Example.com",
                Password = Password,
                Role = "Participant"
            });

            AssertStatus(result, 201);

            var saved = await context.Users.SingleAsync();
            Assert.Equal("newperson@example.com", saved.Email);
            Assert.Equal("Participant", saved.Role);
            Assert.NotEqual(Password, saved.PasswordHash);
        }

        [Fact]
        public async Task Register_OrganiserWithValidCode_ReturnsCreated()
        {
            using var context = NewContext();

            var result = await NewAuth(context).Register(new RegisterRequestDto
            {
                FirstName = "Org",
                LastName = "Anizer",
                Email = "organiser@example.com",
                Password = Password,
                Role = "Organiser",
                OrganiserCode = OrganiserCode
            });

            AssertStatus(result, 201);
            Assert.Equal("Organiser", (await context.Users.SingleAsync()).Role);
        }

        [Fact]
        public async Task Register_OrganiserWithWrongCode_ReturnsForbidden()
        {
            using var context = NewContext();

            var result = await NewAuth(context).Register(new RegisterRequestDto
            {
                FirstName = "Sneaky",
                LastName = "Person",
                Email = "sneaky@example.com",
                Password = Password,
                Role = "Organiser",
                OrganiserCode = "WRONG-CODE"
            });

            AssertStatus(result, 403);
            Assert.Empty(await context.Users.ToListAsync());
        }

        [Fact]
        public async Task Register_InvalidRole_ReturnsBadRequest()
        {
            using var context = NewContext();

            var result = await NewAuth(context).Register(new RegisterRequestDto
            {
                FirstName = "Test",
                LastName = "User",
                Email = "admin@example.com",
                Password = Password,
                Role = "Admin"
            });

            AssertStatus(result, 400);
            Assert.Empty(await context.Users.ToListAsync());
        }

        [Fact]
        public async Task Register_DuplicateEmail_ReturnsConflict()
        {
            using var context = NewContext();
            await AddUserAsync(context, "Participant", "existing@example.com");

            var result = await NewAuth(context).Register(new RegisterRequestDto
            {
                FirstName = "New",
                LastName = "Person",
                Email = "existing@example.com",
                Password = Password,
                Role = "Participant"
            });

            AssertStatus(result, 409);
            Assert.Single(await context.Users.ToListAsync());
        }

        [Fact]
        public async Task Login_ValidCredentials_ReturnsOk_AndStoresSession()
        {
            using var context = NewContext();
            var user = await AddUserAsync(
                context, "Participant", "login@example.com", Password);

            var controller = NewAuth(context);

            var result = await controller.Login(new LoginRequestDto
            {
                Email = "login@example.com",
                Password = Password
            });

            AssertStatus(result, 200);
            Assert.Equal(user.UserID,
                controller.HttpContext.Session.GetInt32("UserId"));
            Assert.Equal("Participant",
                controller.HttpContext.Session.GetString("Role"));
        }

        [Fact]
        public async Task Login_IncorrectPassword_ReturnsUnauthorized()
        {
            using var context = NewContext();
            await AddUserAsync(
                context, "Participant", "login@example.com", Password);

            var controller = NewAuth(context);

            var result = await controller.Login(new LoginRequestDto
            {
                Email = "login@example.com",
                Password = "WrongPassword123!"
            });

            AssertStatus(result, 401);
            Assert.Null(controller.HttpContext.Session.GetInt32("UserId"));
        }

        [Fact]
        public async Task Login_UnknownEmail_ReturnsUnauthorized()
        {
            using var context = NewContext();

            var result = await NewAuth(context).Login(new LoginRequestDto
            {
                Email = "nobody@example.com",
                Password = Password
            });

            AssertStatus(result, 401);
        }

        [Fact]
        public void Logout_ClearsSession()
        {
            using var context = NewContext();
            var controller = new AuthController(context, TestConfig())
            {
                ControllerContext = ContextFor(5, "Participant")
            };

            var result = controller.Logout();

            AssertStatus(result, 200);
            Assert.Null(controller.HttpContext.Session.GetInt32("UserId"));
            Assert.Null(controller.HttpContext.Session.GetString("Role"));
        }

        // =====================================================
        // PROFILE
        // =====================================================

        [Fact]
        public async Task GetMyProfile_NotLoggedIn_ReturnsUnauthorized()
        {
            using var context = NewContext();
            var controller = new ProfileController(context)
            {
                ControllerContext = ContextFor(null, null)
            };

            AssertStatus(await controller.GetMyProfile(), 401);
        }

        [Fact]
        public async Task GetMyProfile_LoggedIn_ReturnsOwnProfile()
        {
            using var context = NewContext();
            var user = await AddUserAsync(context, "Participant", "me@example.com");
            var controller = new ProfileController(context)
            {
                ControllerContext = ContextFor(user.UserID, "Participant")
            };

            var result = await controller.GetMyProfile();

            var ok = Assert.IsType<OkObjectResult>(result);
            var profile = Assert.IsType<ProfileResponseDto>(ok.Value);
            Assert.Equal("me@example.com", profile.Email);
        }

        [Fact]
        public async Task UpdateMyProfile_ChangesOnlyTheLoggedInUser()
        {
            using var context = NewContext();
            var me = await AddUserAsync(context, "Participant", "me@example.com");
            var other = await AddUserAsync(context, "Participant", "other@example.com");
            var controller = new ProfileController(context)
            {
                ControllerContext = ContextFor(me.UserID, "Participant")
            };

            var result = await controller.UpdateMyProfile(new UpdateProfileRequestDto
            {
                FirstName = "Changed",
                LastName = "Name",
                PhoneNumber = "0711111111"
            });

            AssertStatus(result, 200);
            Assert.Equal("Changed",
                (await context.Users.FindAsync(me.UserID))!.FirstName);
            Assert.NotEqual("Changed",
                (await context.Users.FindAsync(other.UserID))!.FirstName);
        }

        // =====================================================
        // EVENTS
        // =====================================================

        [Fact]
        public async Task CreateEvent_ParticipantRole_ReturnsForbidden()
        {
            using var context = NewContext();
            await SeedLookupsAsync(context);

            var controller = NewEvents(context, 1, "Participant");

            var result = await controller.CreateEvent(NewEventRequest());

            AssertStatus(result, 403);
            Assert.Empty(await context.Events.ToListAsync());
        }

        [Fact]
        public async Task CreateEvent_NotLoggedIn_ReturnsUnauthorized()
        {
            using var context = NewContext();
            await SeedLookupsAsync(context);

            var controller = NewEvents(context, null, null);

            AssertStatus(await controller.CreateEvent(NewEventRequest()), 401);
            Assert.Empty(await context.Events.ToListAsync());
        }

        [Fact]
        public async Task CreateEvent_OrganiserRole_ReturnsCreated()
        {
            using var context = NewContext();
            await SeedLookupsAsync(context);
            var organiser = await AddUserAsync(context, "Organiser", "org@example.com");

            var controller = NewEvents(context, organiser.UserID, "Organiser");

            var result = await controller.CreateEvent(NewEventRequest());

            AssertStatus(result, 201);
            var saved = await context.Events.SingleAsync();
            Assert.Equal(organiser.UserID, saved.OrganiserID);
        }

        [Fact]
        public async Task UpdateEvent_DifferentOrganiser_ReturnsForbidden()
        {
            using var context = NewContext();
            await SeedLookupsAsync(context);
            var owner = await AddUserAsync(context, "Organiser", "owner@example.com");
            var intruder = await AddUserAsync(context, "Organiser", "intruder@example.com");
            var ev = await AddEventAsync(context, owner.UserID);

            var controller = NewEvents(context, intruder.UserID, "Organiser");

            var result = await controller.UpdateEvent(ev.EventID, new UpdateEventRequestDto
            {
                EventName = "Hijacked",
                EventDate = DateTime.UtcNow.AddDays(40),
                Location = "Somewhere",
                Distance = 5,
                EventTypeId = 1
            });

            AssertStatus(result, 403);
            Assert.Equal("Test Event",
                (await context.Events.FindAsync(ev.EventID))!.EventName);
        }

        [Fact]
        public async Task DeleteEvent_OwnEvent_ReturnsNoContent()
        {
            using var context = NewContext();
            await SeedLookupsAsync(context);
            var owner = await AddUserAsync(context, "Organiser", "owner@example.com");
            var ev = await AddEventAsync(context, owner.UserID);

            var controller = NewEvents(context, owner.UserID, "Organiser");

            AssertStatus(await controller.DeleteEvent(ev.EventID), 204);
            Assert.Empty(await context.Events.ToListAsync());
        }

        [Fact]
        public async Task DeleteEvent_WithEnrolments_ReturnsConflict()
        {
            using var context = NewContext();
            await SeedLookupsAsync(context);
            var owner = await AddUserAsync(context, "Organiser", "owner@example.com");
            var runner = await AddUserAsync(context, "Participant", "runner@example.com");
            var ev = await AddEventAsync(context, owner.UserID);
            var cat = await AddCategoryAsync(context, ev.EventID, "10 km");
            await AddEnrolmentAsync(context, runner.UserID, ev.EventID, cat.CategoryID);

            var controller = NewEvents(context, owner.UserID, "Organiser");

            AssertStatus(await controller.DeleteEvent(ev.EventID), 409);
            Assert.Single(await context.Events.ToListAsync());
        }

        [Fact]
        public async Task GetMyEvents_Organiser_ReturnsOnlyOwnEvents()
        {
            using var context = NewContext();
            await SeedLookupsAsync(context);
            var mine = await AddUserAsync(context, "Organiser", "mine@example.com");
            var theirs = await AddUserAsync(context, "Organiser", "theirs@example.com");
            await AddEventAsync(context, mine.UserID);
            await AddEventAsync(context, theirs.UserID);

            var controller = NewEvents(context, mine.UserID, "Organiser");

            var result = await controller.GetMyEvents();

            var ok = Assert.IsType<OkObjectResult>(result);
            var events = Assert.IsAssignableFrom<IEnumerable<EventResponseDto>>(ok.Value);
            Assert.Single(events);
        }

        [Fact]
        public async Task GetMyEvents_Participant_ReturnsForbidden()
        {
            using var context = NewContext();
            var controller = NewEvents(context, 3, "Participant");

            AssertStatus(await controller.GetMyEvents(), 403);
        }

        // =====================================================
        // CATEGORIES
        // =====================================================

        [Fact]
        public async Task CreateCategory_OwningOrganiser_ReturnsCreated()
        {
            using var context = NewContext();
            await SeedLookupsAsync(context);
            var owner = await AddUserAsync(context, "Organiser", "owner@example.com");
            var ev = await AddEventAsync(context, owner.UserID);

            var controller = new CategoriesController(context)
            {
                ControllerContext = ContextFor(owner.UserID, "Organiser")
            };

            var result = await controller.CreateCategory(
                ev.EventID,
                new CreateCategoryRequestDto { CategoryName = "Under 20" });

            AssertStatus(result, 201);
            Assert.Single(await context.Categories.ToListAsync());
        }

        [Fact]
        public async Task CreateCategory_Participant_ReturnsForbidden()
        {
            using var context = NewContext();
            await SeedLookupsAsync(context);
            var owner = await AddUserAsync(context, "Organiser", "owner@example.com");
            var ev = await AddEventAsync(context, owner.UserID);

            var controller = new CategoriesController(context)
            {
                ControllerContext = ContextFor(99, "Participant")
            };

            var result = await controller.CreateCategory(
                ev.EventID,
                new CreateCategoryRequestDto { CategoryName = "Under 20" });

            AssertStatus(result, 403);
            Assert.Empty(await context.Categories.ToListAsync());
        }

        // =====================================================
        // ENROLMENTS
        // =====================================================

        [Fact]
        public async Task CreateEnrolment_ValidRequest_ReturnsCreated_AndRecordsLink()
        {
            using var context = NewContext();
            await SeedLookupsAsync(context);
            var owner = await AddUserAsync(context, "Organiser", "owner@example.com");
            var runner = await AddUserAsync(context, "Participant", "runner@example.com");
            var ev = await AddEventAsync(context, owner.UserID);
            var cat = await AddCategoryAsync(context, ev.EventID, "10 km");

            var controller = NewEnrolments(context, runner.UserID, "Participant");

            var result = await controller.CreateEnrolment(new CreateEnrolmentRequestDto
            {
                EventId = ev.EventID,
                CategoryId = cat.CategoryID
            });

            AssertStatus(result, 201);

            // The enrolment must link the participant, event and category.
            var saved = await context.Enrolments.SingleAsync();
            Assert.Equal(runner.UserID, saved.ParticipantID);
            Assert.Equal(ev.EventID, saved.EventID);
            Assert.Equal(cat.CategoryID, saved.CategoryID);
            Assert.Equal(await PendingStatusIdAsync(context), saved.EnrolmentStatusID);
        }

        [Fact]
        public async Task CreateEnrolment_CategoryFromDifferentEvent_ReturnsBadRequest()
        {
            using var context = NewContext();
            await SeedLookupsAsync(context);
            var owner = await AddUserAsync(context, "Organiser", "owner@example.com");
            var runner = await AddUserAsync(context, "Participant", "runner@example.com");
            var firstEvent = await AddEventAsync(context, owner.UserID);
            var secondEvent = await AddEventAsync(context, owner.UserID);
            var otherCategory = await AddCategoryAsync(context, secondEvent.EventID, "20 km");

            var controller = NewEnrolments(context, runner.UserID, "Participant");

            var result = await controller.CreateEnrolment(new CreateEnrolmentRequestDto
            {
                EventId = firstEvent.EventID,
                CategoryId = otherCategory.CategoryID
            });

            AssertStatus(result, 400);
            Assert.Empty(await context.Enrolments.ToListAsync());
        }

        [Fact]
        public async Task CreateEnrolment_Organiser_ReturnsForbidden()
        {
            using var context = NewContext();
            await SeedLookupsAsync(context);
            var owner = await AddUserAsync(context, "Organiser", "owner@example.com");
            var ev = await AddEventAsync(context, owner.UserID);
            var cat = await AddCategoryAsync(context, ev.EventID, "10 km");

            var controller = NewEnrolments(context, owner.UserID, "Organiser");

            var result = await controller.CreateEnrolment(new CreateEnrolmentRequestDto
            {
                EventId = ev.EventID,
                CategoryId = cat.CategoryID
            });

            AssertStatus(result, 403);
            Assert.Empty(await context.Enrolments.ToListAsync());
        }

        [Fact]
        public async Task CreateEnrolment_NotLoggedIn_ReturnsUnauthorized()
        {
            using var context = NewContext();
            var controller = NewEnrolments(context, null, null);

            var result = await controller.CreateEnrolment(new CreateEnrolmentRequestDto
            {
                EventId = 1,
                CategoryId = 1
            });

            AssertStatus(result, 401);
        }

        [Fact]
        public async Task CreateEnrolment_AlreadyEnrolled_ReturnsConflict()
        {
            using var context = NewContext();
            await SeedLookupsAsync(context);
            var owner = await AddUserAsync(context, "Organiser", "owner@example.com");
            var runner = await AddUserAsync(context, "Participant", "runner@example.com");
            var ev = await AddEventAsync(context, owner.UserID);
            var cat = await AddCategoryAsync(context, ev.EventID, "10 km");
            await AddEnrolmentAsync(context, runner.UserID, ev.EventID, cat.CategoryID);

            var controller = NewEnrolments(context, runner.UserID, "Participant");

            var result = await controller.CreateEnrolment(new CreateEnrolmentRequestDto
            {
                EventId = ev.EventID,
                CategoryId = cat.CategoryID
            });

            AssertStatus(result, 409);
            Assert.Single(await context.Enrolments.ToListAsync());
        }

        [Fact]
        public async Task GetEventEnrolments_OtherOrganiser_ReturnsForbidden()
        {
            using var context = NewContext();
            await SeedLookupsAsync(context);
            var owner = await AddUserAsync(context, "Organiser", "owner@example.com");
            var other = await AddUserAsync(context, "Organiser", "other@example.com");
            var ev = await AddEventAsync(context, owner.UserID);

            var controller = NewEnrolments(context, other.UserID, "Organiser");

            AssertStatus(await controller.GetEventEnrolments(ev.EventID), 403);
        }

        [Fact]
        public async Task GetEventEnrolments_OwningOrganiser_ReturnsEnrolments()
        {
            using var context = NewContext();
            await SeedLookupsAsync(context);
            var owner = await AddUserAsync(context, "Organiser", "owner@example.com");
            var runner = await AddUserAsync(context, "Participant", "runner@example.com");
            var ev = await AddEventAsync(context, owner.UserID);
            var cat = await AddCategoryAsync(context, ev.EventID, "10 km");
            await AddEnrolmentAsync(context, runner.UserID, ev.EventID, cat.CategoryID);

            var controller = NewEnrolments(context, owner.UserID, "Organiser");

            var result = await controller.GetEventEnrolments(ev.EventID);

            var ok = Assert.IsType<OkObjectResult>(result);
            var list = Assert.IsAssignableFrom<IEnumerable<EnrolmentResponseDto>>(ok.Value);
            Assert.Single(list);
        }

        // =====================================================
        // RESULTS
        // =====================================================

        [Fact]
        public async Task RecordResult_Participant_ReturnsForbidden()
        {
            using var context = NewContext();
            var controller = NewResults(context, 3, "Participant");

            var result = await controller.RecordResult(1, NewResultRequest());

            AssertStatus(result, 403);
            Assert.Empty(await context.Results.ToListAsync());
        }

        [Fact]
        public async Task RecordResult_OwningOrganiser_ReturnsCreated()
        {
            using var context = NewContext();
            await SeedLookupsAsync(context);
            var owner = await AddUserAsync(context, "Organiser", "owner@example.com");
            var runner = await AddUserAsync(context, "Participant", "runner@example.com");
            var ev = await AddEventAsync(context, owner.UserID);
            var cat = await AddCategoryAsync(context, ev.EventID, "10 km");
            var enrolment = await AddEnrolmentAsync(context, runner.UserID, ev.EventID, cat.CategoryID);

            var controller = NewResults(context, owner.UserID, "Organiser");

            var result = await controller.RecordResult(enrolment.EnrolmentID, NewResultRequest());

            AssertStatus(result, 201);
            var saved = await context.Results.SingleAsync();
            Assert.Equal(enrolment.EnrolmentID, saved.EnrolmentID);
            Assert.Equal(1, saved.FinishPosition);
        }

        [Fact]
        public async Task RecordResult_OtherOrganiser_ReturnsForbidden()
        {
            using var context = NewContext();
            await SeedLookupsAsync(context);
            var owner = await AddUserAsync(context, "Organiser", "owner@example.com");
            var other = await AddUserAsync(context, "Organiser", "other@example.com");
            var runner = await AddUserAsync(context, "Participant", "runner@example.com");
            var ev = await AddEventAsync(context, owner.UserID);
            var cat = await AddCategoryAsync(context, ev.EventID, "10 km");
            var enrolment = await AddEnrolmentAsync(context, runner.UserID, ev.EventID, cat.CategoryID);

            var controller = NewResults(context, other.UserID, "Organiser");

            var result = await controller.RecordResult(enrolment.EnrolmentID, NewResultRequest());

            AssertStatus(result, 403);
            Assert.Empty(await context.Results.ToListAsync());
        }

        [Fact]
        public async Task GetMyResults_Participant_ReturnsOnlyOwnResults()
        {
            using var context = NewContext();
            await SeedLookupsAsync(context);
            var owner = await AddUserAsync(context, "Organiser", "owner@example.com");
            var runnerOne = await AddUserAsync(context, "Participant", "one@example.com");
            var runnerTwo = await AddUserAsync(context, "Participant", "two@example.com");
            var ev = await AddEventAsync(context, owner.UserID);
            var cat = await AddCategoryAsync(context, ev.EventID, "10 km");
            var enrolmentOne = await AddEnrolmentAsync(context, runnerOne.UserID, ev.EventID, cat.CategoryID);
            var enrolmentTwo = await AddEnrolmentAsync(context, runnerTwo.UserID, ev.EventID, cat.CategoryID);

            context.Results.AddRange(
                new Result { EnrolmentID = enrolmentOne.EnrolmentID, FinishTime = TimeSpan.FromMinutes(40), FinishPosition = 1 },
                new Result { EnrolmentID = enrolmentTwo.EnrolmentID, FinishTime = TimeSpan.FromMinutes(50), FinishPosition = 2 });
            await context.SaveChangesAsync();

            var controller = NewResults(context, runnerOne.UserID, "Participant");

            var result = await controller.GetMyResults();

            var ok = Assert.IsType<OkObjectResult>(result);
            var list = Assert.IsAssignableFrom<IEnumerable<ResultResponseDto>>(ok.Value);
            var only = Assert.Single(list);
            Assert.Equal(1, only.FinishPosition);
        }

        [Fact]
        public async Task GetMyResults_Organiser_ReturnsForbidden()
        {
            using var context = NewContext();
            var controller = NewResults(context, 1, "Organiser");

            AssertStatus(await controller.GetMyResults(), 403);
        }

        // =====================================================
        // HELPERS
        // =====================================================

        private static RaceDayDbContext NewContext()
        {
            var options = new DbContextOptionsBuilder<RaceDayDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            return new RaceDayDbContext(options);
        }

        private static IConfiguration TestConfig()
        {
            return new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "Registration:OrganiserCode", OrganiserCode }
                })
                .Build();
        }

        private static AuthController NewAuth(RaceDayDbContext context)
        {
            return new AuthController(context, TestConfig())
            {
                ControllerContext = ContextFor(null, null)
            };
        }

        private static EventsController NewEvents(
            RaceDayDbContext context, int? userId, string? role)
        {
            return new EventsController(context)
            {
                ControllerContext = ContextFor(userId, role)
            };
        }

        private static EnrolmentsController NewEnrolments(
            RaceDayDbContext context, int? userId, string? role)
        {
            return new EnrolmentsController(context)
            {
                ControllerContext = ContextFor(userId, role)
            };
        }

        private static ResultsController NewResults(
            RaceDayDbContext context, int? userId, string? role)
        {
            return new ResultsController(context)
            {
                ControllerContext = ContextFor(userId, role)
            };
        }

        private static CreateEventRequestDto NewEventRequest()
        {
            return new CreateEventRequestDto
            {
                EventName = "Community Run",
                Description = "A community running event",
                EventDate = DateTime.UtcNow.AddDays(30),
                Location = "East London",
                Distance = 10,
                EventTypeId = 1
            };
        }

        private static CreateResultRequestDto NewResultRequest()
        {
            return new CreateResultRequestDto
            {
                FinishTime = TimeSpan.FromMinutes(45),
                FinishPosition = 1
            };
        }

        // Adds the event types and enrolment statuses the API relies on
        // (only if they are not already there).
        private static async Task SeedLookupsAsync(RaceDayDbContext context)
        {
            if (!await context.EventTypes.AnyAsync())
            {
                context.EventTypes.Add(
                    new EventType { EventTypeID = 1, TypeName = "Run" });
            }

            if (!await context.EnrolmentStatuses.AnyAsync())
            {
                context.EnrolmentStatuses.AddRange(
                    new EnrolmentStatus { EnrolmentStatusID = 1, StatusName = "Pending" },
                    new EnrolmentStatus { EnrolmentStatusID = 2, StatusName = "Confirmed" },
                    new EnrolmentStatus { EnrolmentStatusID = 3, StatusName = "Cancelled" });
            }

            await context.SaveChangesAsync();
        }

        private static async Task<User> AddUserAsync(
            RaceDayDbContext context,
            string role,
            string email,
            string? password = null)
        {
            var user = new User
            {
                FirstName = role,
                LastName = "Tester",
                Email = email,
                PasswordHash = "placeholder-hash",
                Role = role
            };

            if (password != null)
            {
                user.PasswordHash =
                    new PasswordHasher<User>().HashPassword(user, password);
            }

            context.Users.Add(user);
            await context.SaveChangesAsync();
            return user;
        }

        private static async Task<Event> AddEventAsync(
            RaceDayDbContext context, int organiserId)
        {
            var ev = new Event
            {
                EventName = "Test Event",
                Description = "A test event",
                EventDate = DateTime.UtcNow.AddDays(30),
                Location = "East London",
                Distance = 10,
                OrganiserID = organiserId,
                EventTypeID = 1
            };

            context.Events.Add(ev);
            await context.SaveChangesAsync();
            return ev;
        }

        private static async Task<Category> AddCategoryAsync(
            RaceDayDbContext context, int eventId, string name)
        {
            var category = new Category { EventID = eventId, CategoryName = name };
            context.Categories.Add(category);
            await context.SaveChangesAsync();
            return category;
        }

        private static async Task<int> PendingStatusIdAsync(
            RaceDayDbContext context)
        {
            var pending = await context.EnrolmentStatuses
                .FirstAsync(s => s.StatusName == "Pending");
            return pending.EnrolmentStatusID;
        }

        private static async Task<Enrolment> AddEnrolmentAsync(
            RaceDayDbContext context, int participantId, int eventId, int categoryId)
        {
            var enrolment = new Enrolment
            {
                ParticipantID = participantId,
                EventID = eventId,
                CategoryID = categoryId,
                EnrolmentStatusID = await PendingStatusIdAsync(context),
                EnrolmentDate = DateTime.UtcNow
            };

            context.Enrolments.Add(enrolment);
            await context.SaveChangesAsync();
            return enrolment;
        }

        // Checks the HTTP status code of a controller result.
        private static void AssertStatus(IActionResult result, int expected)
        {
            int? actual = result switch
            {
                ObjectResult objectResult => objectResult.StatusCode,
                StatusCodeResult statusResult => statusResult.StatusCode,
                _ => null
            };

            Assert.Equal((int?)expected, actual);
        }

        // Creates a controller context with a fake session.
        // Pass null, null to simulate a user who is not logged in.
        private static ControllerContext ContextFor(int? userId, string? role)
        {
            var httpContext = new DefaultHttpContext();
            var session = new TestSession();

            if (userId != null && role != null)
            {
                session.SetInt32("UserId", userId.Value);
                session.SetString("Role", role);
            }

            httpContext.Features.Set<ISessionFeature>(
                new TestSessionFeature { Session = session });

            return new ControllerContext { HttpContext = httpContext };
        }

        private sealed class TestSessionFeature : ISessionFeature
        {
            public ISession Session { get; set; } = null!;
        }

        // A lightweight in-memory session used by the unit tests.
        private sealed class TestSession : ISession
        {
            private readonly Dictionary<string, byte[]> _session = new();

            public bool IsAvailable => true;

            public string Id => "TestSessionId";

            public IEnumerable<string> Keys => _session.Keys;

            public void Clear() => _session.Clear();

            public Task CommitAsync(CancellationToken cancellationToken = default)
                => Task.CompletedTask;

            public Task LoadAsync(CancellationToken cancellationToken = default)
                => Task.CompletedTask;

            public void Remove(string key) => _session.Remove(key);

            public void Set(string key, byte[] value) => _session[key] = value;

            public bool TryGetValue(string key, out byte[]? value)
            {
                if (_session.TryGetValue(key, out var stored))
                {
                    value = stored;
                    return true;
                }

                value = null;
                return false;
            }
        }
    }
}
