using System;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using WriterApp.Application.Subscriptions;
using WriterApp.Data;
using WriterApp.Data.Subscriptions;
using Xunit;

namespace WriterApp.Tests
{
    public sealed class EntitlementServiceTests
    {
        [Fact]
        public async Task DefaultFreePlanIsReturned()
        {
            await using SqliteConnection connection = new("DataSource=:memory:");
            await connection.OpenAsync();
            AppDbContext dbContext = BuildDbContext(connection);

            IEntitlementService service = BuildService(dbContext);

            UserEntitlements entitlements = await service.GetEntitlementsAsync("user-1");

            Assert.Equal("free", entitlements.PlanKey);
            Assert.False(await service.HasAsync("user-1", "ai.enabled"));
            Assert.Equal(0, await service.GetIntAsync("user-1", "ai.monthly_tokens"));
        }

        [Fact]
        public async Task AssignedStandardPlanIsReturned()
        {
            await using SqliteConnection connection = new("DataSource=:memory:");
            await connection.OpenAsync();
            AppDbContext dbContext = BuildDbContext(connection);

            Plan? standardPlan = await dbContext.Plans.FirstOrDefaultAsync(plan => plan.Key == "standard");
            Assert.NotNull(standardPlan);

            dbContext.UserPlanAssignments.Add(new UserPlanAssignment
            {
                UserId = "user-2",
                PlanId = standardPlan!.PlanId,
                AssignedUtc = DateTime.UtcNow,
                AssignedBy = "seed"
            });
            await dbContext.SaveChangesAsync();

            IEntitlementService service = BuildService(dbContext);

            UserEntitlements entitlements = await service.GetEntitlementsAsync("user-2");

            Assert.Equal("standard", entitlements.PlanKey);
            Assert.True(await service.HasAsync("user-2", "ai.enabled"));
            Assert.Equal(200000, await service.GetIntAsync("user-2", "ai.monthly_tokens"));
        }

        [Fact]
        public async Task MissingEntitlementReturnsNullOrFalse()
        {
            await using SqliteConnection connection = new("DataSource=:memory:");
            await connection.OpenAsync();
            AppDbContext dbContext = BuildDbContext(connection);

            IEntitlementService service = BuildService(dbContext);

            Assert.False(await service.HasAsync("user-3", "missing.key"));
            Assert.Null(await service.GetIntAsync("user-3", "missing.key"));
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

        private static IEntitlementService BuildService(AppDbContext dbContext)
        {
            IMemoryCache cache = new MemoryCache(new MemoryCacheOptions());
            IPlanRepository planRepository = new PlanRepository(dbContext);
            return new EntitlementService(planRepository, cache);
        }
    }
}
