using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using WriterApp.Application.Documents;
using WriterApp.Data;
using WriterApp.Data.Documents;
using Xunit;

namespace WriterApp.Tests
{
    public sealed class DocumentLifecycleServiceTests
    {
        [Fact]
        public async Task CleanupExpiredTrashAsync_DeletesOnlyExpiredDocuments_OnSqlite()
        {
            await using SqliteConnection connection = new("Filename=:memory:");
            await connection.OpenAsync();

            DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;

            await using AppDbContext db = new(options);
            await db.Database.EnsureCreatedAsync();

            string ownerUserId = "cleanup-user";
            DateTimeOffset now = DateTimeOffset.UtcNow;
            DateTime utcNow = now.UtcDateTime;

            ProjectRecord project = new()
            {
                Id = Guid.NewGuid(),
                OwnerUserId = ownerUserId,
                Title = "Cleanup project",
                CreatedUtc = now,
                UpdatedUtc = now
            };
            db.Projects.Add(project);

            DocumentRecord expired = BuildDocument(project.Id, ownerUserId, "Expired", DocumentKind.Other, utcNow.AddDays(-10), now);
            DocumentRecord recent = BuildDocument(project.Id, ownerUserId, "Recent", DocumentKind.Other, utcNow.AddDays(-6), now);
            DocumentRecord active = BuildDocument(project.Id, ownerUserId, "Active", DocumentKind.Other, null, now);
            db.Documents.AddRange(expired, recent, active);
            await db.SaveChangesAsync();

            DocumentLifecycleService service = new(db, NullLogger<DocumentLifecycleService>.Instance);

            int deleted = await service.CleanupExpiredTrashAsync(TimeSpan.FromDays(7), CancellationToken.None);

            Assert.Equal(1, deleted);

            List<Guid> remainingIds = await db.Documents
                .AsNoTracking()
                .Select(document => document.Id)
                .ToListAsync();

            Assert.DoesNotContain(expired.Id, remainingIds);
            Assert.Contains(recent.Id, remainingIds);
            Assert.Contains(active.Id, remainingIds);
        }

        [Fact]
        public async Task CleanupExpiredTrashAsync_QueryIsSqlTranslatable_OnSqlite()
        {
            await using SqliteConnection connection = new("Filename=:memory:");
            await connection.OpenAsync();

            DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;

            await using AppDbContext db = new(options);
            await db.Database.EnsureCreatedAsync();

            DateTime cutoffUtc = DateTime.UtcNow.AddDays(-30);
            IQueryable<DocumentRecord> query = db.Documents
                .AsNoTracking()
                .Where(document => document.DeletedAtUtc != null && document.DeletedAtUtc < cutoffUtc)
                .Select(document => document);

            string sql = query.ToQueryString();

            Assert.Contains("DeletedAtUtc", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("<", sql, StringComparison.Ordinal);
            Assert.Contains("WHERE", sql, StringComparison.OrdinalIgnoreCase);
        }

        private static DocumentRecord BuildDocument(
            Guid projectId,
            string ownerUserId,
            string title,
            DocumentKind kind,
            DateTime? deletedAtUtc,
            DateTimeOffset now)
        {
            return new DocumentRecord
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                OwnerUserId = ownerUserId,
                Title = title,
                DocumentKind = kind,
                LanguageCode = "en",
                TranslationGroupId = null,
                CreatedAt = now,
                UpdatedAt = now,
                CreatedAtUnixSeconds = now.ToUnixTimeSeconds(),
                UpdatedAtUnixSeconds = now.ToUnixTimeSeconds(),
                IsArchived = false,
                ArchivedAt = null,
                DeletedAtUtc = deletedAtUtc
            };
        }
    }
}
