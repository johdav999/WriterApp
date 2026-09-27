namespace WriterApp.Device.Shared.Storage;

public interface ILocalDocumentStore
{
    Task<LocalDocument> CreateAsync(string title, CancellationToken cancellationToken = default);
    Task<LocalDocument?> GetAsync(Guid documentId, CancellationToken cancellationToken = default);
    Task<LocalDocumentList> ListAsync(LocalDocumentScope scope = LocalDocumentScope.Active, CancellationToken cancellationToken = default);
    // Saves an existing snapshot, checking LocalRevision and returning the new revision.
    Task<LocalDocument> SaveAsync(LocalDocument document, CancellationToken cancellationToken = default);
    Task<LocalDocument> RenameAsync(Guid documentId, long revision, string title, CancellationToken cancellationToken = default);
    Task<LocalDocument> DuplicateAsync(Guid documentId, CancellationToken cancellationToken = default);
    Task<LocalDocument> MoveToTrashAsync(Guid documentId, long revision, CancellationToken cancellationToken = default);
    Task<LocalDocument> RestoreAsync(Guid documentId, long revision, CancellationToken cancellationToken = default);
    Task PermanentlyDeleteAsync(Guid documentId, long revision, CancellationToken cancellationToken = default);
}
