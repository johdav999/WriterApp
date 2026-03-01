using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WriterApp.Data.Documents;

namespace WriterApp.Application.Documents
{
    public sealed record PageUpdate(string? Title, string? Content);

    public interface IPageRepository
    {
        Task<IReadOnlyList<PageRecord>> ListBySectionAsync(Guid sectionId, string ownerUserId, CancellationToken ct);
        Task<PageRecord?> GetAsync(Guid pageId, string ownerUserId, CancellationToken ct);
        Task<PageRecord> CreateAsync(PageRecord page, CancellationToken ct);
        Task<PageRecord?> UpdateAsync(Guid pageId, string ownerUserId, PageUpdate update, CancellationToken ct);
        Task<bool> DeleteAsync(Guid pageId, string ownerUserId, CancellationToken ct);
        Task<PageRecord?> MoveAsync(Guid pageId, string ownerUserId, Guid targetSectionId, int targetOrderIndex, CancellationToken ct);
    }
}
