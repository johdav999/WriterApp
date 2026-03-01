using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WriterApp.Application.Documents;

namespace WriterApp.Data.Documents
{
    public sealed class PageRepository : IPageRepository
    {
        private readonly AppDbContext _dbContext;

        public PageRepository(AppDbContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public async Task<IReadOnlyList<PageRecord>> ListBySectionAsync(Guid sectionId, string ownerUserId, CancellationToken ct)
        {
            List<PageRecord> pages = await _dbContext.Pages
                .AsNoTracking()
                .Where(page => page.SectionId == sectionId)
                .Join(_dbContext.Documents.AsNoTracking(),
                    page => page.DocumentId,
                    document => document.Id,
                    (page, document) => new { page, document })
                .Where(row => row.document.OwnerUserId == ownerUserId)
                .OrderBy(row => row.page.OrderIndex)
                .Select(row => row.page)
                .ToListAsync(ct);

            return pages;
        }

        public Task<PageRecord?> GetAsync(Guid pageId, string ownerUserId, CancellationToken ct)
        {
            return _dbContext.Pages
                .AsNoTracking()
                .Where(page => page.Id == pageId)
                .Join(_dbContext.Documents.AsNoTracking(),
                    page => page.DocumentId,
                    document => document.Id,
                    (page, document) => new { page, document })
                .Where(row => row.document.OwnerUserId == ownerUserId)
                .Select(row => row.page)
                .FirstOrDefaultAsync(ct);
        }

        public async Task<PageRecord> CreateAsync(PageRecord page, CancellationToken ct)
        {
            _dbContext.Pages.Add(page);
            await TouchDocumentAsync(page.DocumentId, ct);
            await SqliteRetryHelper.ExecuteAsync(() => _dbContext.SaveChangesAsync(ct), ct);
            return page;
        }

        public async Task<PageRecord?> UpdateAsync(Guid pageId, string ownerUserId, PageUpdate update, CancellationToken ct)
        {
            PageRecord? page = await _dbContext.Pages
                .Where(candidate => candidate.Id == pageId)
                .Join(_dbContext.Documents,
                    candidate => candidate.DocumentId,
                    document => document.Id,
                    (candidate, document) => new { candidate, document })
                .Where(row => row.document.OwnerUserId == ownerUserId)
                .Select(row => row.candidate)
                .FirstOrDefaultAsync(ct);

            if (page is null)
            {
                return null;
            }

            bool changed = false;
            if (update.Title is not null && !string.Equals(page.Title, update.Title, StringComparison.Ordinal))
            {
                page.Title = update.Title;
                changed = true;
            }

            if (update.Content is not null && !string.Equals(page.Content, update.Content, StringComparison.Ordinal))
            {
                page.Content = update.Content;
                changed = true;
            }

            if (!changed)
            {
                return page;
            }

            page.UpdatedAt = DateTimeOffset.UtcNow;
            await TouchDocumentAsync(page.DocumentId, ct);
            await SqliteRetryHelper.ExecuteAsync(() => _dbContext.SaveChangesAsync(ct), ct);
            return page;
        }

        public async Task<bool> DeleteAsync(Guid pageId, string ownerUserId, CancellationToken ct)
        {
            PageRecord? page = await _dbContext.Pages
                .Where(candidate => candidate.Id == pageId)
                .Join(_dbContext.Documents,
                    candidate => candidate.DocumentId,
                    document => document.Id,
                    (candidate, document) => new { candidate, document })
                .Where(row => row.document.OwnerUserId == ownerUserId)
                .Select(row => row.candidate)
                .FirstOrDefaultAsync(ct);

            if (page is null)
            {
                return false;
            }

            _dbContext.Pages.Remove(page);
            await TouchDocumentAsync(page.DocumentId, ct);
            await SqliteRetryHelper.ExecuteAsync(() => _dbContext.SaveChangesAsync(ct), ct);
            return true;
        }

        public async Task<PageRecord?> MoveAsync(Guid pageId, string ownerUserId, Guid targetSectionId, int targetOrderIndex, CancellationToken ct)
        {
            PageRecord? page = await _dbContext.Pages
                .Where(candidate => candidate.Id == pageId)
                .Join(_dbContext.Documents,
                    candidate => candidate.DocumentId,
                    document => document.Id,
                    (candidate, document) => new { candidate, document })
                .Where(row => row.document.OwnerUserId == ownerUserId)
                .Select(row => row.candidate)
                .FirstOrDefaultAsync(ct);

            if (page is null)
            {
                return null;
            }

            Guid sourceSectionId = page.SectionId;
            int sourceOrder = page.OrderIndex;

            List<PageRecord> sourcePages = await _dbContext.Pages
                .Where(item => item.SectionId == sourceSectionId && item.Id != pageId)
                .OrderBy(item => item.OrderIndex)
                .ToListAsync(ct);

            if (targetOrderIndex < 0)
            {
                targetOrderIndex = 0;
            }

            List<PageRecord> targetPages = sourceSectionId == targetSectionId
                ? sourcePages
                : await _dbContext.Pages
                    .Where(item => item.SectionId == targetSectionId)
                    .OrderBy(item => item.OrderIndex)
                    .ToListAsync(ct);

            if (sourceSectionId == targetSectionId)
            {
                sourcePages.Insert(Math.Min(targetOrderIndex, sourcePages.Count), page);
                for (int index = 0; index < sourcePages.Count; index++)
                {
                    sourcePages[index].OrderIndex = index;
                }
            }
            else
            {
                page.SectionId = targetSectionId;
                targetPages.Insert(Math.Min(targetOrderIndex, targetPages.Count), page);
                for (int index = 0; index < sourcePages.Count; index++)
                {
                    sourcePages[index].OrderIndex = index;
                }

                for (int index = 0; index < targetPages.Count; index++)
                {
                    targetPages[index].OrderIndex = index;
                }
            }

            if (page.SectionId != sourceSectionId || page.OrderIndex != sourceOrder)
            {
                page.UpdatedAt = DateTimeOffset.UtcNow;
            }

            await TouchDocumentAsync(page.DocumentId, ct);
            await SqliteRetryHelper.ExecuteAsync(() => _dbContext.SaveChangesAsync(ct), ct);
            return page;
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
