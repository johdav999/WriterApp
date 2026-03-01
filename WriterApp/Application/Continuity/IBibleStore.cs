using System;
using System.Threading;
using System.Threading.Tasks;

namespace WriterApp.Application.Continuity
{
    public interface IBibleStore
    {
        Task<BibleSnapshotState?> GetSnapshotAsync(Guid documentId, BibleType bibleType, CancellationToken ct);

        Task<BibleSnapshotState> UpsertSnapshotAsync(
            Guid documentId,
            BibleType bibleType,
            string contentJson,
            string sourceHash,
            BibleRefreshCursor cursor,
            BibleRefreshStats stats,
            CancellationToken ct);
    }
}
