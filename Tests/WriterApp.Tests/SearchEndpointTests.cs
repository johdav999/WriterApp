using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using WriterApp.Application.Documents;
using WriterApp.Application.Search;
using WriterApp.Application.Security;
using WriterApp.Controllers;
using WriterApp.Data;
using WriterApp.Data.Documents;
using Xunit;

namespace WriterApp.Tests
{
    public sealed class SearchEndpointTests
    {
        [Fact]
        public async Task Search_WithProjectId_ReturnsScenePageContentMatches()
        {
            await using AppDbContext db = BuildDbContext();
            (Guid projectId, DocumentRecord document, SectionRecord section, PageRecord page) = SeedScenePage(db);

            SearchIndexService searchIndex = new(
                db,
                NullLogger<SearchIndexService>.Instance,
                new SearchIndexBackfillQueue());

            await searchIndex.UpsertDocumentAsync(document, CancellationToken.None);
            await searchIndex.UpsertSectionAsync(section, CancellationToken.None);
            await searchIndex.UpsertPageAsync(page, CancellationToken.None);

            SearchController controller = new(
                searchIndex,
                new StubUserIdResolver(),
                NullLogger<SearchController>.Instance,
                new StubHostEnvironment());

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity())
                }
            };

            ActionResult<IReadOnlyList<SearchResultDto>> action = await controller.Search(
                q: "Test",
                projectId: projectId,
                includeMeta: true,
                limit: 100,
                ct: CancellationToken.None);

            OkObjectResult ok = Assert.IsType<OkObjectResult>(action.Result);
            IReadOnlyList<SearchResultDto> results = Assert.IsAssignableFrom<IReadOnlyList<SearchResultDto>>(ok.Value);
            SearchResultDto hit = Assert.Single(results);
            Assert.Equal(document.Id, hit.DocumentId);
            Assert.Equal(section.Id, hit.SectionId);
            Assert.Equal(page.Id, hit.PageId);
            Assert.Equal("page", hit.EntityType);
            Assert.Equal("content", hit.MatchKind);
            Assert.Contains("Test", hit.Snippet, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Search_WhenBackfillFailsDueToSchemaDrift_ReturnsOkEmpty()
        {
            await using AppDbContext db = BuildDbContext();
            (Guid projectId, _, _, _) = SeedScenePage(db);
            BreakSectionSceneCardsSchema(db);

            SearchIndexService searchIndex = new(
                db,
                NullLogger<SearchIndexService>.Instance,
                new SearchIndexBackfillQueue());

            SearchController controller = new(
                searchIndex,
                new StubUserIdResolver(),
                NullLogger<SearchController>.Instance,
                new StubHostEnvironment());

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity())
                }
            };

            ActionResult<IReadOnlyList<SearchResultDto>> action = await controller.Search(
                q: "Test",
                projectId: projectId,
                includeMeta: true,
                limit: 100,
                ct: CancellationToken.None);

            OkObjectResult ok = Assert.IsType<OkObjectResult>(action.Result);
            IReadOnlyList<SearchResultDto> results = Assert.IsAssignableFrom<IReadOnlyList<SearchResultDto>>(ok.Value);
            Assert.Empty(results);
        }

        [Fact]
        public async Task Search_WithUppercaseDocumentIdAndLowercaseIndexDocumentId_ReturnsMatch()
        {
            await using AppDbContext db = BuildDbContext();
            (Guid projectId, DocumentRecord document, SectionRecord section, PageRecord page) = SeedScenePage(db);

            db.Database.ExecuteSqlRaw("""
UPDATE "Documents"
SET "Id" = upper("Id"), "ProjectId" = upper("ProjectId")
WHERE "Id" = {0};
""", document.Id.ToString("D"));

            db.Database.ExecuteSqlRaw("""
INSERT INTO "SearchIndexEntries" (
    "EntityType","EntityId","DocumentId","SectionId","PageId","Title","Content","UpdatedAt"
) VALUES (
    'page', {0}, {1}, {2}, {3}, 'Page 1', 'Contains Test marker', '2026-01-01T00:00:00.0000000Z'
);
""",
                page.Id.ToString("D").ToLowerInvariant(),
                document.Id.ToString("D").ToLowerInvariant(),
                section.Id.ToString("D").ToLowerInvariant(),
                page.Id.ToString("D").ToLowerInvariant());

            SearchIndexService searchIndex = new(
                db,
                NullLogger<SearchIndexService>.Instance,
                new SearchIndexBackfillQueue());

            SearchController controller = new(
                searchIndex,
                new StubUserIdResolver(),
                NullLogger<SearchController>.Instance,
                new StubHostEnvironment());

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity())
                }
            };

            ActionResult<IReadOnlyList<SearchResultDto>> action = await controller.Search(
                q: "Test",
                projectId: projectId,
                includeMeta: false,
                limit: 100,
                ct: CancellationToken.None);

            OkObjectResult ok = Assert.IsType<OkObjectResult>(action.Result);
            IReadOnlyList<SearchResultDto> results = Assert.IsAssignableFrom<IReadOnlyList<SearchResultDto>>(ok.Value);
            SearchResultDto hit = Assert.Single(results);
            Assert.Equal("page", hit.EntityType);
            Assert.Equal(page.Id, hit.PageId);
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

        private static (Guid ProjectId, DocumentRecord Document, SectionRecord Section, PageRecord Page) SeedScenePage(AppDbContext db)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            Guid projectId = Guid.NewGuid();
            Guid documentId = Guid.NewGuid();
            Guid sectionId = Guid.NewGuid();
            Guid pageId = Guid.NewGuid();

            ProjectRecord project = new()
            {
                Id = projectId,
                OwnerUserId = "user-1",
                Title = "Project",
                CreatedUtc = now,
                UpdatedUtc = now
            };

            DocumentRecord document = new()
            {
                Id = documentId,
                ProjectId = projectId,
                OwnerUserId = "user-1",
                Title = "Draft",
                DocumentKind = DocumentKind.Manuscript,
                CreatedAt = now,
                UpdatedAt = now
            };

            SectionRecord section = new()
            {
                Id = sectionId,
                DocumentId = documentId,
                Title = "Scene 1",
                OrderIndex = 0,
                CreatedAt = now,
                UpdatedAt = now
            };

            PageRecord page = new()
            {
                Id = pageId,
                DocumentId = documentId,
                SectionId = sectionId,
                Title = "Page 1",
                Content = "{\"type\":\"doc\",\"content\":[{\"type\":\"paragraph\",\"content\":[{\"type\":\"text\",\"text\":\"This scene body has Test inside.\"}]}]}",
                OrderIndex = 0,
                CreatedAt = now,
                UpdatedAt = now
            };

            db.Projects.Add(project);
            db.Documents.Add(document);
            db.Sections.Add(section);
            db.Pages.Add(page);
            db.SaveChanges();

            return (projectId, document, section, page);
        }

        private static void BreakSectionSceneCardsSchema(AppDbContext db)
        {
            db.Database.ExecuteSqlRaw("""
PRAGMA foreign_keys=OFF;
ALTER TABLE "SectionSceneCards" RENAME TO "__SectionSceneCards_old";
CREATE TABLE "SectionSceneCards" (
    "SectionId" TEXT NOT NULL CONSTRAINT "PK_SectionSceneCards" PRIMARY KEY,
    "NarrativePurpose" TEXT NULL,
    "EmotionalBeat" TEXT NULL,
    "KeyEvents" TEXT NULL,
    "OpenQuestions" TEXT NULL,
    "UpdatedUtc" TEXT NOT NULL,
    CONSTRAINT "FK_SectionSceneCards_Sections_SectionId" FOREIGN KEY ("SectionId") REFERENCES "Sections" ("Id") ON DELETE CASCADE
);
INSERT INTO "SectionSceneCards" ("SectionId","NarrativePurpose","EmotionalBeat","KeyEvents","OpenQuestions","UpdatedUtc")
SELECT "SectionId","NarrativePurpose","EmotionalBeat","KeyEvents","OpenQuestions","UpdatedUtc"
FROM "__SectionSceneCards_old";
DROP TABLE "__SectionSceneCards_old";
PRAGMA foreign_keys=ON;
""");
        }

        private sealed class StubUserIdResolver : IUserIdResolver
        {
            public string ResolveUserId(ClaimsPrincipal user) => "user-1";
        }

        private sealed class StubHostEnvironment : IHostEnvironment
        {
            public string EnvironmentName { get; set; } = Environments.Development;
            public string ApplicationName { get; set; } = "WriterApp.Tests";
            public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
            public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        }
    }
}
