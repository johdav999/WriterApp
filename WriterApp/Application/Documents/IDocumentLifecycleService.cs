using System;
using System.Threading;
using System.Threading.Tasks;
using WriterApp.Data.Documents;

namespace WriterApp.Application.Documents
{
    public interface IDocumentLifecycleService
    {
        Task<DocumentRecord?> ArchiveAsync(Guid documentId, string ownerUserId, CancellationToken ct);
        Task<DocumentRecord?> UnarchiveAsync(Guid documentId, string ownerUserId, CancellationToken ct);
        Task<DocumentRecord?> MoveToTrashAsync(Guid documentId, string ownerUserId, CancellationToken ct);
        Task<DocumentRecord?> RestoreAsync(Guid documentId, string ownerUserId, CancellationToken ct);
        Task<bool> PermanentlyDeleteAsync(Guid documentId, CancellationToken ct);
        Task<int> CleanupExpiredTrashAsync(TimeSpan retention, CancellationToken ct);
    }
}
