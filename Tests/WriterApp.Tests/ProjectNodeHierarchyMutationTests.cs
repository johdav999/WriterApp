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
using WriterApp.Application.Subscriptions;
using WriterApp.Controllers;
using WriterApp.Data;
using WriterApp.Data.Documents;
using Xunit;

namespace WriterApp.Tests
{
    public sealed class ProjectNodeHierarchyMutationTests
    {
        [Fact]
        public async Task CreateNode_AllowsRootChapter()
        {
            await using AppDbContext db = BuildDbContext();
            SeedProjectSkeleton(db, out Guid projectId, out _, out _, out _);
            ProjectsController controller = BuildController(db);

            ActionResult<ProjectNodeDto> result = await controller.CreateNode(
                projectId,
                new ProjectNodeCreateRequest(null, "chapter", "Prelude", null, null, null),
                CancellationToken.None);

            ProjectNodeDto payload = Assert.IsType<OkObjectResult>(result.Result).Value as ProjectNodeDto
                ?? throw new InvalidOperationException("Expected created node.");
            Assert.Equal("chapter", payload.NodeType);
            Assert.Null(payload.ParentId);
        }

        [Fact]
        public async Task CreateNode_AllowsChapterUnderPart()
        {
            await using AppDbContext db = BuildDbContext();
            SeedProjectSkeleton(db, out Guid projectId, out Guid partId, out _, out _);
            ProjectsController controller = BuildController(db);

            ActionResult<ProjectNodeDto> result = await controller.CreateNode(
                projectId,
                new ProjectNodeCreateRequest(partId, "chapter", "Chapter 1", null, null, null),
                CancellationToken.None);

            ProjectNodeDto payload = Assert.IsType<OkObjectResult>(result.Result).Value as ProjectNodeDto
                ?? throw new InvalidOperationException("Expected created node.");
            Assert.Equal(partId, payload.ParentId);
            Assert.Equal("chapter", payload.NodeType);
        }

        [Fact]
        public async Task CreateNode_RejectsSceneUnderPart_WithoutMutatingData()
        {
            await using AppDbContext db = BuildDbContext();
            SeedProjectSkeleton(db, out Guid projectId, out Guid partId, out _, out _);
            ProjectsController controller = BuildController(db);
            int beforeCount = await db.ProjectNodes.CountAsync(node => node.ProjectId == projectId);

            ActionResult<ProjectNodeDto> result = await controller.CreateNode(
                projectId,
                new ProjectNodeCreateRequest(partId, "scene", "Scene 1", null, null, null),
                CancellationToken.None);

            BadRequestObjectResult badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Contains("cannot be placed under part", badRequest.Value?.ToString(), StringComparison.OrdinalIgnoreCase);
            Assert.Equal(beforeCount, await db.ProjectNodes.CountAsync(node => node.ProjectId == projectId));
        }

        [Fact]
        public async Task CreateNode_RejectsUnknownNodeType_WithoutMutatingData()
        {
            await using AppDbContext db = BuildDbContext();
            SeedProjectSkeleton(db, out Guid projectId, out _, out _, out _);
            ProjectsController controller = BuildController(db);
            int beforeCount = await db.ProjectNodes.CountAsync(node => node.ProjectId == projectId);

            ActionResult<ProjectNodeDto> result = await controller.CreateNode(
                projectId,
                new ProjectNodeCreateRequest(null, "unknown", "Mystery", null, null, null),
                CancellationToken.None);

            BadRequestObjectResult badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Contains("Node type is required", badRequest.Value?.ToString(), StringComparison.OrdinalIgnoreCase);
            Assert.Equal(beforeCount, await db.ProjectNodes.CountAsync(node => node.ProjectId == projectId));
        }

        [Fact]
        public async Task PatchNode_RejectsSelfParent_WithoutMutatingData()
        {
            await using AppDbContext db = BuildDbContext();
            SeedProjectSkeleton(db, out Guid projectId, out _, out Guid chapterId, out Guid sceneId);
            ProjectsController controller = BuildController(db);

            ActionResult<ProjectNodeDto> result = await controller.PatchNode(
                projectId,
                chapterId,
                new ProjectNodePatchRequest("Chapter 1", chapterId, null, null, "chapter"),
                CancellationToken.None);

            Assert.IsType<BadRequestObjectResult>(result.Result);
            ProjectNodeRecord persisted = await db.ProjectNodes.SingleAsync(node => node.Id == chapterId);
            Assert.Null(persisted.ParentId);
            Assert.Equal(ProjectNodeType.Chapter, persisted.NodeType);
            Assert.Equal(sceneId, await db.ProjectNodes.Where(node => node.ParentId == chapterId).Select(node => node.Id).SingleAsync());
        }

        [Fact]
        public async Task PatchNode_RejectsDescendantCycle_WithoutMutatingData()
        {
            await using AppDbContext db = BuildDbContext();
            SeedProjectSkeleton(db, out Guid projectId, out Guid partId, out Guid chapterId, out _);
            ProjectsController controller = BuildController(db);

            ActionResult<ProjectNodeDto> result = await controller.PatchNode(
                projectId,
                partId,
                new ProjectNodePatchRequest("Act I", chapterId, null, null, "part"),
                CancellationToken.None);

            BadRequestObjectResult badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Contains("descendants", badRequest.Value?.ToString(), StringComparison.OrdinalIgnoreCase);
            ProjectNodeRecord persisted = await db.ProjectNodes.SingleAsync(node => node.Id == partId);
            Assert.Null(persisted.ParentId);
        }

        [Fact]
        public async Task PatchNode_RejectsCrossProjectParent_WithoutMutatingData()
        {
            await using AppDbContext db = BuildDbContext();
            SeedProjectSkeleton(db, out Guid projectId, out _, out _, out Guid sceneId);
            Guid otherProjectId = SeedOtherProjectWithChapter(db, out Guid otherChapterId);
            ProjectsController controller = BuildController(db);

            ActionResult<ProjectNodeDto> result = await controller.PatchNode(
                projectId,
                sceneId,
                new ProjectNodePatchRequest("Scene 1", otherChapterId, null, null, "scene"),
                CancellationToken.None);

            BadRequestObjectResult badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Contains("not found in this project", badRequest.Value?.ToString(), StringComparison.OrdinalIgnoreCase);
            ProjectNodeRecord persisted = await db.ProjectNodes.SingleAsync(node => node.Id == sceneId);
            Assert.NotEqual(otherProjectId, persisted.ProjectId);
        }

        [Fact]
        public async Task PatchNode_RejectsInvalidReparentTarget_WithoutMutatingData()
        {
            await using AppDbContext db = BuildDbContext();
            SeedProjectSkeleton(db, out Guid projectId, out Guid partId, out Guid chapterId, out Guid sceneId);
            ProjectsController controller = BuildController(db);

            ActionResult<ProjectNodeDto> result = await controller.PatchNode(
                projectId,
                sceneId,
                new ProjectNodePatchRequest("Scene 1", partId, null, null, "scene"),
                CancellationToken.None);

            BadRequestObjectResult badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Contains("cannot be placed under part", badRequest.Value?.ToString(), StringComparison.OrdinalIgnoreCase);
            ProjectNodeRecord persisted = await db.ProjectNodes.SingleAsync(node => node.Id == sceneId);
            Assert.Equal(chapterId, persisted.ParentId);
        }

        [Fact]
        public async Task PatchNode_AllowsValidReparent()
        {
            await using AppDbContext db = BuildDbContext();
            SeedProjectSkeleton(db, out Guid projectId, out Guid partId, out Guid firstChapterId, out Guid sceneId);
            Guid secondChapterId = Guid.NewGuid();
            db.ProjectNodes.Add(new ProjectNodeRecord
            {
                Id = secondChapterId,
                ProjectId = projectId,
                ParentId = partId,
                NodeType = ProjectNodeType.Chapter,
                Title = "Chapter 2",
                OrderIndex = 1,
                WordCountCache = 0,
                UpdatedUtc = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();

            ProjectsController controller = BuildController(db);
            ActionResult<ProjectNodeDto> result = await controller.PatchNode(
                projectId,
                sceneId,
                new ProjectNodePatchRequest("Scene 1", secondChapterId, null, null, "scene"),
                CancellationToken.None);

            ProjectNodeDto payload = Assert.IsType<OkObjectResult>(result.Result).Value as ProjectNodeDto
                ?? throw new InvalidOperationException("Expected patched node.");
            Assert.Equal(secondChapterId, payload.ParentId);

            ProjectNodeRecord persisted = await db.ProjectNodes.SingleAsync(node => node.Id == sceneId);
            Assert.Equal(secondChapterId, persisted.ParentId);
            Assert.Equal(ProjectNodeType.Scene, persisted.NodeType);
            Assert.Empty(await db.ProjectNodes.Where(node => node.ParentId == firstChapterId && node.Id == sceneId).ToListAsync());
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
                new StubEntitlementService(),
                config,
                NullLogger<ProjectsController>.Instance);

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity())
                }
            };

            return controller;
        }

        private static void SeedProjectSkeleton(AppDbContext db, out Guid projectId, out Guid partId, out Guid chapterId, out Guid sceneId)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            projectId = Guid.NewGuid();
            Guid documentId = Guid.NewGuid();
            Guid sectionId = Guid.NewGuid();
            partId = Guid.NewGuid();
            chapterId = Guid.NewGuid();
            sceneId = Guid.NewGuid();

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
                Title = "Manuscript",
                DocumentKind = DocumentKind.Manuscript,
                CreatedAt = now,
                UpdatedAt = now
            });

            db.Sections.Add(new SectionRecord
            {
                Id = sectionId,
                DocumentId = documentId,
                Title = "Scene 1",
                OrderIndex = 0,
                CreatedAt = now,
                UpdatedAt = now
            });

            db.ProjectNodes.Add(new ProjectNodeRecord
            {
                Id = partId,
                ProjectId = projectId,
                ParentId = null,
                NodeType = ProjectNodeType.Part,
                Title = "Act I",
                OrderIndex = 0,
                WordCountCache = 0,
                UpdatedUtc = now
            });

            db.ProjectNodes.Add(new ProjectNodeRecord
            {
                Id = chapterId,
                ProjectId = projectId,
                ParentId = partId,
                NodeType = ProjectNodeType.Chapter,
                Title = "Chapter 1",
                OrderIndex = 0,
                WordCountCache = 0,
                UpdatedUtc = now
            });

            db.ProjectNodes.Add(new ProjectNodeRecord
            {
                Id = sceneId,
                ProjectId = projectId,
                ParentId = chapterId,
                NodeType = ProjectNodeType.Scene,
                Title = "Scene 1",
                OrderIndex = 0,
                LinkedSectionId = sectionId,
                WordCountCache = 0,
                UpdatedUtc = now
            });

            db.SaveChanges();
        }

        private static Guid SeedOtherProjectWithChapter(AppDbContext db, out Guid chapterId)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            Guid projectId = Guid.NewGuid();
            chapterId = Guid.NewGuid();

            db.Projects.Add(new ProjectRecord
            {
                Id = projectId,
                OwnerUserId = "user-1",
                Title = "Other Project",
                CreatedUtc = now,
                UpdatedUtc = now
            });

            db.ProjectNodes.Add(new ProjectNodeRecord
            {
                Id = chapterId,
                ProjectId = projectId,
                ParentId = null,
                NodeType = ProjectNodeType.Chapter,
                Title = "Other Chapter",
                OrderIndex = 0,
                WordCountCache = 0,
                UpdatedUtc = now
            });

            db.SaveChanges();
            return projectId;
        }

        private sealed class StubUserIdResolver : IUserIdResolver
        {
            public string ResolveUserId(ClaimsPrincipal user) => "user-1";
        }

        private sealed class StubEntitlementService : IEntitlementService
        {
            public Task<UserEntitlements> GetEntitlementsAsync(string userId)
            {
                return Task.FromResult(new UserEntitlements(
                    userId,
                    "professional",
                    "professional",
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)));
            }

            public PlanTier GetUserTier(UserEntitlements entitlements) => PlanTier.Professional;

            public Task<bool> HasAsync(string userId, string entitlementKey) => Task.FromResult(true);

            public Task<int?> GetIntAsync(string userId, string entitlementKey) => Task.FromResult<int?>(null);

            public void InvalidateForUser(string userId)
            {
            }
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

            public Task<ProjectDeletionResult> DeleteOwnedProjectInExistingTransactionAsync(Guid incomingId, string ownerUserId, CancellationToken ct)
                => Task.FromResult(new ProjectDeletionResult(false, null, null));
        }
    }
}
