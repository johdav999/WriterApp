using System;
using System.Collections.Generic;
using System.Security.Claims;
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
using WriterApp.Data;
using WriterApp.Data.Subscriptions;
using Xunit;

namespace WriterApp.Tests
{
    public sealed class AdminPlanOverrideTests
    {
        [Fact]
        public void PlanOverrideAccess_DisabledByDefault_ReturnsFalse()
        {
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>())
                .Build();

            bool enabled = AdminPlanOverrideAccess.IsEnabled(configuration);

            Assert.False(enabled);
        }

        [Fact]
        public void PlanOverrideAccess_UnauthorizedPrincipal_ReturnsFalse()
        {
            ClaimsPrincipal principal = new(new ClaimsIdentity(
                new[] { new Claim("roles", "User") },
                authenticationType: "Test"));

            bool allowed = AdminPlanOverrideAccess.IsAuthorized(principal);

            Assert.False(allowed);
        }

        [Fact]
        public void AdminApiAccess_BootstrapOidMatch_ReturnsTrue()
        {
            const string oid = "00000000-0000-0000-151c-3ba2d7110bfa";
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["BOOTSTRAP_ADMIN_ENABLED"] = "true",
                    ["BOOTSTRAP_ADMIN_OID"] = oid
                })
                .Build();

            ClaimsPrincipal principal = new(new ClaimsIdentity(
                new[]
                {
                    new Claim("oid", oid)
                },
                authenticationType: "Test"));

            bool allowed = AdminPlanOverrideAccess.IsAuthorized(principal, configuration);

            Assert.True(allowed);
        }

        [Fact]
        public void AdminApiAccess_BootstrapOidMismatch_ReturnsFalse()
        {
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["BOOTSTRAP_ADMIN_ENABLED"] = "true",
                    ["BOOTSTRAP_ADMIN_OID"] = "00000000-0000-0000-151c-3ba2d7110bfa"
                })
                .Build();

            ClaimsPrincipal principal = new(new ClaimsIdentity(
                new[]
                {
                    new Claim("oid", "00000000-0000-0000-1111-111111111111")
                },
                authenticationType: "Test"));

            bool allowed = AdminPlanOverrideAccess.IsAuthorized(principal, configuration);

            Assert.False(allowed);
        }

        [Fact]
        public async Task SetOverride_ToPro_RefreshesResolvedEntitlementsImmediately()
        {
            await using SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync();
            await using AppDbContext dbContext = BuildDbContext(connection);

            const string userId = "user-override-pro";
            SeedStripeDerivedStandardEntitlement(dbContext, userId, "price_standard");

            AdminPlanOverrideService service = BuildService(dbContext);
            IEntitlementService entitlementService = BuildEntitlementService(dbContext);

            UserEntitlements before = await entitlementService.GetEntitlementsAsync(userId);
            Assert.Equal("standard", before.PlanKey);

            var response = await service.SetOverride(
                userId,
                "pro",
                "admin-user-id",
                "admin@example.com",
                "staging test");

            UserEntitlements after = await entitlementService.GetEntitlementsAsync(userId);

            Assert.Equal(UserEntitlementDefaults.ProfessionalPlanKey, response.ResolvedPlanKey);
            Assert.True(response.IsManuallyOverridden);
            Assert.Equal("professional", after.PlanKey);
        }

        [Fact]
        public async Task ClearOverride_RevertsToStripeDerivedPlan()
        {
            await using SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync();
            await using AppDbContext dbContext = BuildDbContext(connection);

            const string userId = "user-clear-override";
            SeedStripeDerivedStandardEntitlement(dbContext, userId, "price_standard");

            AdminPlanOverrideService service = BuildService(dbContext);
            IEntitlementService entitlementService = BuildEntitlementService(dbContext);

            await service.SetOverride(
                userId,
                "pro",
                "admin-user-id",
                "admin@example.com",
                "promote for testing");

            var response = await service.SetOverride(
                userId,
                null,
                "admin-user-id",
                "admin@example.com",
                "clear override");

            UserEntitlements after = await entitlementService.GetEntitlementsAsync(userId);

            Assert.Equal(UserEntitlementDefaults.StandardPlanKey, response.ResolvedPlanKey);
            Assert.False(response.IsManuallyOverridden);
            Assert.Equal("standard", after.PlanKey);
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

        private static void SeedStripeDerivedStandardEntitlement(AppDbContext dbContext, string userId, string stripePriceId)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            dbContext.UserEntitlements.Add(new UserEntitlement
            {
                UserId = userId,
                PlanKey = UserEntitlementDefaults.StandardPlanKey,
                SubscriptionStatus = UserEntitlementDefaults.ActiveSubscriptionStatus,
                CreatedAt = now.AddDays(-5),
                AiMonthlyTokenBudget = UserEntitlementDefaults.StandardMonthlyTokenBudget,
                AiTokensUsedThisPeriod = 100,
                PeriodStartUtc = now.AddDays(-1),
                StripePriceId = stripePriceId,
                UpdatedUtc = now.AddHours(-1)
            });
            dbContext.SaveChanges();
        }

        private static AdminPlanOverrideService BuildService(AppDbContext dbContext)
        {
            IUserEntitlementStore entitlementStore = BuildEntitlementStore(dbContext);
            IEntitlementService entitlementService = BuildEntitlementService(dbContext, entitlementStore);
            return new AdminPlanOverrideService(
                dbContext,
                entitlementStore,
                entitlementService,
                new StubStripePriceResolver(),
                NullLogger<AdminPlanOverrideService>.Instance);
        }

        private static IEntitlementService BuildEntitlementService(AppDbContext dbContext, IUserEntitlementStore? entitlementStore = null)
        {
            IMemoryCache cache = new MemoryCache(new MemoryCacheOptions());
            return new EntitlementService(
                new PlanRepository(dbContext),
                entitlementStore ?? BuildEntitlementStore(dbContext),
                cache);
        }

        private static IUserEntitlementStore BuildEntitlementStore(AppDbContext dbContext)
        {
            return new UserEntitlementStore(
                dbContext,
                new FixedClock(DateTime.UtcNow),
                NullLogger<UserEntitlementStore>.Instance);
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
