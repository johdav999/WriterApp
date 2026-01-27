using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WriterApp.Data.Documents;

namespace WriterApp.Application.Documents
{
    public interface ISectionRepository
    {
        Task<IReadOnlyList<SectionRecord>> ListByDocumentAsync(Guid documentId, string ownerUserId, CancellationToken ct);
        Task<SectionRecord?> GetAsync(Guid sectionId, string ownerUserId, CancellationToken ct);
        Task<SectionRecord> CreateAsync(SectionRecord section, CancellationToken ct);
        Task<bool> ExistsAsync(Guid sectionId, string ownerUserId, CancellationToken ct);
        Task<SectionRecord?> UpdateAsync(Guid sectionId, string ownerUserId, SectionUpdate update, CancellationToken ct);
        Task<SectionRecord?> DeleteAsync(Guid sectionId, string ownerUserId, CancellationToken ct);
    }

    public sealed record SectionUpdate(string? Title, string? NarrativePurpose);
}
