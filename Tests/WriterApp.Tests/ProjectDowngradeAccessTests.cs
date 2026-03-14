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
    public sealed class ProjectDowngradeAccessTests
    {
        [Fact]
        public async Task ListProjectItems_AllowsExistingProjects_ForFreeUser()
        {
            await using AppDbContext db = BuildDbContext();
            SeedProjectGraph(db, out Guid projectId, out _, out _, out _);

            ProjectsController controller = BuildController(db, PlanTier.Free);

            ActionResult<IReadOnlyList<ProjectListItemDto>> result = await controller.ListProjectItems(CancellationToken.None);

            OkObjectResult ok = Assert.IsType<OkObjectResult>(result.Result);
            IReadOnlyList<ProjectListItemDto> payload = Assert.IsAssignableFrom<IReadOnlyList<ProjectListItemDto>>(ok.Value);
            ProjectListItemDto item = Assert.Single(payload);
            Assert.Equal(projectId, item.ProjectId);
            Assert.Equal("Downgraded project", item.Title);
        }

        [Fact]
        public async Task GetTree_AllowsOpeningExistingProject_ForFreeUser()
        {
            await using AppDbContext db = BuildDbContext();
            SeedProjectGraph(db, out Guid projectId, out Guid sceneNodeId, out _, out _);

            ProjectsController controller = BuildController(db, PlanTier.Free);

            ActionResult<ProjectTreeDto> result = await controller.GetTree(projectId, CancellationToken.None);

            OkObjectResult ok = Assert.IsType<OkObjectResult>(result.Result);
            ProjectTreeDto payload = Assert.IsType<ProjectTreeDto>(ok.Value);
            Assert.Equal(projectId, payload.Project.Id);
            Assert.Contains(payload.Nodes, node => node.Id == sceneNodeId && string.Equals(node.NodeType, "scene", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task OpenScene_AllowsReadingExistingProjectContent_ForFreeUser()
        {
            await using AppDbContext db = BuildDbContext();
            SeedProjectGraph(db, out Guid projectId, out Guid sceneNodeId, out Guid documentId, out Guid sectionId);

            ProjectsController controller = BuildController(db, PlanTier.Free);

            ActionResult<ProjectSceneOpenTargetDto> result = await controller.OpenScene(projectId, sceneNodeId, CancellationToken.None);

            OkObjectResult ok = Assert.IsType<OkObjectResult>(result.Result);
            ProjectSceneOpenTargetDto payload = Assert.IsType<ProjectSceneOpenTargetDto>(ok.Value);
            Assert.Equal(projectId, payload.ProjectId);
            Assert.Equal(sceneNodeId, payload.SceneNodeId);
            Assert.Equal(documentId, payload.DocumentId);
            Assert.Equal(sectionId, payload.SectionId);
        }

        [Fact]
        public async Task CreateNode_ReturnsStructured402_WhenFreeUserAttemptsStructureEdit()
        {
            await using AppDbContext db = BuildDbContext();
            SeedProjectGraph(db, out Guid projectId, out _, out _, out _);

            ProjectsController controller = BuildController(db, PlanTier.Free);
            ProjectNodeCreateRequest request = new(null, "part", "Locked Part", null, null, null);

            ActionResult<ProjectNodeDto> result = await controller.CreateNode(projectId, request, CancellationToken.None);

            ObjectResult objectResult = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(StatusCodes.Status402PaymentRequired, objectResult.StatusCode);
            ProblemDetails problem = Assert.IsType<ProblemDetails>(objectResult.Value);
            Assert.Equal("entitlement_denied", problem.Extensions["code"]?.ToString());
            Assert.Equal("projects.structure", problem.Extensions["featureKey"]?.ToString());
            Assert.Contains("Standard", problem.Detail ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task PatchNode_AllowsRename_ForFreeUser()
        {
            await using AppDbContext db = BuildDbContext();
            SeedProjectGraph(db, out Guid projectId, out Guid sceneNodeId, out _, out _);

            ProjectsController controller = BuildController(db, PlanTier.Free);
            ProjectNodeRecord existing = await db.ProjectNodes.SingleAsync(item => item.Id == sceneNodeId);
            ProjectNodePatchRequest request = new(
                "Renamed scene",
                existing.ParentId,
                existing.LinkedSectionId,
                existing.MetadataJson,
                existing.NodeType.ToString());

            ActionResult<ProjectNodeDto> result = await controller.PatchNode(projectId, sceneNodeId, request, CancellationToken.None);

            OkObjectResult ok = Assert.IsType<OkObjectResult>(result.Result);
            ProjectNodeDto payload = Assert.IsType<ProjectNodeDto>(ok.Value);
            Assert.Equal("Renamed scene", payload.Title);

            ProjectNodeRecord updated = await db.ProjectNodes.SingleAsync(item => item.Id == sceneNodeId);
            Assert.Equal("Renamed scene", updated.Title);
        }

        private static ProjectsController BuildController(AppDbContext db, PlanTier userTier)
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
                new StubEntitlementService(userTier),
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

        private static void SeedProjectGraph(
            AppDbContext db,
            out Guid projectId,
            out Guid sceneNodeId,
            out Guid documentId,
            out Guid sectionId)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            projectId = Guid.NewGuid();
            documentId = Guid.NewGuid();
            sectionId = Guid.NewGuid();
            Guid partNodeId = Guid.NewGuid();
            Guid chapterNodeId = Guid.NewGuid();
            sceneNodeId = Guid.NewGuid();
            Guid pageId = Guid.NewGuid();

            db.Projects.Add(new ProjectRecord
            {
                Id = projectId,
                OwnerUserId = "user-1",
                Title = "Downgraded project",
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
                UpdatedAt = now,
                CreatedAtUnixSeconds = now.ToUnixTimeSeconds(),
                UpdatedAtUnixSeconds = now.ToUnixTimeSeconds()
            });

            db.Sections.Add(new SectionRecord
            {
                Id = sectionId,
                DocumentId = documentId,
                Title = "Scene section",
                OrderIndex = 0,
                CreatedAt = now,
                UpdatedAt = now
            });

            db.Pages.Add(new PageRecord
            {
                Id = pageId,
                DocumentId = documentId,
                SectionId = sectionId,
                Title = "Page 1",
                Content = "Existing premium project content.",
                OrderIndex = 0,
                CreatedAt = now,
                UpdatedAt = now
            });

            db.ProjectNodes.AddRange(
                new ProjectNodeRecord
                {
                    Id = partNodeId,
                    ProjectId = projectId,
                    ParentId = null,
                    NodeType = ProjectNodeType.Part,
                    Title = "Part I",
                    OrderIndex = 0,
                    WordCountCache = 0,
                    UpdatedUtc = now
                },
                new ProjectNodeRecord
                {
                    Id = chapterNodeId,
                    ProjectId = projectId,
                    ParentId = partNodeId,
                    NodeType = ProjectNodeType.Chapter,
                    Title = "Chapter 1",
                    OrderIndex = 0,
                    WordCountCache = 0,
                    UpdatedUtc = now
                },
                new ProjectNodeRecord
                {
                    Id = sceneNodeId,
                    ProjectId = projectId,
                    ParentId = chapterNodeId,
                    NodeType = ProjectNodeType.Scene,
                    Title = "Scene 1",
                    OrderIndex = 0,
                    LinkedSectionId = sectionId,
                    WordCountCache = 123,
                    UpdatedUtc = now
                });

            db.SaveChanges();
        }

        private sealed class StubUserIdResolver : IUserIdResolver
        {
            public string ResolveUserId(ClaimsPrincipal user) => "user-1";
        }

        private sealed class StubEntitlementService : IEntitlementService
        {
            private readonly PlanTier _userTier;

            public StubEntitlementService(PlanTier userTier)
            {
                _userTier = userTier;
            }

            public Task<UserEntitlements> GetEntitlementsAsync(string userId)
            {
                string planKey = _userTier switch
                {
                    PlanTier.Professional => "professional",
                    PlanTier.Standard => "standard",
                    _ => "free"
                };

                return Task.FromResult(new UserEntitlements(
                    userId,
                    planKey,
                    planKey,
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)));
            }

            public PlanTier GetUserTier(UserEntitlements entitlements) => _userTier;

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
