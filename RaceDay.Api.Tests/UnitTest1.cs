using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Controllers;
using RaceDay.Api.Data;
using RaceDay.Api.Models;
using RaceDay.Contracts.DTOs.Auth;

namespace RaceDay.Api.Tests
{
    public class UnitTest1
    {
        // Test 1: Passwords are hashed and can be verified.
        [Fact]
        public void PasswordHasher_HashesAndVerifiesPassword()
        {
            var hasher = new PasswordHasher<User>();

            var user = new User
            {
                Email = "test@example.com"
            };

            string password = "RaceDayTest123!";

            string hashedPassword =
                hasher.HashPassword(user, password);

            Assert.NotEqual(password, hashedPassword);

            var result = hasher.VerifyHashedPassword(
                user,
                hashedPassword,
                password);

            Assert.Equal(
                PasswordVerificationResult.Success,
                result);
        }

        // Test 2: Participants cannot create events.
        [Fact]
        public async Task CreateEvent_ParticipantRole_ReturnsForbidden()
        {
            var options = new DbContextOptionsBuilder<RaceDayDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            using var context = new RaceDayDbContext(options);

            var controller = new EventsController(context)
            {
                ControllerContext = CreateControllerContext(
                    1,
                    "Participant")
            };

            var request = new EventRequest
            {
                EventName = "Test Running Event",
                Description = "A test event",
                EventDate = DateTime.UtcNow.AddDays(30),
                Location = "East London",
                Distance = 10,
                EventTypeID = 1
            };

            var result = await controller.CreateEvent(request);

            Assert.IsType<ObjectResult>(result);

            var objectResult = (ObjectResult)result;

            Assert.Equal(403, objectResult.StatusCode);

            Assert.Empty(await context.Events.ToListAsync());
        }

        // Test 3: Organisers can create events.
        [Fact]
        public async Task CreateEvent_OrganiserRole_ReturnsCreated()
        {
            var options = new DbContextOptionsBuilder<RaceDayDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            using var context = new RaceDayDbContext(options);

            context.EventTypes.Add(new EventType
            {
                EventTypeID = 1,
                TypeName = "Running"
            });

            await context.SaveChangesAsync();

            var controller = new EventsController(context)
            {
                ControllerContext = CreateControllerContext(
                    1,
                    "Organiser")
            };

            var request = new EventRequest
            {
                EventName = "Community Run",
                Description = "A community running event",
                EventDate = DateTime.UtcNow.AddDays(30),
                Location = "East London",
                Distance = 10,
                EventTypeID = 1
            };

            var result = await controller.CreateEvent(request);

            Assert.IsType<CreatedAtActionResult>(result);

            Assert.Single(await context.Events.ToListAsync());
        }

        // Test 4: An organiser cannot update another organiser's event.
        [Fact]
        public async Task UpdateEvent_DifferentOrganiser_ReturnsForbidden()
        {
            var options = new DbContextOptionsBuilder<RaceDayDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            using var context = new RaceDayDbContext(options);

            context.EventTypes.Add(new EventType
            {
                EventTypeID = 1,
                TypeName = "Running"
            });

            context.Events.Add(new Event
            {
                EventID = 1,
                EventName = "Existing Event",
                Description = "An existing event",
                EventDate = DateTime.UtcNow.AddDays(30),
                Location = "East London",
                Distance = 10,
                OrganiserID = 2,
                EventTypeID = 1
            });

            await context.SaveChangesAsync();

            var controller = new EventsController(context)
            {
                ControllerContext = CreateControllerContext(
                    1,
                    "Organiser")
            };

            var request = new EventRequest
            {
                EventName = "Updated Event",
                Description = "An updated event",
                EventDate = DateTime.UtcNow.AddDays(40),
                Location = "East London",
                Distance = 15,
                EventTypeID = 1
            };

            var result = await controller.UpdateEvent(1, request);

            Assert.IsType<ObjectResult>(result);

            var objectResult = (ObjectResult)result;

            Assert.Equal(403, objectResult.StatusCode);
        }

        // Test 5: A valid enrolment can be created.
        [Fact]
        public async Task CreateEnrolment_ValidRequest_ReturnsCreated()
        {
            var options = new DbContextOptionsBuilder<RaceDayDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            using var context = new RaceDayDbContext(options);

            context.EventTypes.Add(new EventType
            {
                EventTypeID = 1,
                TypeName = "Running"
            });

            context.Events.Add(new Event
            {
                EventID = 1,
                EventName = "Test Run",
                Description = "A test event",
                EventDate = DateTime.UtcNow.AddDays(30),
                Location = "East London",
                Distance = 10,
                OrganiserID = 2,
                EventTypeID = 1
            });

            context.Categories.Add(new Category
            {
                CategoryID = 1,
                EventID = 1,
                CategoryName = "10 km"
            });

            context.EnrolmentStatuses.Add(new EnrolmentStatus
            {
                EnrolmentStatusID = 1,
                StatusName = "Pending"
            });

            await context.SaveChangesAsync();

            var controller = new EnrolmentsController(context)
            {
                ControllerContext = CreateControllerContext(
                    3,
                    "Participant")
            };

            var request = new CreateEnrolmentRequest
            {
                EventID = 1,
                CategoryID = 1
            };

            var result = await controller.CreateEnrolment(request);

            Assert.IsType<CreatedAtActionResult>(result);
        }

        // Test 6: An enrolment is rejected if its category belongs
        // to a different event.
        [Fact]
        public async Task CreateEnrolment_CategoryBelongsToDifferentEvent_ReturnsBadRequest()
        {
            var options = new DbContextOptionsBuilder<RaceDayDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            using var context = new RaceDayDbContext(options);

            context.EventTypes.Add(new EventType
            {
                EventTypeID = 1,
                TypeName = "Running"
            });

            context.Events.AddRange(
                new Event
                {
                    EventID = 1,
                    EventName = "First Event",
                    Description = "First test event",
                    EventDate = DateTime.UtcNow.AddDays(30),
                    Location = "East London",
                    Distance = 10,
                    OrganiserID = 2,
                    EventTypeID = 1
                },
                new Event
                {
                    EventID = 2,
                    EventName = "Second Event",
                    Description = "Second test event",
                    EventDate = DateTime.UtcNow.AddDays(40),
                    Location = "East London",
                    Distance = 20,
                    OrganiserID = 2,
                    EventTypeID = 1
                });

            context.Categories.Add(new Category
            {
                CategoryID = 1,
                EventID = 2,
                CategoryName = "20 km"
            });

            context.EnrolmentStatuses.Add(new EnrolmentStatus
            {
                EnrolmentStatusID = 1,
                StatusName = "Registered"
            });

            await context.SaveChangesAsync();

            var controller = new EnrolmentsController(context)
            {
                ControllerContext = CreateControllerContext(
                    3,
                    "Participant")
            };

            var request = new CreateEnrolmentRequest
            {
                EventID = 1,
                CategoryID = 1
            };

            var result = await controller.CreateEnrolment(request);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        // Test 7: Registration creates a new Participant account
        // and stores a hashed password.
        [Fact]
        public async Task Register_ValidRequest_ReturnsCreated()
        {
            var options = new DbContextOptionsBuilder<RaceDayDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            using var context = new RaceDayDbContext(options);

            var controller = new AuthController(context);

            var request = new RegisterRequestDto
            {
                FirstName = "Ayakha",
                LastName = "Twalo",
                Email = "newparticipant@example.com",
                Password = "RaceDayTest123!",
                PhoneNumber = "0712345678"
            };

            var result = await controller.Register(request);

            Assert.IsType<CreatedResult>(result);

            var savedUser = await context.Users
                .FirstOrDefaultAsync(
                    user => user.Email == "newparticipant@example.com");

            Assert.NotNull(savedUser);
            Assert.Equal("Participant", savedUser.Role);
            Assert.NotEqual(
                "RaceDayTest123!",
                savedUser.PasswordHash);
        }

        // Test 8: Registration rejects an email address that
        // already belongs to an existing account.
        [Fact]
        public async Task Register_DuplicateEmail_ReturnsConflict()
        {
            var options = new DbContextOptionsBuilder<RaceDayDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            using var context = new RaceDayDbContext(options);

            context.Users.Add(new User
            {
                FirstName = "Existing",
                LastName = "Participant",
                Email = "existing@example.com",
                PasswordHash = "existing-hash",
                Role = "Participant"
            });

            await context.SaveChangesAsync();

            var controller = new AuthController(context);

            var request = new RegisterRequestDto
            {
                FirstName = "New",
                LastName = "Participant",
                Email = "existing@example.com",
                Password = "RaceDayTest123!",
                PhoneNumber = "0712345678"
            };

            var result = await controller.Register(request);

            Assert.IsType<ConflictObjectResult>(result);

            Assert.Single(await context.Users.ToListAsync());
        }

        // Test 9: Login accepts the correct email and password
        // and stores the user's identity and role in the session.
        [Fact]
        public async Task Login_ValidCredentials_ReturnsOk()
        {
            var options = new DbContextOptionsBuilder<RaceDayDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            using var context = new RaceDayDbContext(options);

            var passwordHasher = new PasswordHasher<User>();

            var user = new User
            {
                FirstName = "Test",
                LastName = "Participant",
                Email = "login@example.com",
                Role = "Participant"
            };

            user.PasswordHash = passwordHasher.HashPassword(
                user,
                "RaceDayTest123!");

            context.Users.Add(user);
            await context.SaveChangesAsync();

            var controller = new AuthController(context)
            {
                ControllerContext = CreateControllerContext(
                    user.UserID,
                    user.Role)
            };

            var result = await controller.Login(new LoginRequestDto
            {
                Email = "login@example.com",
                Password = "RaceDayTest123!"
            });

            Assert.IsType<OkObjectResult>(result);

            Assert.Equal(
                user.UserID,
                controller.HttpContext.Session.GetInt32("UserId"));

            Assert.Equal(
                "Participant",
                controller.HttpContext.Session.GetString("Role"));
        }

        // Test 10: Login rejects an incorrect password.
        [Fact]
        public async Task Login_IncorrectPassword_ReturnsUnauthorized()
        {
            var options = new DbContextOptionsBuilder<RaceDayDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            using var context = new RaceDayDbContext(options);

            var passwordHasher = new PasswordHasher<User>();

            var user = new User
            {
                FirstName = "Test",
                LastName = "Participant",
                Email = "login@example.com",
                Role = "Participant"
            };

            user.PasswordHash = passwordHasher.HashPassword(
                user,
                "CorrectPassword123!");

            context.Users.Add(user);
            await context.SaveChangesAsync();

            var controller = new AuthController(context);

            var result = await controller.Login(new LoginRequestDto
            {
                Email = "login@example.com",
                Password = "WrongPassword123!"
            });

            Assert.IsType<UnauthorizedObjectResult>(result);
        }

        // Creates a controller context with a test session.
        private static ControllerContext CreateControllerContext(
            int userId,
            string role)
        {
            var httpContext = new DefaultHttpContext();

            var session = new TestSession();

            session.SetInt32("UserId", userId);
            session.SetString("Role", role);

            httpContext.Features.Set<ISessionFeature>(
                new TestSessionFeature
                {
                    Session = session
                });

            return new ControllerContext
            {
                HttpContext = httpContext
            };
        }

        // Provides the session feature required by HttpContext.
        private sealed class TestSessionFeature : ISessionFeature
        {
            public ISession Session { get; set; } = null!;
        }

        // A lightweight in-memory session used by the unit tests.
        private sealed class TestSession : ISession
        {
            private readonly Dictionary<string, byte[]> _session =
                new Dictionary<string, byte[]>();

            public bool IsAvailable => true;

            public string Id => "TestSessionId";

            public IEnumerable<string> Keys => _session.Keys;

            public void Clear()
            {
                _session.Clear();
            }

            public Task CommitAsync(
                CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }

            public Task LoadAsync(
                CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }

            public void Remove(string key)
            {
                _session.Remove(key);
            }

            public void Set(string key, byte[] value)
            {
                _session[key] = value;
            }

            public bool TryGetValue(string key, out byte[]? value)
            {
                if (_session.TryGetValue(key, out var storedValue))
                {
                    value = storedValue;
                    return true;
                }

                value = null;
                return false;
            }
        }
    }
}
