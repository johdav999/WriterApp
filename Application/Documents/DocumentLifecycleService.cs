using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WriterApp.Data;
using WriterApp.Data.AI;
using WriterApp.Data.Documents;

namespace WriterApp.Application.Documents
{
    public sealed class DocumentLifecycleService : IDocumentLifecycleService
    {
        private readonly AppDbContext _dbContext;
        private readonly ILogger<DocumentLifecycleService> _logger;

        public DocumentLifecycleService(AppDbContext dbContext, ILogger<DocumentLifecycleService> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<DocumentRecord?> ArchiveAsync(Guid documentId, string ownerUserId, CancellationToken ct)
        {
            DocumentRecord? document = await FindOwnedDocumentAsync(documentId, ownerUserId, ct);
            if (document is null || document.DeletedAt is not null)
            {
                return null;
            }

            if (!document.IsArchived)
            {
                document.IsArchived = true;
                document.ArchivedAt = DateTimeOffset.UtcNow;
                document.UpdatedAt = DateTimeOffset.UtcNow;
                await _dbContext.SaveChangesAsync(ct);
            }

            return document;
        }

        public async Task<DocumentRecord?> UnarchiveAsync(Guid documentId, string ownerUserId, CancellationToken ct)
        {
            DocumentRecord? document = await FindOwnedDocumentAsync(documentId, ownerUserId, ct);
            if (document is null || document.DeletedAt is not null)
            {
                return null;
            }

            if (document.IsArchived)
            {
                document.IsArchived = false;
                document.ArchivedAt = null;
                document.UpdatedAt = DateTimeOffset.UtcNow;
                await _dbContext.SaveChangesAsync(ct);
            }

            return document;
        }

        public async Task<DocumentRecord?> MoveToTrashAsync(Guid documentId, string ownerUserId, CancellationToken ct)
        {
            DocumentRecord? document = await FindOwnedDocumentAsync(documentId, ownerUserId, ct);
            if (document is null)
            {
                return null;
            }

            if (document.DeletedAt is null)
            {
                document.DeletedAt = DateTimeOffset.UtcNow;
                document.IsArchived = false;
                document.ArchivedAt = null;
                document.UpdatedAt = DateTimeOffset.UtcNow;
                await _dbContext.SaveChangesAsync(ct);
            }

            return document;
        }

        public async Task<DocumentRecord?> RestoreAsync(Guid documentId, string ownerUserId, CancellationToken ct)
        {
            DocumentRecord? document = await FindOwnedDocumentAsync(documentId, ownerUserId, ct);
            if (document is null)
            {
                return null;
            }

            if (document.DeletedAt is not null || document.IsArchived)
            {
                document.DeletedAt = null;
                document.IsArchived = false;
                document.ArchivedAt = null;
                document.UpdatedAt = DateTimeOffset.UtcNow;
                await _dbContext.SaveChangesAsync(ct);
            }

            return document;
        }

        public async Task<bool> PermanentlyDeleteAsync(Guid documentId, CancellationToken ct)
        {
            DocumentRecord? document = await _dbContext.Documents
                .FirstOrDefaultAsync(item => item.Id == documentId, ct);
            if (document is null)
            {
                return false;
            }

            await using var transaction = await _dbContext.Database.BeginTransactionAsync(ct);

            List<AiActionAppliedEventRecord> appliedEvents = await _dbContext.AiActionAppliedEvents
                .Where(item => item.AppliedToDocumentId == documentId)
                .ToListAsync(ct);
            if (appliedEvents.Count > 0)
            {
                _dbContext.AiActionAppliedEvents.RemoveRange(appliedEvents);
            }

            List<AiActionHistoryEntryRecord> historyEntries = await _dbContext.AiActionHistoryEntries
                .Where(item => item.DocumentId == documentId)
                .ToListAsync(ct);
            if (historyEntries.Count > 0)
            {
                _dbContext.AiActionHistoryEntries.RemoveRange(historyEntries);
            }

            List<PageVersionRecord> versions = await _dbContext.PageVersions
                .Where(item => item.DocumentId == documentId)
                .ToListAsync(ct);
            if (versions.Count > 0)
            {
                _dbContext.PageVersions.RemoveRange(versions);
            }

            _dbContext.Documents.Remove(document);
            await _dbContext.SaveChangesAsync(ct);

            await RemoveSearchIndexEntriesAsync(documentId, ct);

            await transaction.CommitAsync(ct);
            _logger.LogInformation("Permanently deleted document {DocumentId}.", documentId);
            return true;
        }

        public async Task<int> CleanupExpiredTrashAsync(TimeSpan retention, CancellationToken ct)
        {
            DateTime cutoffUtc = DateTime.UtcNow.Subtract(retention);
            List<Guid> expiredIds = await _dbContext.Documents
                .AsNoTracking()
                // SQLite struggles with some DateTimeOffset comparisons; compare against UTC DateTime explicitly.
                .Where(document => document.DeletedAt.HasValue && document.DeletedAt.Value.UtcDateTime < cutoffUtc)
                .Select(document => document.Id)
                .ToListAsync(ct);

            int deleted = 0;
            foreach (Guid documentId in expiredIds)
            {
                if (await PermanentlyDeleteAsync(documentId, ct))
                {
                    deleted += 1;
                }
            }

            if (deleted > 0)
            {
                _logger.LogInformation("CleanupExpiredTrash removed {Count} documents.", deleted);
            }

            return deleted;
        }

        private Task<DocumentRecord?> FindOwnedDocumentAsync(Guid documentId, string ownerUserId, CancellationToken ct)
        {
            return _dbContext.Documents
                .FirstOrDefaultAsync(document => document.Id == documentId && document.OwnerUserId == ownerUserId, ct);
        }

        private async Task RemoveSearchIndexEntriesAsync(Guid documentId, CancellationToken ct)
        {
            try
            {
                const string deleteFts = @"
DELETE FROM SearchIndexFts
WHERE rowid IN (
    SELECT Id FROM SearchIndexEntries WHERE DocumentId = $documentId
);";
                const string deleteEntries = "DELETE FROM SearchIndexEntries WHERE DocumentId = $documentId;";

                await _dbContext.Database.ExecuteSqlRawAsync(deleteFts, new object[] { documentId }, ct);
                await _dbContext.Database.ExecuteSqlRawAsync(deleteEntries, new object[] { documentId }, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to remove search index entries for document {DocumentId}.", documentId);
            }
        }
    }
}
