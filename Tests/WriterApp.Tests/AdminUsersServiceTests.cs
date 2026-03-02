using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using WriterApp.Application.Billing;
using WriterApp.Application.Security;
using WriterApp.Application.Subscriptions;
using WriterApp.Application.Usage;
using WriterApp.Application.Users;
using WriterApp.Data;
using WriterApp.Data.Subscriptions;
using WriterApp.Shared;
using Xunit;

namespace WriterApp.Tests
{
    public sealed class AdminUsersServiceTests
    {
        [Fact]
        public void AdminApiFlag_DefaultsToDisabled()
        {
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>())
                .Build();

            Assert.False(AdminPlanOverrideAccess.IsAdminApiEnabled(configuration));
        }

        [Fact]
        public void AdminApi_NonAdminPrincipal_IsBlocked()
        {
            ClaimsPrincipal principal = new(new ClaimsIdentity(
                new[] { new Claim("roles", "User") },
                authenticationType: "Test"));

            Assert.False(AdminPlanOverrideAccess.IsAuthorized(principal));
        }

        [Fact]
        public async Task QueryUsers_Paging_ReturnsTwentyAndTotalCount()
        {
            await using SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync();
            await using AppDbContext dbContext = BuildDbContext(connection);
            SeedUsers(dbContext, count: 25);

            AdminUsersService service = BuildService(dbContext);

            AdminUserListResponseDto response = await service.QueryUsersAsync(
                page: 1,
                pageSize: 20,
                q: null,
                planKey: null,
                overrideOnly: false,
                subscriptionStatus: null,
                tokensLeftLt: null,
                tokensLeftGt: null,
                sort: "createdAt asc");

            Assert.Equal(20, response.Items.Count);
            Assert.Equal(25, response.TotalCount);
            Assert.Equal(1, response.Page);
            Assert.Equal(20, response.PageSize);
        }

        [Fact]
        public async Task QueryUsers_Filters_WorkForPlanQueryAndOverrideOnly()
        {
            await using SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync();
            await using AppDbContext dbContext = BuildDbContext(connection);
            SeedUsers(dbContext, count: 6);

            AdminUsersService service = BuildService(dbContext);

            AdminUserListResponseDto response = await service.QueryUsersAsync(
                page: 1,
                pageSize: 20,
                q: "user-0",
                planKey: "Standard",
                overrideOnly: true,
                subscriptionStatus: null,
                tokensLeftLt: null,
                tokensLeftGt: null,
                sort: "createdAt asc");

            Assert.Single(response.Items);
            Assert.Equal("user-0", response.Items[0].UserId);
            Assert.Equal("Standard", response.Items[0].PlanKey);
            Assert.True(response.Items[0].IsManuallyOverridden);
        }

        [Fact]
        public async Task PlanOverride_UpdatesSnapshotImmediately()
        {
            await using SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync();
            await using AppDbContext dbContext = BuildDbContext(connection);
            SeedUsers(dbContext, count: 1);

            AdminUsersService service = BuildService(dbContext);

            AdminPlanOverrideResponse overrideResponse = await service.SetPlanOverrideAsync(
                "user-0",
                new AdminSetPlanOverrideRequest("Pro", "admin test"),
                "admin-1",
                "admin@example.com");

            AdminUserDetailDto? detail = await service.GetUserAsync("user-0");

            Assert.NotNull(detail);
            Assert.Equal(UserEntitlementDefaults.ProfessionalPlanKey, overrideResponse.ResolvedPlanKey);
            Assert.Equal(UserEntitlementDefaults.ProfessionalPlanKey, detail!.PlanKey);
            Assert.True(detail.IsManuallyOverridden);
        }

        [Fact]
        public async Task PlanOverride_Succeeds_WhenAuditTableMissing()
        {
            await using SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync();
            await using AppDbContext dbContext = BuildDbContext(connection);
            SeedUsers(dbContext, count: 1);

            // Simulate schema drift where audit table was not created yet.
            dbContext.Database.ExecuteSqlRaw("""DROP TABLE IF EXISTS "AdminAuditEvents";""");

            AdminUsersService service = BuildService(dbContext);

            AdminPlanOverrideResponse response = await service.SetPlanOverrideAsync(
                "user-0",
                new AdminSetPlanOverrideRequest("Pro", "missing audit table"),
                "admin-1",
                "admin@example.com");

            AdminUserDetailDto? detail = await service.GetUserAsync("user-0");

            Assert.Equal(UserEntitlementDefaults.ProfessionalPlanKey, response.ResolvedPlanKey);
            Assert.NotNull(detail);
            Assert.True(detail!.IsManuallyOverridden);
            Assert.Equal(UserEntitlementDefaults.ProfessionalPlanKey, detail.PlanKey);
        }

        [Fact]
        public async Task AdminAuditWrite_CanceledToken_BubblesOperationCanceledException()
        {
            await using SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync();
            await using AppDbContext dbContext = BuildDbContext(connection);

            AdminAuditService auditService = new(dbContext, NullLogger<AdminAuditService>.Instance);
            using CancellationTokenSource cts = new();
            cts.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                auditService.WriteAsync(
                    "admin-1",
                    "admin@example.com",
                    "SetPlanOverride",
                    "user-1",
                    "user@example.com",
                    new { reason = "cancelled" },
                    cts.Token));
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

        private static AdminUsersService BuildService(AppDbContext dbContext)
        {
            IUserEntitlementStore entitlementStore = new UserEntitlementStore(
                dbContext,
                new FixedClock(DateTime.UtcNow),
                NullLogger<UserEntitlementStore>.Instance);
            IEntitlementService entitlementService = new EntitlementService(
                new PlanRepository(dbContext),
                entitlementStore,
                new MemoryCache(new MemoryCacheOptions()));
            AdminPlanOverrideService overrideService = new(
                dbContext,
                entitlementStore,
                entitlementService,
                new StubStripePriceResolver(),
                NullLogger<AdminPlanOverrideService>.Instance);

            return new AdminUsersService(
                dbContext,
                overrideService,
                NullLogger<AdminUsersService>.Instance);
        }

        private static void SeedUsers(AppDbContext dbContext, int count)
        {
            DateTime now = DateTime.UtcNow;
            Plan standardPlan = dbContext.Plans.First(plan => plan.Key == "standard");
            Plan professionalPlan = dbContext.Plans.First(plan => plan.Key == "professional");

            for (int i = 0; i < count; i++)
            {
                string userId = $"user-{i}";
                dbContext.UserProfiles.Add(new UserProfile
                {
                    UserId = userId,
                    DisplayName = $"user{i}@example.com",
                    CreatedUtc = now.AddMinutes(-i),
                    UpdatedUtc = now.AddMinutes(-i),
                    HasOnboarded = true
                });

                bool isStandard = i % 2 == 0;
                dbContext.UserEntitlements.Add(new UserEntitlement
                {
                    UserId = userId,
                    PlanKey = isStandard ? UserEntitlementDefaults.StandardPlanKey : UserEntitlementDefaults.ProfessionalPlanKey,
                    SubscriptionStatus = UserEntitlementDefaults.ActiveSubscriptionStatus,
                    CreatedAt = now.AddMinutes(-i),
                    AiMonthlyTokenBudget = isStandard
                        ? UserEntitlementDefaults.StandardMonthlyTokenBudget
                        : UserEntitlementDefaults.ProfessionalMonthlyTokenBudget,
                    AiTokensUsedThisPeriod = 1000,
                    PeriodStartUtc = now.AddDays(-2),
                    UpdatedUtc = now.AddMinutes(-i)
                });

                if (isStandard)
                {
                    dbContext.UserPlanAssignments.Add(new UserPlanAssignment
                    {
                        UserId = userId,
                        PlanId = standardPlan.PlanId,
                        AssignedUtc = now.AddMinutes(-i),
                        AssignedBy = "seed"
                    });
                }
                else
                {
                    dbContext.UserPlanAssignments.Add(new UserPlanAssignment
                    {
                        UserId = userId,
                        PlanId = professionalPlan.PlanId,
                        AssignedUtc = now.AddMinutes(-i),
                        AssignedBy = "seed"
                    });
                }
            }

            dbContext.SaveChanges();
        }

        private sealed class StubStripePriceResolver : IStripePriceResolver
        {
            public string ResolvePriceId(string planKey, out string normalizedPlanKey)
            {
                normalizedPlanKey = UserEntitlementDefaults.NormalizePlanKey(planKey);
                return normalizedPlanKey switch
                {
                    UserEntitlementDefaults.StandardPlanKey => "price_standard",
                    UserEntitlementDefaults.ProfessionalPlanKey => "price_pro",
                    _ => "price_free"
                };
            }

            public string? ResolvePlanKey(string priceId)
            {
                return priceId switch
                {
                    "price_standard" => "standard",
                    "price_pro" => "pro",
                    _ => null
                };
            }
        }

        private sealed class FixedClock : IClock
        {
            public FixedClock(DateTime utcNow)
            {
                UtcNow = utcNow;
            }

            public DateTime UtcNow { get; }
        }
    }
}
