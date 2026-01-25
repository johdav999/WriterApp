using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WriterApp.Application.Documents;

namespace WriterApp.Data.Documents
{
    public sealed class SectionRepository : ISectionRepository
    {
        private readonly AppDbContext _dbContext;

        public SectionRepository(AppDbContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public async Task<IReadOnlyList<SectionRecord>> ListByDocumentAsync(Guid documentId, string ownerUserId, CancellationToken ct)
        {
            List<SectionRecord> sections = await _dbContext.Sections
                .AsNoTracking()
                .Where(section => section.DocumentId == documentId)
                .Join(_dbContext.Documents.AsNoTracking(),
                    section => section.DocumentId,
                    document => document.Id,
                    (section, document) => new { section, document })
                .Where(row => row.document.OwnerUserId == ownerUserId)
                .OrderBy(row => row.section.OrderIndex)
                .Select(row => row.section)
                .ToListAsync(ct);

            return sections;
        }

        public Task<SectionRecord?> GetAsync(Guid sectionId, string ownerUserId, CancellationToken ct)
        {
            return _dbContext.Sections
                .AsNoTracking()
                .Where(section => section.Id == sectionId)
                .Join(_dbContext.Documents.AsNoTracking(),
                    section => section.DocumentId,
                    document => document.Id,
                    (section, document) => new { section, document })
                .Where(row => row.document.OwnerUserId == ownerUserId)
                .Select(row => row.section)
                .FirstOrDefaultAsync(ct);
        }

        public async Task<SectionRecord> CreateAsync(SectionRecord section, CancellationToken ct)
        {
            _dbContext.Sections.Add(section);
            await TouchDocumentAsync(section.DocumentId, ct);
            await SqliteRetryHelper.ExecuteAsync(() => _dbContext.SaveChangesAsync(ct), ct);
            return section;
        }

        public Task<bool> ExistsAsync(Guid sectionId, string ownerUserId, CancellationToken ct)
        {
            return _dbContext.Sections
                .AsNoTracking()
                .Where(section => section.Id == sectionId)
                .Join(_dbContext.Documents.AsNoTracking(),
                    section => section.DocumentId,
                    document => document.Id,
                    (section, document) => new { section, document })
                .AnyAsync(row => row.document.OwnerUserId == ownerUserId, ct);
        }

        public async Task<SectionRecord?> UpdateAsync(Guid sectionId, string ownerUserId, SectionUpdate update, CancellationToken ct)
        {
            SectionRecord? section = await _dbContext.Sections
                .Where(item => item.Id == sectionId)
                .Join(_dbContext.Documents,
                    item => item.DocumentId,
                    document => document.Id,
                    (item, document) => new { section = item, document })
                .Where(row => row.document.OwnerUserId == ownerUserId)
                .Select(row => row.section)
                .FirstOrDefaultAsync(ct);

            if (section is null)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(update.Title))
            {
                section.Title = update.Title.Trim();
            }

            section.NarrativePurpose = string.IsNullOrWhiteSpace(update.NarrativePurpose)
                ? null
                : update.NarrativePurpose.Trim();

            section.UpdatedAt = DateTimeOffset.UtcNow;
            await TouchDocumentAsync(section.DocumentId, ct);
            await SqliteRetryHelper.ExecuteAsync(() => _dbContext.SaveChangesAsync(ct), ct);
            return section;
        }

        private async Task TouchDocumentAsync(Guid documentId, CancellationToken ct)
        {
            DocumentRecord? document = await _dbContext.Documents
                .FirstOrDefaultAsync(item => item.Id == documentId, ct);
            if (document is null)
            {
                return;
            }

            document.UpdatedAt = DateTimeOffset.UtcNow;
        }
    }
}
