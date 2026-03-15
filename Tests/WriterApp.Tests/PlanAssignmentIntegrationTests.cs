using System;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using WriterApp.Application.Security;
using WriterApp.Application.Subscriptions;
using WriterApp.Application.Usage;
using WriterApp.Data;
using WriterApp.Data.Subscriptions;
using Xunit;

namespace WriterApp.Tests
{
    public sealed class PlanAssignmentIntegrationTests
    {
        [Fact]
        public async Task AssignPlanAsync_CreatesAssignment_AndEntitlementReflectsLatestPlan()
        {
            await using SqliteConnection connection = new("DataSource=:memory:");
            await connection.OpenAsync();
            await using AppDbContext dbContext = BuildDbContext(connection);

            string userId = "user-admin-flow";
            DateTime now = DateTime.UtcNow;
            TestClock clock = new(now);

            IPlanRepository planRepository = new PlanRepository(dbContext);
            UserEntitlementStore userEntitlementStore = new(
                dbContext,
                clock,
                new DeletedUserIdentityService(dbContext));
            IMemoryCache cache = new MemoryCache(new MemoryCacheOptions());
            EntitlementService entitlementService = new(planRepository, userEntitlementStore, cache);
            PlanAssignmentService planAssignmentService = new(
                dbContext,
                entitlementService,
                NullLogger<PlanAssignmentService>.Instance);

            await planAssignmentService.AssignPlanAsync(
                userId,
                UserEntitlementDefaults.StandardPlanKey,
                "admin",
                "admin@local");
            await planAssignmentService.AssignPlanAsync(
                userId,
                UserEntitlementDefaults.ProfessionalPlanKey,
                "admin",
                "admin@local");

            int assignmentCount = await dbContext.UserPlanAssignments
                .CountAsync(item => item.UserId == userId);
            Assert.Equal(2, assignmentCount);

            UserEntitlement entitlement = await userEntitlementStore.GetOrCreateAsync(userId);
            Assert.Equal(UserEntitlementDefaults.ProfessionalPlanKey, entitlement.PlanKey);
            Assert.Equal(UserEntitlementDefaults.PROFESSIONAL_MONTHLY_TOKEN_BUDGET, entitlement.AiMonthlyTokenBudget);

            UserEntitlements resolved = await entitlementService.GetEntitlementsAsync(userId);
            Assert.Equal("professional", resolved.PlanKey);
            Assert.True(await entitlementService.HasAsync(userId, "ai.enabled"));
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

        private sealed class TestClock : IClock
        {
            private readonly DateTime _utcNow;

            public TestClock(DateTime utcNow)
            {
                _utcNow = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
            }

            public DateTime UtcNow => _utcNow;
        }
    }
}
