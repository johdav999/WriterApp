namespace WriterApp.Device.Shared.Storage;

public interface ILocalDocumentStore
{
    Task<LocalDocument> MoveStandaloneToProjectAsync(LocalDocument source, Guid targetDocumentId, CancellationToken ct = default) =>
        throw new NotSupportedException("Moving documents into projects is not supported by this storage adapter.");
    Task<LocalDocument> CreateProjectDocumentAsync(Guid projectId, string title, string kind, CancellationToken ct = default) =>
        throw new NotSupportedException("Adding project documents is not supported by this storage adapter.");
    Task<LocalDocument> UpdateProjectMetadataAsync(LocalDocument source, string? title = null, bool makePrimary = false, CancellationToken ct = default) =>
        throw new NotSupportedException("Project metadata updates are not supported by this storage adapter.");
    Task<LocalDocument> RecoverSnapshotAsync(LocalDocument source, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Snapshot recovery is not supported by this storage adapter.");
    Task<LocalDocument> CreateProjectAsync(string title, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Project creation is not supported by this storage adapter.");
    Task<LocalDocument> CreateAsync(string title, CancellationToken cancellationToken = default);
    Task<LocalDocument> CreateImportedAsync(string title, string html, CancellationToken cancellationToken = default);
    Task<LocalDocument?> GetAsync(Guid documentId, CancellationToken cancellationToken = default);
    Task<LocalDocumentList> ListAsync(LocalDocumentScope scope = LocalDocumentScope.Active, CancellationToken cancellationToken = default);
    async Task<LocalSearchDocuments> ReadSearchDocumentsAsync(CancellationToken cancellationToken = default)
    {
        var list = await ListAsync(LocalDocumentScope.Active, cancellationToken);
        return new(list.Documents.Take(256).ToArray(), list.Issues, list.Documents.Count > 256);
    }
    // Saves an existing snapshot, checking LocalRevision and returning the new revision.
    Task<LocalDocument> SaveAsync(LocalDocument document, CancellationToken cancellationToken = default);
    Task<LocalDocument> RenameAsync(Guid documentId, long revision, string title, CancellationToken cancellationToken = default);
    Task<LocalDocument> DuplicateAsync(Guid documentId, CancellationToken cancellationToken = default);
    Task<LocalDocument> MoveToTrashAsync(Guid documentId, long revision, CancellationToken cancellationToken = default);
    Task<LocalDocument> RestoreAsync(Guid documentId, long revision, CancellationToken cancellationToken = default);
    Task PermanentlyDeleteAsync(Guid documentId, long revision, CancellationToken cancellationToken = default);
}
