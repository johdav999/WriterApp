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
using System.Net.Http;
using WriterApp.Application.Exporting;
using WriterApp.Application.Security;
using WriterApp.Application.Documents;
using WriterApp.Controllers;
using WriterApp.Data;
using WriterApp.Data.Documents;
using Xunit;

namespace WriterApp.Tests
{
    public sealed class ExportEndpointTests
    {
        [Fact]
        public async Task ExportDocumentPost_Docx_ReturnsFile()
        {
            Guid documentId = Guid.NewGuid();
            using AppDbContext dbContext = BuildDbContext();
            SeedDocument(dbContext, documentId);

            ExportService exportService = BuildExportService();
            IConfiguration config = BuildConfig(("Exports:DocxEnabled", "true"));
            DocumentExportController controller = BuildController(dbContext, exportService, config);

            ExportDocumentRequest request = new(
                documentId,
                "docx",
                null,
                "document");

            IActionResult result = await controller.ExportDocumentPost(documentId, request, CancellationToken.None);
            FileContentResult file = Assert.IsType<FileContentResult>(result);
            Assert.Equal("application/vnd.openxmlformats-officedocument.wordprocessingml.document", file.ContentType);
            Assert.NotEmpty(file.FileContents);
        }

        [Fact]
        public async Task ExportDocumentGet_SynopsisDocx_ReturnsFile()
        {
            Guid documentId = Guid.NewGuid();
            using AppDbContext dbContext = BuildDbContext();
            SeedDocument(dbContext, documentId);
            SeedSynopsis(dbContext, documentId);

            ExportService exportService = BuildExportService();
            IConfiguration config = BuildConfig(("Exports:DocxEnabled", "true"));
            DocumentExportController controller = BuildController(dbContext, exportService, config);

            IActionResult result = await controller.ExportDocument(
                documentId,
                "synopsis",
                "docx",
                null,
                CancellationToken.None);

            FileContentResult file = Assert.IsType<FileContentResult>(result);
            Assert.Equal("application/vnd.openxmlformats-officedocument.wordprocessingml.document", file.ContentType);
            Assert.Contains("Synopsis.docx", file.FileDownloadName, StringComparison.OrdinalIgnoreCase);
            Assert.NotEmpty(file.FileContents);
        }

        [Fact]
        public async Task ExportDocumentPost_Epub_ReturnsFile()
        {
            Guid documentId = Guid.NewGuid();
            using AppDbContext dbContext = BuildDbContext();
            SeedDocument(dbContext, documentId);

            ExportService exportService = BuildExportService();
            IConfiguration config = BuildConfig(("Exports:EpubEnabled", "true"));
            DocumentExportController controller = BuildController(dbContext, exportService, config);

            ExportDocumentRequest request = new(
                documentId,
                "epub",
                null,
                "document");

            IActionResult result = await controller.ExportDocumentPost(documentId, request, CancellationToken.None);
            FileContentResult file = Assert.IsType<FileContentResult>(result);
            Assert.Equal("application/epub+zip", file.ContentType);
            Assert.NotEmpty(file.FileContents);
        }

        [Fact]
        public async Task ExportPrintPost_UsesLinkedSceneContentFallback_WhenSectionPagesAreEmpty()
        {
            Guid documentId = Guid.NewGuid();
            Guid projectId = Guid.NewGuid();
            Guid sectionId = Guid.NewGuid();
            using AppDbContext dbContext = BuildDbContext();
            SeedManuscriptWithEmptyPageAndSceneContent(dbContext, documentId, projectId, sectionId);

            ExportService exportService = BuildExportService();
            IConfiguration config = BuildConfig();
            DocumentExportController controller = BuildController(dbContext, exportService, config);

            ExportDocumentRequest request = new(
                documentId,
                "html",
                null,
                "document");

            ActionResult<DocumentExportController.ExportPrintPayload> result = await controller.ExportPrintPost(documentId, request, CancellationToken.None);
            OkObjectResult ok = Assert.IsType<OkObjectResult>(result.Result);
            DocumentExportController.ExportPrintPayload payload =
                Assert.IsType<DocumentExportController.ExportPrintPayload>(ok.Value);

            Assert.Contains("Fallback scene content from SceneContents", payload.Html);
            Assert.Contains("Scene One", payload.Html);
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

        private static void SeedDocument(AppDbContext context, Guid documentId)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            DocumentRecord document = new()
            {
                Id = documentId,
                ProjectId = Guid.NewGuid(),
                OwnerUserId = "user-1",
                Title = "Export Doc",
                DocumentKind = DocumentKind.Other,
                CreatedAt = now,
                UpdatedAt = now
            };

            SectionRecord section = new()
            {
                Id = Guid.NewGuid(),
                DocumentId = documentId,
                Title = "Section One",
                OrderIndex = 0,
                CreatedAt = now,
                UpdatedAt = now
            };

            PageRecord page = new()
            {
                Id = Guid.NewGuid(),
                DocumentId = documentId,
                SectionId = section.Id,
                Title = "Page One",
                Content = "<p>Hello export.</p>",
                OrderIndex = 0,
                CreatedAt = now,
                UpdatedAt = now
            };

            context.Documents.Add(document);
            context.Sections.Add(section);
            context.Pages.Add(page);
            context.SaveChanges();
        }

        private static void SeedSynopsis(AppDbContext context, Guid documentId)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            context.DocumentSynopses.Add(new DocumentSynopsisRecord
            {
                DocumentId = documentId,
                Logline = "A synopsis logline.",
                Premise = "A synopsis premise.",
                Theme = "A synopsis theme.",
                ProtagonistArc = "A synopsis arc.",
                CentralConflict = "A synopsis conflict.",
                Stakes = "A synopsis stakes.",
                Setting = "A synopsis setting.",
                EndingIntent = "A synopsis ending intent.",
                OpenQuestions = "A synopsis open question.",
                Notes = "A synopsis note.",
                UpdatedAt = now
            });
            context.SaveChanges();
        }

        private static void SeedManuscriptWithEmptyPageAndSceneContent(
            AppDbContext context,
            Guid documentId,
            Guid projectId,
            Guid sectionId)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            Guid pageId = Guid.NewGuid();
            Guid sceneNodeId = Guid.NewGuid();

            context.Projects.Add(new ProjectRecord
            {
                Id = projectId,
                OwnerUserId = "user-1",
                Title = "Project",
                CreatedUtc = now,
                UpdatedUtc = now
            });

            context.Documents.Add(new DocumentRecord
            {
                Id = documentId,
                ProjectId = projectId,
                OwnerUserId = "user-1",
                Title = "Manuscript Export",
                DocumentKind = DocumentKind.Manuscript,
                CreatedAt = now,
                UpdatedAt = now
            });

            context.Sections.Add(new SectionRecord
            {
                Id = sectionId,
                DocumentId = documentId,
                Title = "Scene One",
                OrderIndex = 0,
                CreatedAt = now,
                UpdatedAt = now
            });

            context.Pages.Add(new PageRecord
            {
                Id = pageId,
                DocumentId = documentId,
                SectionId = sectionId,
                Title = "Page One",
                Content = string.Empty,
                OrderIndex = 0,
                CreatedAt = now,
                UpdatedAt = now
            });

            context.ProjectNodes.Add(new ProjectNodeRecord
            {
                Id = sceneNodeId,
                ProjectId = projectId,
                ParentId = null,
                NodeType = ProjectNodeType.Scene,
                Title = "Scene One",
                OrderIndex = 0,
                LinkedSectionId = sectionId,
                WordCountCache = 0,
                UpdatedUtc = now
            });

            context.SceneContents.Add(new SceneContentRecord
            {
                SceneNodeId = sceneNodeId,
                ContentJson = "<p>Fallback scene content from SceneContents.</p>",
                UpdatedAtUtc = now
            });

            context.SaveChanges();
        }

        private static ExportService BuildExportService()
        {
            IExportRenderer[] renderers =
            {
                new DocxExportRenderer(
                    NullLogger<DocxExportRenderer>.Instance,
                    BuildConfig(("Exports:DocxFetchRemoteImages", "false")),
                    new StubHttpClientFactory()),
                new SynopsisDocxExportRenderer(),
                new EpubExportRenderer()
            };
            return new ExportService(renderers, new StubExportTemplateResolver());
        }

        private static IConfiguration BuildConfig(params (string Key, string Value)[] pairs)
        {
            Dictionary<string, string?> values = pairs.ToDictionary(pair => pair.Key, pair => (string?)pair.Value);
            return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        }

        private static DocumentExportController BuildController(
            AppDbContext context,
            ExportService exportService,
            IConfiguration configuration)
        {
            DocumentExportController controller = new(
                context,
                new StubUserIdResolver(),
                new StubProjectSceneLinkingService(),
                exportService,
                configuration,
                NullLogger<DocumentExportController>.Instance);

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity())
                }
            };

            return controller;
        }

        private sealed class StubUserIdResolver : IUserIdResolver
        {
            public string ResolveUserId(ClaimsPrincipal user) => "user-1";
        }

        private sealed class StubExportTemplateResolver : IExportTemplateResolver
        {
            public Task<WriterApp.Data.Exporting.ExportTemplate> ResolveAsync(string ownerUserId, Guid? templateId, CancellationToken ct)
            {
                throw new InvalidOperationException("Templates are not used for DOCX/EPUB exports.");
            }
        }

        private sealed class StubProjectSceneLinkingService : IProjectSceneLinkingService
        {
            public Task<DocumentRecord?> GetOrCreateManuscriptDocumentAsync(Guid projectId, string ownerUserId, CancellationToken ct)
                => Task.FromResult<DocumentRecord?>(null);

            public Task<SceneLinkResult?> EnsureSceneLinkedSectionAsync(Guid projectId, Guid sceneNodeId, string ownerUserId, CancellationToken ct)
                => Task.FromResult<SceneLinkResult?>(null);

            public Task<SceneLinkResult?> EnsureSceneLinkedSectionAsync(ProjectRecord project, ProjectNodeRecord sceneNode, string ownerUserId, CancellationToken ct)
                => Task.FromResult<SceneLinkResult?>(null);

            public Task<IReadOnlyList<ManuscriptSceneSectionItem>> GetManuscriptSceneSectionsAsync(Guid projectId, string ownerUserId, CancellationToken ct)
                => Task.FromResult<IReadOnlyList<ManuscriptSceneSectionItem>>(Array.Empty<ManuscriptSceneSectionItem>());
        }

        private sealed class StubHttpClientFactory : IHttpClientFactory
        {
            public HttpClient CreateClient(string name) => new();
        }
    }
}
