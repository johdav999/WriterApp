using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using WriterApp.Application.Documents;
using WriterApp.Application.Importing;
using WriterApp.Application.Search;
using WriterApp.Application.Security;
using WriterApp.Controllers;
using WriterApp.Data;
using WriterApp.Data.Documents;
using Xunit;

namespace WriterApp.Tests
{
    public sealed class SectionsImportEndpointTests
    {
        [Fact]
        public async Task ImportSectionContent_TxtReplace_ReturnsConvertedHtmlAndUpdatesPage()
        {
            await using AppDbContext db = BuildDbContext();
            SeedDocumentGraph(db, out Guid documentId, out Guid sectionId, out Guid pageId);

            SectionsController controller = BuildController(db);
            byte[] bytes = Encoding.UTF8.GetBytes("First paragraph.\n\nSecond paragraph.");
            using MemoryStream stream = new(bytes);
            IFormFile file = new FormFile(stream, 0, bytes.Length, "file", "import.txt")
            {
                Headers = new HeaderDictionary(),
                ContentType = "text/plain"
            };

            SectionImportFormRequest request = new(
                file,
                sectionId,
                "replace",
                NormalizeWhitespace: true,
                PreserveTxtLineBreaks: false);

            ActionResult<SectionImportResponseDto> result = await controller.ImportSectionContent(
                documentId,
                sectionId,
                request,
                CancellationToken.None);

            OkObjectResult ok = Assert.IsType<OkObjectResult>(result.Result);
            SectionImportResponseDto payload = Assert.IsType<SectionImportResponseDto>(ok.Value);
            Assert.Equal("txt", payload.Format);
            Assert.Contains("<p>First paragraph.</p>", payload.Html);
            Assert.Contains("<p>Second paragraph.</p>", payload.Html);
            Assert.Equal(sectionId, payload.TargetSectionId);

            PageRecord? page = await db.Pages.FirstOrDefaultAsync(item => item.Id == pageId);
            Assert.NotNull(page);
            Assert.Contains("First paragraph.", page!.Content);
            Assert.Contains("Second paragraph.", page.Content);
        }

        private static SectionsController BuildController(AppDbContext db)
        {
            IConfiguration config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>())
                .Build();
            SectionsController controller = new(
                new DocumentRepository(db, NullLogger<DocumentRepository>.Instance),
                new SectionRepository(db, NullLogger<SectionRepository>.Instance, config),
                new PageRepository(db),
                new StubUserIdResolver(),
                new StubSearchIndexService(),
                db,
                NullLogger<SectionsController>.Instance,
                config,
                new StubVersionHistoryService(),
                new SectionImportService());

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

        private static void SeedDocumentGraph(AppDbContext db, out Guid documentId, out Guid sectionId, out Guid pageId)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            Guid projectId = Guid.NewGuid();
            documentId = Guid.NewGuid();
            sectionId = Guid.NewGuid();
            pageId = Guid.NewGuid();

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
                Title = "Scene",
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
                Content = "<p>Old content</p>",
                OrderIndex = 0,
                CreatedAt = now,
                UpdatedAt = now
            });

            db.SaveChanges();
        }

        private sealed class StubUserIdResolver : IUserIdResolver
        {
            public string ResolveUserId(ClaimsPrincipal user) => "user-1";
        }

        private sealed class StubSearchIndexService : ISearchIndexService
        {
            public string? DisabledReason => null;
            public Task UpsertDocumentAsync(DocumentRecord document, CancellationToken ct) => Task.CompletedTask;
            public Task UpsertSectionAsync(SectionRecord section, CancellationToken ct) => Task.CompletedTask;
            public Task UpsertPageAsync(PageRecord page, CancellationToken ct) => Task.CompletedTask;
            public Task UpsertPageNotesAsync(PageRecord page, PageNoteRecord notes, CancellationToken ct) => Task.CompletedTask;
            public Task UpsertSceneCardAsync(SectionRecord section, SectionSceneCardRecord card, CancellationToken ct) => Task.CompletedTask;
            public Task ReplaceOutlineAsync(DocumentRecord document, string outlineText, IReadOnlyList<DocumentOutlineNodeRecord> nodes, CancellationToken ct) => Task.CompletedTask;
            public Task DeleteByEntityAsync(string entityType, Guid entityId, CancellationToken ct) => Task.CompletedTask;
            public Task<int> GetProjectEntryCountAsync(string ownerUserId, Guid projectId, CancellationToken ct) => Task.FromResult(0);
            public Task RebuildProjectIndexAsync(string ownerUserId, Guid projectId, CancellationToken ct) => Task.CompletedTask;
            public Task<bool> TryProbeAndRecoverAsync(CancellationToken ct = default) => Task.FromResult(true);
            public Task RebuildSearchIndexAsync(CancellationToken ct = default) => Task.CompletedTask;
            public Task<IReadOnlyList<SearchResultDto>> SearchAsync(string userId, Guid projectId, string query, bool includeMeta, int limit, string? correlationId, CancellationToken ct)
                => Task.FromResult<IReadOnlyList<SearchResultDto>>(Array.Empty<SearchResultDto>());
        }

        private sealed class StubVersionHistoryService : IVersionHistoryService
        {
            public Task<PageVersionRecord?> CreateCheckpointAsync(string userId, PageRecord page, string content, string reason, bool allowDuplicate, CancellationToken ct)
                => Task.FromResult<PageVersionRecord?>(null);

            public Task<PageVersionRecord?> CreateCheckpointIfDueAsync(string userId, PageRecord page, string content, TimeSpan minAge, CancellationToken ct)
                => Task.FromResult<PageVersionRecord?>(null);

            public Task<IReadOnlyList<PageVersionRecord>> ListVersionsAsync(string userId, Guid pageId, CancellationToken ct)
                => Task.FromResult<IReadOnlyList<PageVersionRecord>>(Array.Empty<PageVersionRecord>());

            public Task<PageVersionRecord?> GetVersionAsync(string userId, Guid versionId, CancellationToken ct)
                => Task.FromResult<PageVersionRecord?>(null);

            public string DecompressContent(PageVersionRecord version) => string.Empty;

            public Task PruneAsync(string userId, Guid pageId, CancellationToken ct) => Task.CompletedTask;
        }
    }
}
