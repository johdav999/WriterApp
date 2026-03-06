using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WriterApp.Application.Documents;

namespace WriterApp.Data.Documents
{
    public sealed class DocumentRepository : IDocumentRepository
    {
        private readonly AppDbContext _dbContext;
        private readonly ILogger<DocumentRepository> _logger;

        public DocumentRepository(AppDbContext dbContext, ILogger<DocumentRepository> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task<DocumentRecord?> GetAsync(Guid documentId, string ownerUserId, CancellationToken ct)
        {
            return _dbContext.Documents
                .AsNoTracking()
                .FirstOrDefaultAsync(document =>
                    document.Id == documentId && document.OwnerUserId == ownerUserId, ct);
        }

        public async Task<IReadOnlyList<DocumentRecord>> ListAsync(
            string ownerUserId,
            DocumentListView view,
            CancellationToken ct)
        {
            IQueryable<DocumentRecord> query = _dbContext.Documents
                .AsNoTracking()
                .Where(document => document.OwnerUserId == ownerUserId);

            query = view switch
            {
                DocumentListView.Archived => query.Where(document => document.DeletedAtUtc == null && document.IsArchived),
                DocumentListView.Trash => query.Where(document => document.DeletedAtUtc != null),
                _ => query.Where(document => document.DeletedAtUtc == null && !document.IsArchived)
            };

            List<DocumentRecord> documents = await query.ToListAsync(ct);

            documents = documents
                .OrderByDescending(document => document.UpdatedAtUnixSeconds)
                .ToList();

            if (documents.Count == 0)
            {
                _logger.LogInformation("ListAsync returned 0 documents for user {UserId}.", ownerUserId);
            }

            return documents;
        }

        public async Task<DocumentRecord> CreateAsync(DocumentRecord document, CancellationToken ct)
        {
            _dbContext.Documents.Add(document);
            await DatabaseRetryHelper.ExecuteAsync(_dbContext, () => _dbContext.SaveChangesAsync(ct), ct);
            _logger.LogInformation("Created document {DocumentId} for user {UserId}.", document.Id, document.OwnerUserId);
            return document;
        }

        public async Task<DocumentRecord?> UpdateTitleAsync(Guid documentId, string ownerUserId, string title, CancellationToken ct)
        {
            DocumentRecord? document = await _dbContext.Documents
                .FirstOrDefaultAsync(candidate =>
                    candidate.Id == documentId && candidate.OwnerUserId == ownerUserId, ct);

            if (document is null)
            {
                return null;
            }

            string nextTitle = title.Trim();
            if (string.Equals(document.Title, nextTitle, StringComparison.Ordinal))
            {
                return document;
            }

            document.Title = nextTitle;
            document.UpdatedAt = DateTimeOffset.UtcNow;
            document.UpdatedAtUnixSeconds = document.UpdatedAt.ToUnixTimeSeconds();
            await DatabaseRetryHelper.ExecuteAsync(_dbContext, () => _dbContext.SaveChangesAsync(ct), ct);
            return document;
        }

        public Task<bool> ExistsAsync(Guid documentId, string ownerUserId, CancellationToken ct)
        {
            return _dbContext.Documents
                .AsNoTracking()
                .AnyAsync(document => document.Id == documentId && document.OwnerUserId == ownerUserId, ct);
        }
    }
}
