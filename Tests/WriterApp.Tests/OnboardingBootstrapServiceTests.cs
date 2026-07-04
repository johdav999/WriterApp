using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using WriterApp.Application.AI;
using WriterApp.Application.Documents;
using WriterApp.Data;
using WriterApp.Data.Documents;
using Xunit;

namespace WriterApp.Tests
{
    public sealed class OnboardingBootstrapServiceTests
    {
        [Fact]
        public async Task CreateStarterWorkspaceForOnboarding_CreatesProjectAndFirstScene()
        {
            await using SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync();
            await using AppDbContext db = BuildDbContext(connection);
            OnboardingBootstrapService service = BuildService(db);

            OnboardingBootstrapResult result = await service.CreateStarterWorkspaceForOnboardingAsync("user-1", "Novel", CancellationToken.None);

            ProjectRecord project = await db.Projects.SingleAsync(item => item.Id == result.ProjectId);
            Assert.Equal("My Novel", project.Title);
            Assert.Equal(1, await db.ProjectNodes.CountAsync(item => item.ProjectId == project.Id && item.NodeType == ProjectNodeType.Part));
            Assert.Equal(1, await db.ProjectNodes.CountAsync(item => item.ProjectId == project.Id && item.NodeType == ProjectNodeType.Chapter));
            Assert.Equal(1, await db.ProjectNodes.CountAsync(item => item.ProjectId == project.Id && item.NodeType == ProjectNodeType.Scene));
            Assert.Equal(1, await db.Documents.CountAsync(item => item.ProjectId == project.Id && item.DocumentKind == DocumentKind.Manuscript));
            Assert.True(await db.Sections.AnyAsync());
            Assert.True(await db.Pages.AnyAsync());

            ProjectNodeRecord scene = await db.ProjectNodes.SingleAsync(item => item.Id == result.FirstSceneNodeId);
            ProjectNodeRecord chapter = await db.ProjectNodes.SingleAsync(item => item.ProjectId == project.Id && item.NodeType == ProjectNodeType.Chapter);
            ProjectNodeRecord part = await db.ProjectNodes.SingleAsync(item => item.ProjectId == project.Id && item.NodeType == ProjectNodeType.Part);
            Assert.Equal(part.Id, chapter.ParentId);
            Assert.Equal(chapter.Id, scene.ParentId);
            PageRecord page = await db.Pages.SingleAsync();
            SceneContentRecord sceneContent = await db.SceneContents.SingleAsync();
            Assert.Contains("Café", page.Content);
            Assert.Contains("their eyes met", page.Content);
            Assert.DoesNotContain("caf&#", page.Content, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("cafÃ©", page.Content, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(page.Content, sceneContent.ContentJson);
            Assert.True(OnboardingDemoSceneMetadata.IsDemoScene(scene.MetadataJson));
        }

        [Fact]
        public async Task CreateStarterWorkspaceForOnboarding_RetryDoesNotDuplicateStarterContent()
        {
            await using SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync();
            await using AppDbContext db = BuildDbContext(connection);
            OnboardingBootstrapService service = BuildService(db);

            OnboardingBootstrapResult first = await service.CreateStarterWorkspaceForOnboardingAsync("user-1", "Other", CancellationToken.None);
            OnboardingBootstrapResult second = await service.CreateStarterWorkspaceForOnboardingAsync("user-1", "Other", CancellationToken.None);

            Assert.Equal(first.ProjectId, second.ProjectId);
            Assert.Equal(first.FirstSceneNodeId, second.FirstSceneNodeId);
            Assert.Equal(1, await db.Projects.CountAsync(item => item.OwnerUserId == "user-1"));
            Assert.Equal(1, await db.Documents.CountAsync(item => item.ProjectId == first.ProjectId && item.DocumentKind == DocumentKind.Manuscript));
            Assert.Equal(1, await db.ProjectNodes.CountAsync(item => item.ProjectId == first.ProjectId && item.NodeType == ProjectNodeType.Scene));
            Assert.Equal(1, await db.ProjectNodes.CountAsync(item => item.ProjectId == first.ProjectId && item.NodeType == ProjectNodeType.Chapter));
        }

        [Fact]
        public async Task CreateStarterWorkspaceForOnboarding_DoesNotOverwriteExistingSceneText()
        {
            await using SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync();
            await using AppDbContext db = BuildDbContext(connection);
            OnboardingBootstrapService service = BuildService(db);

            OnboardingBootstrapResult first = await service.CreateStarterWorkspaceForOnboardingAsync("user-1", "Other", CancellationToken.None);
            PageRecord page = await db.Pages.SingleAsync();
            page.Content = "<p>User kept writing.</p>";
            await db.SaveChangesAsync();

            OnboardingBootstrapResult second = await service.CreateStarterWorkspaceForOnboardingAsync("user-1", "Other", CancellationToken.None);

            PageRecord reloadedPage = await db.Pages.SingleAsync(item => item.SectionId == page.SectionId);
            Assert.Equal(first.ProjectId, second.ProjectId);
            Assert.Equal("<p>User kept writing.</p>", reloadedPage.Content);
        }

        [Fact]
        public async Task CreateStarterWorkspaceForOnboarding_RecoversPartialProjectWithoutDuplicatingProject()
        {
            await using SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync();
            await using AppDbContext db = BuildDbContext(connection);
            OnboardingBootstrapService service = BuildService(db);

            Guid projectId = Guid.NewGuid();
            db.Projects.Add(new ProjectRecord
            {
                Id = projectId,
                OwnerUserId = "user-1",
                Title = "My Blog Post",
                CreatedUtc = DateTimeOffset.UtcNow,
                UpdatedUtc = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();

            OnboardingBootstrapResult result = await service.CreateStarterWorkspaceForOnboardingAsync("user-1", "Blog", CancellationToken.None);

            Assert.Equal(projectId, result.ProjectId);
            Assert.Equal(1, await db.Projects.CountAsync(item => item.OwnerUserId == "user-1"));
            Assert.Equal(2, await db.ProjectNodes.CountAsync(item => item.ProjectId == projectId && item.ParentId == null));
            Assert.Equal(1, await db.ProjectNodes.CountAsync(item => item.ProjectId == projectId && item.NodeType == ProjectNodeType.Scene));
            Assert.Equal(1, await db.Documents.CountAsync(item => item.ProjectId == projectId && item.DocumentKind == DocumentKind.Manuscript));
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

        private static OnboardingBootstrapService BuildService(AppDbContext dbContext)
        {
            return new OnboardingBootstrapService(
                dbContext,
                new ProjectSceneLinkingService(dbContext),
                new ProjectWordCountService(dbContext),
                NullLogger<OnboardingBootstrapService>.Instance);
        }
    }
}
