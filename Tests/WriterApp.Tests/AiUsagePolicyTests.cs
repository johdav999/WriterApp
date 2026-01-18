using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using WriterApp.AI.Abstractions;
using WriterApp.AI.Core;
using WriterApp.Application.Subscriptions;
using WriterApp.Application.Usage;
using WriterApp.Data;
using WriterApp.Data.Subscriptions;
using WriterApp.Data.Usage;
using Xunit;

namespace WriterApp.Tests
{
    public sealed class AiUsagePolicyTests
    {
        [Fact]
        public async Task FreeUser_IsBlocked()
        {
            await using SqliteConnection connection = new("DataSource=:memory:");
            await connection.OpenAsync();
            AppDbContext dbContext = BuildDbContext(connection);

            IEntitlementService entitlementService = BuildEntitlementService(dbContext);
            TestClock clock = new(new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc));
            IUsageMeter usageMeter = new UsageMeter(dbContext, clock);
            IAiUsagePolicy policy = BuildPolicy(entitlementService, usageMeter, clock, "user-free");

            AiUsageDecision decision = await policy.EvaluateAsync(new TestBillingProvider(), "rewrite");

            Assert.False(decision.Allowed);
            Assert.Equal("ai.disabled", decision.ErrorCode);
        }

        [Fact]
        public async Task StandardUser_BlockedAfterQuotaExceeded()
        {
            await using SqliteConnection connection = new("DataSource=:memory:");
            await connection.OpenAsync();
            AppDbContext dbContext = BuildDbContext(connection);

            Plan? standardPlan = await dbContext.Plans.FirstOrDefaultAsync(plan => plan.Key == "standard");
            Assert.NotNull(standardPlan);

            dbContext.UserPlanAssignments.Add(new UserPlanAssignment
            {
                UserId = "user-standard",
                PlanId = standardPlan!.PlanId,
                AssignedUtc = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                AssignedBy = "seed"
            });
            await dbContext.SaveChangesAsync();

            IEntitlementService entitlementService = BuildEntitlementService(dbContext);
            TestClock clock = new(new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc));
            UsageMeter usageMeter = new(dbContext, clock);
            IAiUsagePolicy policy = BuildPolicy(entitlementService, usageMeter, clock, "user-standard");

            AiUsageDecision allowed = await policy.EvaluateAsync(new TestBillingProvider(), "rewrite");
            Assert.True(allowed.Allowed);

            UsageEvent usageEvent = new()
            {
                UserId = "user-standard",
                Kind = "ai.text",
                Provider = "mock",
                Model = "mock",
                InputTokens = 200000,
                OutputTokens = 0,
                TimestampUtc = clock.UtcNow
            };

            await usageMeter.RecordAsync(usageEvent);

            AiUsageDecision blocked = await policy.EvaluateAsync(new TestBillingProvider(), "rewrite");
            Assert.False(blocked.Allowed);
            Assert.Equal("ai.quota_exceeded", blocked.ErrorCode);
        }

        [Fact]
        public async Task RateLimit_BlocksAfterLimitExceeded()
        {
            await using SqliteConnection connection = new("DataSource=:memory:");
            await connection.OpenAsync();
            AppDbContext dbContext = BuildDbContext(connection);

            Plan? standardPlan = await dbContext.Plans.FirstOrDefaultAsync(plan => plan.Key == "standard");
            Assert.NotNull(standardPlan);

            dbContext.UserPlanAssignments.Add(new UserPlanAssignment
            {
                UserId = "user-rate",
                PlanId = standardPlan!.PlanId,
                AssignedUtc = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                AssignedBy = "seed"
            });
            await dbContext.SaveChangesAsync();

            IEntitlementService entitlementService = BuildEntitlementService(dbContext);
            TestClock clock = new(new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc));
            IUsageMeter usageMeter = new UsageMeter(dbContext, clock);
            IAiUsagePolicy policy = BuildPolicy(entitlementService, usageMeter, clock, "user-rate", requestsPerMinute: 1);

            AiUsageDecision first = await policy.EvaluateAsync(new TestBillingProvider(), "rewrite");
            AiUsageDecision second = await policy.EvaluateAsync(new TestBillingProvider(), "rewrite");

            Assert.True(first.Allowed);
            Assert.False(second.Allowed);
            Assert.Equal("ai.rate_limited", second.ErrorCode);
        }

        [Fact]
        public async Task DailyCap_BlocksWhenExceeded()
        {
            await using SqliteConnection connection = new("DataSource=:memory:");
            await connection.OpenAsync();
            AppDbContext dbContext = BuildDbContext(connection);

            Plan? standardPlan = await dbContext.Plans.FirstOrDefaultAsync(plan => plan.Key == "standard");
            Assert.NotNull(standardPlan);

            dbContext.UserPlanAssignments.Add(new UserPlanAssignment
            {
                UserId = "user-daily",
                PlanId = standardPlan!.PlanId,
                AssignedUtc = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                AssignedBy = "seed"
            });
            dbContext.PlanEntitlements.Add(new PlanEntitlement
            {
                PlanId = standardPlan.PlanId,
                Key = "ai.daily_tokens_cap",
                Value = "5"
            });
            await dbContext.SaveChangesAsync();

            IEntitlementService entitlementService = BuildEntitlementService(dbContext);
            TestClock clock = new(new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc));
            UsageMeter usageMeter = new(dbContext, clock);
            IAiUsagePolicy policy = BuildPolicy(entitlementService, usageMeter, clock, "user-daily");

            await usageMeter.RecordAsync(new UsageEvent
            {
                UserId = "user-daily",
                Kind = "ai.text",
                Provider = "mock",
                Model = "mock",
                InputTokens = 5,
                OutputTokens = 0,
                TimestampUtc = clock.UtcNow
            });

            AiUsageDecision blocked = await policy.EvaluateAsync(new TestBillingProvider(), "rewrite");
            Assert.False(blocked.Allowed);
            Assert.Equal("ai.quota_exceeded", blocked.ErrorCode);
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

        private static IEntitlementService BuildEntitlementService(AppDbContext dbContext)
        {
            IMemoryCache cache = new MemoryCache(new MemoryCacheOptions());
            IPlanRepository planRepository = new PlanRepository(dbContext);
            return new EntitlementService(planRepository, cache);
        }

        private static IAiUsagePolicy BuildPolicy(
            IEntitlementService entitlementService,
            IUsageMeter usageMeter,
            IClock clock,
            string userId,
            int? requestsPerMinute = null)
        {
            DefaultHttpContext httpContext = new();
            ClaimsIdentity identity = new(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId)
            }, "test");
            httpContext.User = new ClaimsPrincipal(identity);

            HttpContextAccessor accessor = new()
            {
                HttpContext = httpContext
            };

            WriterAiOptions options = new()
            {
                Enabled = true,
                RateLimiting = new WriterAiRateLimitOptions
                {
                    RequestsPerMinute = requestsPerMinute ?? 100
                }
            };
            IMemoryCache cache = new MemoryCache(new MemoryCacheOptions());
            return new AiUsagePolicy(accessor, entitlementService, usageMeter, cache, clock, Options.Create(options));
        }

        private sealed class TestClock : IClock
        {
            public TestClock(DateTime utcNow)
            {
                UtcNow = utcNow;
            }

            public DateTime UtcNow { get; }
        }

        private sealed class TestBillingProvider : IAiProvider, IAiBillingProvider
        {
            public string ProviderId => "test";
            public AiProviderCapabilities Capabilities => new(false, false);
            public bool RequiresEntitlement => true;
            public bool IsBillable => true;

            public Task<AiResult> ExecuteAsync(AiRequest request, CancellationToken ct)
            {
                throw new NotSupportedException("Test provider does not execute requests.");
            }
        }
    }
}
