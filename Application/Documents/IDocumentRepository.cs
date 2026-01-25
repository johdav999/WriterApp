using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WriterApp.Data.Documents;

namespace WriterApp.Application.Documents
{
    public interface IDocumentRepository
    {
        Task<DocumentRecord?> GetAsync(Guid documentId, string ownerUserId, CancellationToken ct);
        Task<IReadOnlyList<DocumentRecord>> ListAsync(string ownerUserId, CancellationToken ct);
        Task<DocumentRecord> CreateAsync(DocumentRecord document, CancellationToken ct);
        Task<DocumentRecord?> UpdateTitleAsync(Guid documentId, string ownerUserId, string title, CancellationToken ct);
        Task<bool> ExistsAsync(Guid documentId, string ownerUserId, CancellationToken ct);
    }
}
