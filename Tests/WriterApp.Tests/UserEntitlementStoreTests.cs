using System;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WriterApp.Application.Subscriptions;
using WriterApp.Application.Usage;
using WriterApp.Data;
using WriterApp.Data.Subscriptions;
using Xunit;

namespace WriterApp.Tests
{
    public sealed class UserEntitlementStoreTests
    {
        [Fact]
        public async Task GetOrCreateAsync_UsesLatestAssignmentDeterministically()
        {
            await using SqliteConnection connection = new("DataSource=:memory:");
            await connection.OpenAsync();
            await using AppDbContext dbContext = BuildDbContext(connection);

            Plan freePlan = await dbContext.Plans.FirstAsync(plan => plan.Key == "free");
            Plan professionalPlan = await dbContext.Plans.FirstAsync(plan => plan.Key == "professional");
            string userId = "user-latest-plan";
            DateTime baseTime = DateTime.UtcNow;

            dbContext.UserPlanAssignments.Add(new UserPlanAssignment
            {
                UserId = userId,
                PlanId = freePlan.PlanId,
                AssignedUtc = baseTime.AddMinutes(-5),
                AssignedBy = "seed"
            });
            dbContext.UserPlanAssignments.Add(new UserPlanAssignment
            {
                UserId = userId,
                PlanId = professionalPlan.PlanId,
                AssignedUtc = baseTime,
                AssignedBy = "seed"
            });
            await dbContext.SaveChangesAsync();

            UserEntitlementStore store = new(dbContext, new TestClock(baseTime.AddMinutes(1)));

            UserEntitlement first = await store.GetOrCreateAsync(userId);
            UserEntitlement second = await store.GetOrCreateAsync(userId);
            UserEntitlement third = await store.GetOrCreateAsync(userId);

            Assert.Equal(UserEntitlementDefaults.ProfessionalPlanKey, first.PlanKey);
            Assert.Equal(UserEntitlementDefaults.ProfessionalPlanKey, second.PlanKey);
            Assert.Equal(UserEntitlementDefaults.ProfessionalPlanKey, third.PlanKey);
            Assert.Equal(UserEntitlementDefaults.PROFESSIONAL_MONTHLY_TOKEN_BUDGET, third.AiMonthlyTokenBudget);
        }

        [Fact]
        public async Task GetOrCreateAsync_WhenPlanChanges_ResetsUsageAndPeriodStart()
        {
            await using SqliteConnection connection = new("DataSource=:memory:");
            await connection.OpenAsync();
            await using AppDbContext dbContext = BuildDbContext(connection);

            Plan freePlan = await dbContext.Plans.FirstAsync(plan => plan.Key == "free");
            Plan professionalPlan = await dbContext.Plans.FirstAsync(plan => plan.Key == "professional");
            string userId = "user-plan-change-reset";
            DateTimeOffset oldPeriodStart = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
            DateTime now = DateTime.UtcNow;
            DateTimeOffset expectedNow = new DateTimeOffset(DateTime.SpecifyKind(now, DateTimeKind.Utc));

            dbContext.UserEntitlements.Add(new UserEntitlement
            {
                UserId = userId,
                PlanKey = UserEntitlementDefaults.FreePlanKey,
                SubscriptionStatus = UserEntitlementDefaults.ActiveSubscriptionStatus,
                CreatedAt = oldPeriodStart,
                AiMonthlyTokenBudget = UserEntitlementDefaults.FREE_MONTHLY_TOKEN_BUDGET,
                AiTokensUsedThisPeriod = 12345,
                PeriodStartUtc = oldPeriodStart,
                UpdatedUtc = oldPeriodStart
            });

            dbContext.UserPlanAssignments.Add(new UserPlanAssignment
            {
                UserId = userId,
                PlanId = freePlan.PlanId,
                AssignedUtc = now.AddMinutes(-10),
                AssignedBy = "seed"
            });
            dbContext.UserPlanAssignments.Add(new UserPlanAssignment
            {
                UserId = userId,
                PlanId = professionalPlan.PlanId,
                AssignedUtc = now,
                AssignedBy = "seed"
            });
            await dbContext.SaveChangesAsync();

            UserEntitlementStore store = new(dbContext, new TestClock(now));

            UserEntitlement entitlement = await store.GetOrCreateAsync(userId);

            Assert.Equal(UserEntitlementDefaults.ProfessionalPlanKey, entitlement.PlanKey);
            Assert.Equal(UserEntitlementDefaults.PROFESSIONAL_MONTHLY_TOKEN_BUDGET, entitlement.AiMonthlyTokenBudget);
            Assert.Equal(0, entitlement.AiTokensUsedThisPeriod);
            Assert.True(entitlement.PeriodStartUtc >= expectedNow.AddSeconds(-1));
            Assert.True(entitlement.PeriodStartUtc <= expectedNow.AddSeconds(1));
            Assert.NotEqual(oldPeriodStart, entitlement.PeriodStartUtc);
        }

        [Fact]
        public async Task GetOrCreateAsync_KeepsStripePlan_WhenNoUserPlanAssignmentExists()
        {
            await using SqliteConnection connection = new("DataSource=:memory:");
            await connection.OpenAsync();
            await using AppDbContext dbContext = BuildDbContext(connection);

            string userId = "user-stripe-standard-no-assignment";
            DateTimeOffset now = DateTimeOffset.UtcNow;

            dbContext.UserEntitlements.Add(new UserEntitlement
            {
                UserId = userId,
                PlanKey = UserEntitlementDefaults.StandardPlanKey,
                SubscriptionStatus = "active",
                CreatedAt = now,
                AiMonthlyTokenBudget = UserEntitlementDefaults.STANDARD_MONTHLY_TOKEN_BUDGET,
                AiTokensUsedThisPeriod = 0,
                PeriodStartUtc = now,
                UpdatedUtc = now
            });
            await dbContext.SaveChangesAsync();

            int assignmentCount = await dbContext.UserPlanAssignments.CountAsync(item => item.UserId == userId);
            Assert.Equal(0, assignmentCount);

            UserEntitlementStore store = new(dbContext, new TestClock(now.UtcDateTime));
            UserEntitlement entitlement = await store.GetOrCreateAsync(userId);

            Assert.Equal(UserEntitlementDefaults.StandardPlanKey, entitlement.PlanKey);
        }

        [Fact]
        public async Task GetOrCreateAsync_ManualAssignmentOverridesStoredEntitlementPlan()
        {
            await using SqliteConnection connection = new("DataSource=:memory:");
            await connection.OpenAsync();
            await using AppDbContext dbContext = BuildDbContext(connection);

            Plan professionalPlan = await dbContext.Plans.FirstAsync(plan => plan.Key == "professional");
            string userId = "user-standard-with-manual-pro-assignment";
            DateTimeOffset now = DateTimeOffset.UtcNow;

            dbContext.UserEntitlements.Add(new UserEntitlement
            {
                UserId = userId,
                PlanKey = UserEntitlementDefaults.StandardPlanKey,
                SubscriptionStatus = "active",
                CreatedAt = now,
                AiMonthlyTokenBudget = UserEntitlementDefaults.STANDARD_MONTHLY_TOKEN_BUDGET,
                AiTokensUsedThisPeriod = 123,
                PeriodStartUtc = now.AddDays(-3),
                UpdatedUtc = now.AddDays(-1)
            });

            dbContext.UserPlanAssignments.Add(new UserPlanAssignment
            {
                UserId = userId,
                PlanId = professionalPlan.PlanId,
                AssignedUtc = now.UtcDateTime,
                AssignedBy = "admin"
            });

            await dbContext.SaveChangesAsync();

            UserEntitlementStore store = new(dbContext, new TestClock(now.UtcDateTime));
            UserEntitlement entitlement = await store.GetOrCreateAsync(userId);

            Assert.Equal(UserEntitlementDefaults.ProfessionalPlanKey, entitlement.PlanKey);
            Assert.Equal(UserEntitlementDefaults.PROFESSIONAL_MONTHLY_TOKEN_BUDGET, entitlement.AiMonthlyTokenBudget);
            Assert.Equal(0, entitlement.AiTokensUsedThisPeriod);

            UserEntitlement? persisted = await dbContext.UserEntitlements
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.UserId == userId);
            Assert.NotNull(persisted);
            Assert.Equal(UserEntitlementDefaults.ProfessionalPlanKey, persisted!.PlanKey);
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

            public TestClock(DateTimeOffset utcNow)
                : this(utcNow.UtcDateTime)
            {
            }

            public DateTime UtcNow => _utcNow;
        }
    }
}
