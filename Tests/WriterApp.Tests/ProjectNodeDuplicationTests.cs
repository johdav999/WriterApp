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
    public sealed class ProjectNodeDuplicationTests
    {
        [Fact]
        public async Task DuplicateScene_CopiesLinkedSectionContentAndMetadata()
        {
            await using AppDbContext db = BuildDbContext();
            SeedSceneProject(db, out Guid projectId, out Guid chapterId, out Guid sourceSceneId, out Guid sourceSectionId, out Guid sourcePageId);

            ProjectsController controller = BuildController(db);
            ActionResult<ProjectNodeDuplicateResponse> result = await controller.DuplicateNode(
                projectId,
                sourceSceneId,
                new ProjectNodeDuplicateRequest(null),
                CancellationToken.None);

            ProjectNodeDuplicateResponse payload = Assert.IsType<OkObjectResult>(result.Result).Value as ProjectNodeDuplicateResponse
                ?? throw new InvalidOperationException("Expected duplicate payload.");
            ProjectNodeDto duplicateRoot = payload.CreatedNodes.Single(node => node.Id == payload.RootNodeId);

            Assert.Equal(chapterId, duplicateRoot.ParentId);
            Assert.Equal("Scene 1 (Copy)", duplicateRoot.Title);
            Assert.Equal(1, duplicateRoot.OrderIndex);
            Assert.True(duplicateRoot.LinkedSectionId.HasValue);
            Assert.NotEqual(sourceSectionId, duplicateRoot.LinkedSectionId.Value);

            SectionRecord copiedSection = await db.Sections.SingleAsync(section => section.Id == duplicateRoot.LinkedSectionId.Value);
            List<PageRecord> copiedPages = await db.Pages
                .Where(page => page.SectionId == copiedSection.Id)
                .OrderBy(page => page.OrderIndex)
                .ToListAsync();
            Assert.Single(copiedPages);
            Assert.Equal("Scene page content", copiedPages[0].Content);

            SectionSceneCardRecord copiedCard = await db.SectionSceneCards.SingleAsync(card => card.SectionId == copiedSection.Id);
            Assert.Equal("Purpose", copiedCard.NarrativePurpose);

            SectionNoteRecord copiedSectionNote = await db.SectionNotes.SingleAsync(note => note.SectionId == copiedSection.Id);
            Assert.Equal("Section notes", copiedSectionNote.NotesText);

            PageAnnotationRecord copiedAnnotation = await db.PageAnnotations.SingleAsync(annotation => annotation.PageId == copiedPages[0].Id);
            Assert.True(string.Equals("todo", copiedAnnotation.Kind, StringComparison.OrdinalIgnoreCase));
            Assert.Equal("Remember this", copiedAnnotation.Content);

            Assert.NotNull(await db.PageNotes.FindAsync(copiedPages[0].Id));
            Assert.NotNull(await db.Pages.FindAsync(sourcePageId));
        }

        [Fact]
        public async Task DuplicateChapter_DeepCopiesSceneSubtreeAndOrdering()
        {
            await using AppDbContext db = BuildDbContext();
            SeedChapterProject(db, out Guid projectId, out Guid chapterId, out List<Guid> sourceSceneIds, out List<Guid> sourceSectionIds);

            ProjectsController controller = BuildController(db);
            ActionResult<ProjectNodeDuplicateResponse> result = await controller.DuplicateNode(
                projectId,
                chapterId,
                new ProjectNodeDuplicateRequest(null),
                CancellationToken.None);

            ProjectNodeDuplicateResponse payload = Assert.IsType<OkObjectResult>(result.Result).Value as ProjectNodeDuplicateResponse
                ?? throw new InvalidOperationException("Expected duplicate payload.");

            ProjectNodeDto duplicatedChapter = payload.CreatedNodes.Single(node => node.Id == payload.RootNodeId);
            Assert.Equal("Chapter 1 (Copy)", duplicatedChapter.Title);
            Assert.Equal(1, duplicatedChapter.OrderIndex);

            List<ProjectNodeDto> duplicatedScenes = payload.CreatedNodes
                .Where(node => string.Equals(node.NodeType, "scene", StringComparison.OrdinalIgnoreCase))
                .OrderBy(node => node.OrderIndex)
                .ToList();
            Assert.Equal(2, duplicatedScenes.Count);
            Assert.All(duplicatedScenes, scene => Assert.Equal(duplicatedChapter.Id, scene.ParentId));
            Assert.Equal(new[] { 0, 1 }, duplicatedScenes.Select(scene => scene.OrderIndex).ToArray());
            Assert.All(duplicatedScenes, scene => Assert.True(scene.LinkedSectionId.HasValue));
            Assert.DoesNotContain(duplicatedScenes.Select(scene => scene.LinkedSectionId!.Value), sectionId => sourceSectionIds.Contains(sectionId));
            Assert.Equal(duplicatedScenes.Count, duplicatedScenes.Select(scene => scene.LinkedSectionId!.Value).Distinct().Count());

            List<string> copiedContents = new();
            foreach (ProjectNodeDto scene in duplicatedScenes)
            {
                Guid sectionId = scene.LinkedSectionId!.Value;
                PageRecord page = await db.Pages
                    .Where(item => item.SectionId == sectionId)
                    .OrderBy(item => item.OrderIndex)
                    .FirstAsync();
                copiedContents.Add(page.Content);
            }

            Assert.Contains("Scene A text", copiedContents);
            Assert.Contains("Scene B text", copiedContents);
            Assert.Equal(2, sourceSceneIds.Count);
        }

        [Fact]
        public async Task DuplicateScene_UsesCopySuffixIncrementWhenCopyAlreadyExists()
        {
            await using AppDbContext db = BuildDbContext();
            SeedSceneProject(db, out Guid projectId, out Guid chapterId, out Guid sourceSceneId, out _, out _);

            db.ProjectNodes.Add(new ProjectNodeRecord
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                ParentId = chapterId,
                NodeType = ProjectNodeType.Scene,
                Title = "Scene 1 (Copy)",
                OrderIndex = 1,
                WordCountCache = 0,
                UpdatedUtc = DateTimeOffset.UtcNow
            });
            db.SaveChanges();

            ProjectsController controller = BuildController(db);
            ActionResult<ProjectNodeDuplicateResponse> result = await controller.DuplicateNode(
                projectId,
                sourceSceneId,
                new ProjectNodeDuplicateRequest(null),
                CancellationToken.None);

            ProjectNodeDuplicateResponse payload = Assert.IsType<OkObjectResult>(result.Result).Value as ProjectNodeDuplicateResponse
                ?? throw new InvalidOperationException("Expected duplicate payload.");
            ProjectNodeDto duplicateRoot = payload.CreatedNodes.Single(node => node.Id == payload.RootNodeId);
            Assert.Equal("Scene 1 (Copy 2)", duplicateRoot.Title);
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

        private static void SeedSceneProject(
            AppDbContext db,
            out Guid projectId,
            out Guid chapterId,
            out Guid sourceSceneId,
            out Guid sourceSectionId,
            out Guid sourcePageId)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            projectId = Guid.NewGuid();
            Guid manuscriptId = Guid.NewGuid();
            chapterId = Guid.NewGuid();
            sourceSceneId = Guid.NewGuid();
            sourceSectionId = Guid.NewGuid();
            sourcePageId = Guid.NewGuid();

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
                Id = manuscriptId,
                ProjectId = projectId,
                OwnerUserId = "user-1",
                Title = "Manuscript",
                DocumentKind = DocumentKind.Manuscript,
                CreatedAt = now,
                UpdatedAt = now
            });

            db.Sections.Add(new SectionRecord
            {
                Id = sourceSectionId,
                DocumentId = manuscriptId,
                Title = "Scene 1",
                OrderIndex = 0,
                CreatedAt = now,
                UpdatedAt = now
            });

            db.Pages.Add(new PageRecord
            {
                Id = sourcePageId,
                DocumentId = manuscriptId,
                SectionId = sourceSectionId,
                Title = "Page 1",
                Content = "Scene page content",
                OrderIndex = 0,
                CreatedAt = now,
                UpdatedAt = now
            });

            db.PageNotes.Add(new PageNoteRecord
            {
                PageId = sourcePageId,
                Notes = "Legacy page note",
                UpdatedAt = now
            });

            db.PageAnnotations.Add(new PageAnnotationRecord
            {
                Id = Guid.NewGuid(),
                DocumentId = manuscriptId,
                PageId = sourcePageId,
                Kind = "todo",
                Status = "open",
                AnchorFrom = 0,
                AnchorTo = 5,
                AnchorText = "Scene",
                Content = "Remember this",
                AuthorUserId = "user-1",
                CreatedAt = now
            });

            db.SectionSceneCards.Add(new SectionSceneCardRecord
            {
                SectionId = sourceSectionId,
                NarrativePurpose = "Purpose",
                UpdatedUtc = now
            });

            db.SectionNotes.Add(new SectionNoteRecord
            {
                SectionId = sourceSectionId,
                NotesText = "Section notes",
                UpdatedAtUtc = now
            });

            db.ProjectNodes.Add(new ProjectNodeRecord
            {
                Id = chapterId,
                ProjectId = projectId,
                ParentId = null,
                NodeType = ProjectNodeType.Chapter,
                Title = "Chapter 1",
                OrderIndex = 0,
                WordCountCache = 100,
                UpdatedUtc = now
            });

            db.ProjectNodes.Add(new ProjectNodeRecord
            {
                Id = sourceSceneId,
                ProjectId = projectId,
                ParentId = chapterId,
                NodeType = ProjectNodeType.Scene,
                Title = "Scene 1",
                OrderIndex = 0,
                LinkedSectionId = sourceSectionId,
                WordCountCache = 100,
                UpdatedUtc = now
            });

            db.SaveChanges();
        }

        private static void SeedChapterProject(
            AppDbContext db,
            out Guid projectId,
            out Guid chapterId,
            out List<Guid> sourceSceneIds,
            out List<Guid> sourceSectionIds)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            projectId = Guid.NewGuid();
            Guid manuscriptId = Guid.NewGuid();
            chapterId = Guid.NewGuid();
            sourceSceneIds = new List<Guid>();
            sourceSectionIds = new List<Guid>();

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
                Id = manuscriptId,
                ProjectId = projectId,
                OwnerUserId = "user-1",
                Title = "Manuscript",
                DocumentKind = DocumentKind.Manuscript,
                CreatedAt = now,
                UpdatedAt = now
            });

            db.ProjectNodes.Add(new ProjectNodeRecord
            {
                Id = chapterId,
                ProjectId = projectId,
                ParentId = null,
                NodeType = ProjectNodeType.Chapter,
                Title = "Chapter 1",
                OrderIndex = 0,
                WordCountCache = 200,
                UpdatedUtc = now
            });

            (string title, string content, int order)[] scenes =
            {
                ("Scene A", "Scene A text", 0),
                ("Scene B", "Scene B text", 1)
            };

            foreach ((string title, string content, int order) in scenes)
            {
                Guid sectionId = Guid.NewGuid();
                Guid pageId = Guid.NewGuid();
                Guid sceneId = Guid.NewGuid();
                sourceSectionIds.Add(sectionId);
                sourceSceneIds.Add(sceneId);

                db.Sections.Add(new SectionRecord
                {
                    Id = sectionId,
                    DocumentId = manuscriptId,
                    Title = title,
                    OrderIndex = order,
                    CreatedAt = now,
                    UpdatedAt = now
                });

                db.Pages.Add(new PageRecord
                {
                    Id = pageId,
                    DocumentId = manuscriptId,
                    SectionId = sectionId,
                    Title = "Page 1",
                    Content = content,
                    OrderIndex = 0,
                    CreatedAt = now,
                    UpdatedAt = now
                });

                db.ProjectNodes.Add(new ProjectNodeRecord
                {
                    Id = sceneId,
                    ProjectId = projectId,
                    ParentId = chapterId,
                    NodeType = ProjectNodeType.Scene,
                    Title = title,
                    OrderIndex = order,
                    LinkedSectionId = sectionId,
                    WordCountCache = 100,
                    UpdatedUtc = now
                });
            }

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
