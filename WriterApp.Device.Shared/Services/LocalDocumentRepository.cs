using WriterApp.Device.Shared.Storage;

namespace WriterApp.Device.Shared.Services;

/// <summary>Device UI entry point. Callers use document identities and returned revisions, never filesystem paths.</summary>
public sealed class LocalDocumentRepository(ILocalDocumentStore store)
{
    public Task<LocalDocument> CreateAsync(string title, CancellationToken cancellationToken = default) =>
        store.CreateAsync(title, cancellationToken);
    public Task<LocalDocument?> LoadAsync(Guid id, CancellationToken cancellationToken = default) =>
        store.GetAsync(id, cancellationToken);
    public Task<LocalDocumentList> ListAsync(LocalDocumentScope scope = LocalDocumentScope.Active, CancellationToken cancellationToken = default) =>
        store.ListAsync(scope, cancellationToken);
    public Task<LocalDocument> SaveAsync(LocalDocument document, CancellationToken cancellationToken = default) =>
        store.SaveAsync(document, cancellationToken);
    public Task<LocalDocument> RenameAsync(LocalDocument document, string title, CancellationToken cancellationToken = default) =>
        store.RenameAsync(document.DocumentId, document.LocalRevision, title, cancellationToken);
    public Task<LocalDocument> DuplicateAsync(Guid id, CancellationToken cancellationToken = default) =>
        store.DuplicateAsync(id, cancellationToken);
    public Task<LocalDocument> MoveToTrashAsync(LocalDocument document, CancellationToken cancellationToken = default) =>
        store.MoveToTrashAsync(document.DocumentId, document.LocalRevision, cancellationToken);
    public Task<LocalDocument> RestoreAsync(LocalDocument document, CancellationToken cancellationToken = default) =>
        store.RestoreAsync(document.DocumentId, document.LocalRevision, cancellationToken);
    public Task PermanentlyDeleteAsync(LocalDocument document, CancellationToken cancellationToken = default) =>
        store.PermanentlyDeleteAsync(document.DocumentId, document.LocalRevision, cancellationToken);
}
