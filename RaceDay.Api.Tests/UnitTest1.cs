using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Controllers;
using RaceDay.Api.Data;
using RaceDay.Api.Models;

namespace RaceDay.Api.Tests
{
    public class UnitTest1
    {
        [Fact]
        public void PasswordHasher_HashesAndVerifiesPassword()
        {
            // Arrange
            var user = new User();
            var passwordHasher = new PasswordHasher<User>();
            string password = "RaceDayTest123!";

            // Act
            string passwordHash =
                passwordHasher.HashPassword(user, password);

            var correctPasswordResult =
                passwordHasher.VerifyHashedPassword(
                    user, passwordHash, password);

            var incorrectPasswordResult =
                passwordHasher.VerifyHashedPassword(
                    user, passwordHash, "WrongPassword123!");

            // Assert
            Assert.NotEqual(password, passwordHash);

            Assert.Equal(
                PasswordVerificationResult.Success,
                correctPasswordResult);

            Assert.Equal(
                PasswordVerificationResult.Failed,
                incorrectPasswordResult);
        }

        [Fact]
        public async Task CreateEvent_ParticipantRole_ReturnsForbidden()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<RaceDayDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            using var context = new RaceDayDbContext(options);

            var controller = new EventsController(context);
            controller.ControllerContext = CreateControllerContext(
                1, "Participant");

            var request = new EventRequest
            {
                EventName = "Test Race",
                Description = "Testing permissions",
                EventDate = DateTime.UtcNow.AddDays(30),
                Location = "East London",
                Distance = 10,
                EventTypeID = 1
            };

            // Act
            var result = await controller.CreateEvent(request);

            // Assert
            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(403, objectResult.StatusCode);
        }

        [Fact]
        public async Task CreateEnrolment_ValidRequest_ReturnsCreated()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<RaceDayDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            using var context = new RaceDayDbContext(options);

            context.Events.Add(new Event
            {
                EventID = 1,
                EventName = "Test Race",
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
                CategoryName = "10km"
            });

            context.EnrolmentStatuses.Add(new EnrolmentStatus
            {
                EnrolmentStatusID = 1,
                StatusName = "Pending"
            });

            await context.SaveChangesAsync();

            var controller = new EnrolmentsController(context);
            controller.ControllerContext = CreateControllerContext(
                3, "Participant");

            var request = new CreateEnrolmentRequest
            {
                EventID = 1,
                CategoryID = 1
            };

            // Act
            var result = await controller.CreateEnrolment(request);

            // Assert
            Assert.IsType<CreatedAtActionResult>(result);

            var enrolment = await context.Enrolments
                .SingleOrDefaultAsync();

            Assert.NotNull(enrolment);
            Assert.Equal(3, enrolment.ParticipantID);
            Assert.Equal(1, enrolment.EventID);
            Assert.Equal(1, enrolment.CategoryID);
        }

        [Fact]
        public async Task CreateEnrolment_CategoryBelongsToDifferentEvent_ReturnsBadRequest()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<RaceDayDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            using var context = new RaceDayDbContext(options);

            context.Events.Add(new Event
            {
                EventID = 1,
                EventName = "Test Race",
                EventDate = DateTime.UtcNow.AddDays(30),
                Location = "East London",
                Distance = 10,
                OrganiserID = 2,
                EventTypeID = 1
            });

            context.Categories.Add(new Category
            {
                CategoryID = 2,
                EventID = 99,
                CategoryName = "Different Event Category"
            });

            await context.SaveChangesAsync();

            var controller = new EnrolmentsController(context);
            controller.ControllerContext = CreateControllerContext(
                3, "Participant");

            var request = new CreateEnrolmentRequest
            {
                EventID = 1,
                CategoryID = 2
            };

            // Act
            var result = await controller.CreateEnrolment(request);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);

            Assert.Empty(await context.Enrolments.ToListAsync());
        }

        private static ControllerContext CreateControllerContext(
            int userId, string role)
        {
            var httpContext = new DefaultHttpContext();
            var session = new TestSession();

            session.SetInt32("UserId", userId);
            session.SetString("Role", role);

            httpContext.Features.Set<ISessionFeature>(
                new TestSessionFeature { Session = session });

            return new ControllerContext
            {
                HttpContext = httpContext
            };
        }

        private class TestSessionFeature : ISessionFeature
        {
            public ISession Session { get; set; } = null!;
        }

        private class TestSession : ISession
        {
            private readonly Dictionary<string, byte[]> _store = new();

            public string Id => "TestSessionId";

            public bool IsAvailable => true;

            public IEnumerable<string> Keys => _store.Keys;

            public void Clear()
            {
                _store.Clear();
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
                _store.Remove(key);
            }

            public void Set(string key, byte[] value)
            {
                _store[key] = value;
            }

            public bool TryGetValue(string key, out byte[] value)
            {
                return _store.TryGetValue(key, out value!);
            }
        }
    }
}
