using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using WriterApp.Application.Documents;
using WriterApp.Data;
using WriterApp.Data.Documents;
using Xunit;

namespace WriterApp.Tests
{
    public sealed class ProjectGoalsServiceTests
    {
        [Fact]
        public async Task TrackPageDelta_IsIdempotentPerEventKey()
        {
            await using SqliteConnection connection = new("Filename=:memory:");
            await connection.OpenAsync();

            DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;

            await using AppDbContext db = new(options);
            await db.Database.EnsureCreatedAsync();

            ProjectRecord project = await SeedProjectWithLinkedSectionAsync(db, "user-1");
            IConfiguration configuration = BuildGoalsConfiguration(enabled: true);
            ProjectWordCountService wordCounts = new(db);
            ProjectGoalsService service = new(db, wordCounts, configuration);

            PageRecord before = await db.Pages.AsNoTracking().FirstAsync();
            PageRecord after = new()
            {
                Id = before.Id,
                DocumentId = before.DocumentId,
                SectionId = before.SectionId,
                Title = before.Title,
                Content = "<p>One two three four.</p>",
                OrderIndex = before.OrderIndex,
                CreatedAt = before.CreatedAt,
                UpdatedAt = before.UpdatedAt.AddSeconds(1)
            };

            await wordCounts.RefreshProjectAsync(project.Id, CancellationToken.None);
            await service.TrackPageDeltaAsync(before, after, "event-1", CancellationToken.None);
            await service.TrackPageDeltaAsync(before, after, "event-1", CancellationToken.None);

            ProjectProgressDailyRecord row = await db.ProjectProgressDaily.FirstAsync();
            Assert.Equal(2, row.WordsDelta);
            Assert.Equal(1, await db.ProjectProgressEvents.CountAsync());
        }

        [Fact]
        public async Task GetDashboard_ComputesStreakFromDailyTarget()
        {
            await using SqliteConnection connection = new("Filename=:memory:");
            await connection.OpenAsync();

            DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;

            await using AppDbContext db = new(options);
            await db.Database.EnsureCreatedAsync();

            ProjectRecord project = await SeedProjectWithLinkedSectionAsync(db, "user-2");
            DateTimeOffset now = DateTimeOffset.UtcNow;
            DateOnly today = DateOnly.FromDateTime(now.UtcDateTime);
            DateOnly yesterday = today.AddDays(-1);
            DateOnly twoDaysAgo = today.AddDays(-2);

            db.ProjectGoals.Add(new ProjectGoalRecord
            {
                ProjectId = project.Id,
                DailyTargetWords = 100,
                WeeklyTargetWords = 700,
                Timezone = "UTC",
                UpdatedUtc = now
            });

            db.ProjectProgressDaily.Add(new ProjectProgressDailyRecord
            {
                ProjectId = project.Id,
                Date = twoDaysAgo.ToString("yyyy-MM-dd"),
                WordsDelta = 60,
                UpdatedUtc = now
            });
            db.ProjectProgressDaily.Add(new ProjectProgressDailyRecord
            {
                ProjectId = project.Id,
                Date = yesterday.ToString("yyyy-MM-dd"),
                WordsDelta = 120,
                UpdatedUtc = now
            });
            db.ProjectProgressDaily.Add(new ProjectProgressDailyRecord
            {
                ProjectId = project.Id,
                Date = today.ToString("yyyy-MM-dd"),
                WordsDelta = 110,
                UpdatedUtc = now
            });
            await db.SaveChangesAsync();

            IConfiguration configuration = BuildGoalsConfiguration(enabled: true);
            ProjectGoalsService service = new(db, new ProjectWordCountService(db), configuration);

            ProjectProgressDashboardDto? dashboard = await service.GetDashboardAsync("user-2", project.Id, CancellationToken.None);
            Assert.NotNull(dashboard);
            Assert.Equal(2, dashboard!.StreakCount);
            Assert.Equal(110, dashboard.TodayWords);
            Assert.Equal(290, dashboard.ThisWeekWords);
        }

        [Fact]
        public async Task CreateMilestone_AutoCompletesWhenTargetWordsMet()
        {
            await using SqliteConnection connection = new("Filename=:memory:");
            await connection.OpenAsync();

            DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;

            await using AppDbContext db = new(options);
            await db.Database.EnsureCreatedAsync();

            string userId = "user-3";
            ProjectRecord project = new()
            {
                Id = Guid.NewGuid(),
                OwnerUserId = userId,
                Title = "Project",
                CreatedUtc = DateTimeOffset.UtcNow,
                UpdatedUtc = DateTimeOffset.UtcNow
            };
            db.Projects.Add(project);
            db.ProjectNodes.Add(new ProjectNodeRecord
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                ParentId = null,
                NodeType = ProjectNodeType.Scene,
                Title = "Root scene",
                OrderIndex = 0,
                WordCountCache = 1200,
                UpdatedUtc = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();

            IConfiguration configuration = BuildGoalsConfiguration(enabled: true);
            ProjectGoalsService service = new(db, new ProjectWordCountService(db), configuration);

            ProjectMilestoneDto? milestone = await service.CreateMilestoneAsync(
                userId,
                project.Id,
                new ProjectMilestoneCreateRequest("Finish draft", 1000, null),
                CancellationToken.None);

            Assert.NotNull(milestone);
            Assert.Equal("completed", milestone!.Status);
            Assert.NotNull(milestone.CompletedUtc);
        }

        private static IConfiguration BuildGoalsConfiguration(bool enabled)
        {
            Dictionary<string, string?> values = new()
            {
                ["Workflow:GoalsEnabled"] = enabled ? "true" : "false"
            };

            return new ConfigurationBuilder()
                .AddInMemoryCollection(values)
                .Build();
        }

        private static async Task<ProjectRecord> SeedProjectWithLinkedSectionAsync(AppDbContext db, string userId)
        {
            DocumentRecord document = new()
            {
                Id = Guid.NewGuid(),
                OwnerUserId = userId,
                Title = "Doc",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            db.Documents.Add(document);

            SectionRecord section = new()
            {
                Id = Guid.NewGuid(),
                DocumentId = document.Id,
                Title = "Scene",
                OrderIndex = 0,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            db.Sections.Add(section);

            db.Pages.Add(new PageRecord
            {
                Id = Guid.NewGuid(),
                DocumentId = document.Id,
                SectionId = section.Id,
                Title = "Page 1",
                Content = "<p>One two.</p>",
                OrderIndex = 0,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

            ProjectRecord project = new()
            {
                Id = Guid.NewGuid(),
                OwnerUserId = userId,
                Title = "Project",
                CreatedUtc = DateTimeOffset.UtcNow,
                UpdatedUtc = DateTimeOffset.UtcNow
            };
            db.Projects.Add(project);
            db.ProjectNodes.Add(new ProjectNodeRecord
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                ParentId = null,
                NodeType = ProjectNodeType.Scene,
                Title = "Scene",
                LinkedSectionId = section.Id,
                OrderIndex = 0,
                UpdatedUtc = DateTimeOffset.UtcNow
            });

            await db.SaveChangesAsync();
            return project;
        }
    }
}
