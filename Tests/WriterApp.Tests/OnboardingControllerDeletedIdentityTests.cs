using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using WriterApp.Application.Documents;
using WriterApp.Application.Security;
using WriterApp.Application.Users;
using WriterApp.Controllers;
using WriterApp.Data;
using Xunit;

namespace WriterApp.Tests
{
    public sealed class OnboardingControllerDeletedIdentityTests
    {
        [Fact]
        public async Task GetState_DeletedIdentity_ReturnsForbidden_WithoutCreatingProfile()
        {
            await using SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync();
            await using AppDbContext dbContext = BuildDbContext(connection);

            DeletedUserIdentityService deletedUserIdentityService = new(dbContext);
            await deletedUserIdentityService.UpsertDeletedIdentityAsync(
                "user-deleted",
                "deleted@example.com",
                "Deleted User",
                "admin-1",
                "admin@example.com",
                "admin_delete",
                CancellationToken.None);
            await dbContext.SaveChangesAsync();

            OnboardingController controller = new(
                dbContext,
                new StubUserIdResolver("user-deleted"),
                deletedUserIdentityService,
                new StubOnboardingBootstrapService(),
                new UserEventService(dbContext, NullLogger<UserEventService>.Instance),
                NullLogger<OnboardingController>.Instance)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext()
                }
            };

            ActionResult<OnboardingController.OnboardingStateResponse> result = await controller.GetState(CancellationToken.None);

            ObjectResult forbidden = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);
            Assert.Empty(await dbContext.UserProfiles.ToListAsync());
        }

        private static AppDbContext BuildDbContext(SqliteConnection connection)
        {
            DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;

            AppDbContext context = new(options);
            context.Database.EnsureCreated();
            return context;
        }

        private sealed class StubUserIdResolver : IUserIdResolver
        {
            private readonly string _userId;

            public StubUserIdResolver(string userId)
            {
                _userId = userId;
            }

            public string ResolveUserId(System.Security.Claims.ClaimsPrincipal user) => _userId;
        }

        private sealed class StubOnboardingBootstrapService : IOnboardingBootstrapService
        {
            public Task<OnboardingBootstrapResult> CreateStarterWorkspaceForOnboardingAsync(string ownerUserId, string intent, CancellationToken ct)
                => throw new NotSupportedException();
        }
    }
}
