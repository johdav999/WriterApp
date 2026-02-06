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
using WriterApp.Application.Exporting;
using WriterApp.Application.Security;
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
                OwnerUserId = "user-1",
                Title = "Export Doc",
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

        private static ExportService BuildExportService()
        {
            IExportRenderer[] renderers =
            {
                new DocxExportRenderer(),
                new EpubExportRenderer()
            };
            return new ExportService(renderers, new StubExportTemplateResolver());
        }

        private static IConfiguration BuildConfig(params (string Key, string Value)[] pairs)
        {
            Dictionary<string, string?> values = pairs.ToDictionary(pair => pair.Key, pair => pair.Value);
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
    }
}
