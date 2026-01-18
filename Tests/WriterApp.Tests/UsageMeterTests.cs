using System;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WriterApp.Application.Usage;
using WriterApp.Data;
using WriterApp.Data.Usage;
using Xunit;

namespace WriterApp.Tests
{
    public sealed class UsageMeterTests
    {
        [Fact]
        public async Task RecordEvents_IncrementsAggregates()
        {
            await using SqliteConnection connection = new("DataSource=:memory:");
            await connection.OpenAsync();
            AppDbContext dbContext = BuildDbContext(connection);

            TestClock clock = new(new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc));
            UsageMeter meter = new(dbContext, clock);

            UsageEvent first = new()
            {
                UserId = "user-1",
                Kind = "ai.text",
                Provider = "mock",
                Model = "mock",
                InputTokens = 10,
                OutputTokens = 5,
                CostMicros = 100,
                TimestampUtc = clock.UtcNow
            };

            UsageEvent second = new()
            {
                UserId = "user-1",
                Kind = "ai.text",
                Provider = "mock",
                Model = "mock",
                InputTokens = 3,
                OutputTokens = 2,
                CostMicros = 50,
                TimestampUtc = clock.UtcNow
            };

            await meter.RecordAsync(first);
            await meter.RecordAsync(second);

            UsageSnapshot snapshot = await meter.GetCurrentPeriodAsync("user-1", "ai.text");
            Assert.Equal(13, snapshot.TotalInputTokens);
            Assert.Equal(7, snapshot.TotalOutputTokens);
            Assert.Equal(150, snapshot.TotalCostMicros);

            UsageSnapshot totalSnapshot = await meter.GetCurrentPeriodAsync("user-1", "ai.total");
            Assert.Equal(13, totalSnapshot.TotalInputTokens);
            Assert.Equal(7, totalSnapshot.TotalOutputTokens);
            Assert.Equal(150, totalSnapshot.TotalCostMicros);
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
            public TestClock(DateTime utcNow)
            {
                UtcNow = utcNow;
            }

            public DateTime UtcNow { get; }
        }
    }
}
