using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using WriterApp.Application.Documents;
using WriterApp.Application.Security;
using WriterApp.Controllers;
using WriterApp.Data;
using WriterApp.Data.Documents;
using Xunit;

namespace WriterApp.Tests
{
    public sealed class ProjectNodeReorderTests
    {
        [Fact]
        public async Task ReorderChildren_AppliesNewOrder_WhenIdsMatchChildren()
        {
            await using AppDbContext db = BuildDbContext();
            SeedProject(db, out Guid projectId, out Guid chapterId, out Guid firstSceneId, out Guid secondSceneId);
            ProjectsController controller = BuildController(db);

            ProjectNodeReorderRequest request = new(new[] { secondSceneId, firstSceneId });
            ActionResult<IReadOnlyList<ProjectNodeDto>> result = await controller.ReorderChildren(
                projectId,
                chapterId,
                request,
                CancellationToken.None);

            List<ProjectNodeDto> payload = Assert.IsType<OkObjectResult>(result.Result).Value as List<ProjectNodeDto>
                ?? throw new InvalidOperationException("Expected reordered payload.");
            Assert.Equal(new[] { secondSceneId, firstSceneId }, payload.Select(item => item.Id).ToArray());

            List<ProjectNodeRecord> persisted = await db.ProjectNodes
                .Where(item => item.ProjectId == projectId && item.ParentId == chapterId)
                .OrderBy(item => item.OrderIndex)
                .ToListAsync();
            Assert.Equal(new[] { secondSceneId, firstSceneId }, persisted.Select(item => item.Id).ToArray());
            Assert.Equal(new[] { 0, 1 }, persisted.Select(item => item.OrderIndex).ToArray());
        }

        [Fact]
        public async Task ReorderChildren_ReturnsConflictProblemDetails_WhenChildSetMismatches()
        {
            await using AppDbContext db = BuildDbContext();
            SeedProject(db, out Guid projectId, out Guid chapterId, out Guid firstSceneId, out Guid secondSceneId);
            ProjectsController controller = BuildController(db);

            Guid unknownId = Guid.NewGuid();
            ProjectNodeReorderRequest request = new(new[] { firstSceneId, unknownId });
            ActionResult<IReadOnlyList<ProjectNodeDto>> result = await controller.ReorderChildren(
                projectId,
                chapterId,
                request,
                CancellationToken.None);

            ObjectResult objectResult = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(StatusCodes.Status409Conflict, objectResult.StatusCode);

            ProblemDetails problem = Assert.IsType<ProblemDetails>(objectResult.Value);
            Assert.Equal("projects.reorder.child_set_mismatch", problem.Extensions["code"]?.ToString());
            Assert.Equal("Invalid reorder request", problem.Title);
            Assert.Equal(StatusCodes.Status409Conflict, problem.Status);
            Assert.NotNull(problem.Extensions["unknownIds"]);
            Assert.NotNull(problem.Extensions["missingIds"]);
            Assert.NotNull(problem.Extensions["currentChildIds"]);
            Assert.NotNull(problem.Extensions["orderedChildIds"]);
            IReadOnlyList<Guid> missingIds = Assert.IsAssignableFrom<IReadOnlyList<Guid>>(problem.Extensions["missingIds"]);
            Assert.Contains(secondSceneId, missingIds);
        }

        private static ProjectsController BuildController(AppDbContext db)
        {
            IConfiguration config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Workflow:ProjectsEnabled"] = "true",
                    ["Workflow:GoalsEnabled"] = "false"
                })
                .Build();

            ProjectsController controller = new(
                db,
                new StubUserIdResolver(),
                new StubProjectWordCountService(),
                new StubProjectGoalsService(),
                new ProjectSceneLinkingService(db),
                new StubProjectDeletionService(),
                config,
                NullLogger<ProjectsController>.Instance);

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity())
                }
            };
            controller.ControllerContext.HttpContext.Request.Headers["X-Correlation-ID"] = "test-correlation";
            return controller;
        }

        private static AppDbContext BuildDbContext()
        {
            DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite("Filename=:memory:")
                .Options;

            AppDbContext context = new(options);
            context.Database.OpenConnection();
            context.Database.EnsureCreated();
            return context;
        }

        private static void SeedProject(
            AppDbContext db,
            out Guid projectId,
            out Guid chapterId,
            out Guid firstSceneId,
            out Guid secondSceneId)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            projectId = Guid.NewGuid();
            Guid documentId = Guid.NewGuid();
            chapterId = Guid.NewGuid();
            firstSceneId = Guid.NewGuid();
            secondSceneId = Guid.NewGuid();
            Guid sectionId = Guid.NewGuid();

            db.Projects.Add(new ProjectRecord
            {
                Id = projectId,
                OwnerUserId = "user-1",
                Title = "Project",
                CreatedUtc = now,
                UpdatedUtc = now
            });

            db.Documents.Add(new DocumentRecord
            {
                Id = documentId,
                ProjectId = projectId,
                OwnerUserId = "user-1",
                Title = "Doc",
                DocumentKind = DocumentKind.Manuscript,
                CreatedAt = now,
                UpdatedAt = now
            });

            db.Sections.Add(new SectionRecord
            {
                Id = sectionId,
                DocumentId = documentId,
                Title = "Section",
                OrderIndex = 0,
                CreatedAt = now,
                UpdatedAt = now
            });

            db.ProjectNodes.Add(new ProjectNodeRecord
            {
                Id = chapterId,
                ProjectId = projectId,
                ParentId = null,
                NodeType = ProjectNodeType.Chapter,
                Title = "Chapter",
                OrderIndex = 0,
                WordCountCache = 0,
                UpdatedUtc = now
            });

            db.ProjectNodes.Add(new ProjectNodeRecord
            {
                Id = firstSceneId,
                ProjectId = projectId,
                ParentId = chapterId,
                NodeType = ProjectNodeType.Scene,
                Title = "Scene 1",
                OrderIndex = 0,
                LinkedSectionId = sectionId,
                WordCountCache = 0,
                UpdatedUtc = now
            });

            db.ProjectNodes.Add(new ProjectNodeRecord
            {
                Id = secondSceneId,
                ProjectId = projectId,
                ParentId = chapterId,
                NodeType = ProjectNodeType.Scene,
                Title = "Scene 2",
                OrderIndex = 1,
                LinkedSectionId = sectionId,
                WordCountCache = 0,
                UpdatedUtc = now
            });

            db.SaveChanges();
        }

        private sealed class StubUserIdResolver : IUserIdResolver
        {
            public string ResolveUserId(ClaimsPrincipal user) => "user-1";
        }

        private sealed class StubProjectWordCountService : IProjectWordCountService
        {
            public Task RefreshProjectAsync(Guid projectId, CancellationToken ct) => Task.CompletedTask;

            public Task RefreshForSectionAsync(Guid sectionId, CancellationToken ct) => Task.CompletedTask;

            public Task<ProjectStatsDto?> GetProjectStatsAsync(string ownerUserId, Guid projectId, CancellationToken ct)
                => Task.FromResult<ProjectStatsDto?>(null);
        }

        private sealed class StubProjectGoalsService : IProjectGoalsService
        {
            public Task<ProjectGoalDto?> UpsertGoalAsync(string ownerUserId, Guid projectId, ProjectGoalUpdateRequest request, CancellationToken ct)
                => Task.FromResult<ProjectGoalDto?>(null);

            public Task<ProjectProgressDashboardDto?> GetDashboardAsync(string ownerUserId, Guid projectId, CancellationToken ct)
                => Task.FromResult<ProjectProgressDashboardDto?>(null);

            public Task<ProjectMilestoneDto?> CreateMilestoneAsync(string ownerUserId, Guid projectId, ProjectMilestoneCreateRequest request, CancellationToken ct)
                => Task.FromResult<ProjectMilestoneDto?>(null);

            public Task<ProjectMilestoneDto?> UpdateMilestoneAsync(string ownerUserId, Guid projectId, Guid milestoneId, ProjectMilestoneUpdateRequest request, CancellationToken ct)
                => Task.FromResult<ProjectMilestoneDto?>(null);

            public Task<bool> DeleteMilestoneAsync(string ownerUserId, Guid projectId, Guid milestoneId, CancellationToken ct)
                => Task.FromResult(false);

            public Task<WritingSessionDto?> StartSessionAsync(string ownerUserId, Guid projectId, CancellationToken ct)
                => Task.FromResult<WritingSessionDto?>(null);

            public Task<WritingSessionDto?> StopSessionAsync(string ownerUserId, Guid projectId, Guid sessionId, string? notes, CancellationToken ct)
                => Task.FromResult<WritingSessionDto?>(null);

            public Task TrackPageDeltaAsync(PageRecord? beforePage, PageRecord? afterPage, string eventKey, CancellationToken ct)
                => Task.CompletedTask;
        }

        private sealed class StubProjectDeletionService : IProjectDeletionService
        {
            public Task<ProjectDeletionResult> DeleteOwnedProjectAsync(Guid incomingId, string ownerUserId, CancellationToken ct)
                => Task.FromResult(new ProjectDeletionResult(false, null, null));
        }
    }
}
