using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using WriterApp.Application.Search;
using WriterApp.Data;
using WriterApp.Data.Documents;
using Xunit;

namespace WriterApp.Tests
{
    public sealed class SearchIndexNormalizationTests
    {
        [Fact]
        public async Task UpsertPageAsync_WritesLowercaseGuidTextIds()
        {
            await using AppDbContext db = BuildDbContext();
            (DocumentRecord document, SectionRecord section, PageRecord page) = SeedDocumentGraph(db);

            SearchIndexService service = new(
                db,
                NullLogger<SearchIndexService>.Instance,
                new SearchIndexBackfillQueue());

            await service.UpsertPageAsync(page, CancellationToken.None);

            await using DbConnection connection = db.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
            {
                await connection.OpenAsync(CancellationToken.None);
            }

            await using DbCommand command = connection.CreateCommand();
            command.CommandText = """
SELECT EntityId, DocumentId, SectionId, PageId
FROM SearchIndexEntries
WHERE EntityType = 'page'
LIMIT 1;
""";
            await using var reader = await command.ExecuteReaderAsync(CancellationToken.None);
            Assert.True(await reader.ReadAsync(CancellationToken.None));

            Assert.Equal(page.Id.ToString("D").ToLowerInvariant(), reader.GetString(0));
            Assert.Equal(document.Id.ToString("D").ToLowerInvariant(), reader.GetString(1));
            Assert.Equal(section.Id.ToString("D").ToLowerInvariant(), reader.GetString(2));
            Assert.Equal(page.Id.ToString("D").ToLowerInvariant(), reader.GetString(3));
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

        private static (DocumentRecord Document, SectionRecord Section, PageRecord Page) SeedDocumentGraph(AppDbContext db)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            Guid projectId = Guid.NewGuid();
            Guid documentId = Guid.NewGuid();
            Guid sectionId = Guid.NewGuid();
            Guid pageId = Guid.NewGuid();

            db.Projects.Add(new ProjectRecord
            {
                Id = projectId,
                OwnerUserId = "user-1",
                Title = "Project",
                CreatedUtc = now,
                UpdatedUtc = now
            });

            DocumentRecord document = new()
            {
                Id = documentId,
                ProjectId = projectId,
                OwnerUserId = "user-1",
                Title = "Doc",
                DocumentKind = DocumentKind.Manuscript,
                CreatedAt = now,
                UpdatedAt = now
            };
            db.Documents.Add(document);

            SectionRecord section = new()
            {
                Id = sectionId,
                DocumentId = documentId,
                Title = "Section",
                OrderIndex = 0,
                CreatedAt = now,
                UpdatedAt = now
            };
            db.Sections.Add(section);

            PageRecord page = new()
            {
                Id = pageId,
                DocumentId = documentId,
                SectionId = sectionId,
                Title = "Page",
                Content = "<p>Test body</p>",
                OrderIndex = 0,
                CreatedAt = now,
                UpdatedAt = now
            };
            db.Pages.Add(page);

            db.SaveChanges();
            return (document, section, page);
        }
    }
}
