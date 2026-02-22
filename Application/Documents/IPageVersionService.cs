using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WriterApp.Data.Documents;

namespace WriterApp.Application.Documents
{
    public interface IPageVersionService
    {
        Task<PageVersionRecord?> CreateSnapshotAsync(
            string userId,
            PageRecord page,
            string content,
            string reason,
            bool allowDuplicate,
            CancellationToken ct);

        Task<PageVersionRecord?> CreateAutosnapshotIfDueAsync(
            string userId,
            PageRecord page,
            string content,
            TimeSpan minAge,
            CancellationToken ct);

        Task<IReadOnlyList<PageVersionRecord>> ListVersionsAsync(
            string userId,
            Guid pageId,
            CancellationToken ct);

        Task<PageVersionRecord?> GetVersionAsync(
            string userId,
            Guid versionId,
            CancellationToken ct);

        string DecompressContent(PageVersionRecord version);

        Task CleanupAsync(string userId, Guid pageId, CancellationToken ct);
    }
}
