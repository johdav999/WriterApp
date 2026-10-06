using WriterApp.Device.Shared.Storage;

namespace WriterApp.Device.Shared.Services;

/// <summary>Device UI entry point. Callers use document identities and returned revisions, never filesystem paths.</summary>
public sealed class LocalDocumentRepository(ILocalDocumentStore store)
{
    public Task<LocalDocument> SetProjectCoverAsync(LocalDocument source, string? cover, Guid changeId, bool restore = false, CancellationToken ct = default) =>
        Notify(store.SetProjectCoverAsync(source, cover, changeId, restore, ct));
    public Task<LocalDocument> CreateTranslationCopyAsync(LocalDocument copy, CancellationToken ct = default) => Notify(store.CreateTranslationCopyAsync(copy, ct));
    public event Action? Changed;
    public Task<LocalDocument> RecoverSnapshotAsync(LocalDocument source, CancellationToken cancellationToken = default) =>
        Notify(store.RecoverSnapshotAsync(source, cancellationToken));
    private readonly Dictionary<Guid, int> _editing = [];
    public void BeginEditing(Guid id) { lock (_editing) _editing[id] = _editing.GetValueOrDefault(id) + 1; }
    public void EndEditing(Guid id)
    { lock (_editing) { if (_editing.GetValueOrDefault(id) <= 1) _editing.Remove(id); else _editing[id]--; } }
    internal bool IsEditing(Guid id) { lock (_editing) return _editing.ContainsKey(id); }
    private async Task<LocalDocument> Notify(Task<LocalDocument> task)
    { var result = await task; Changed?.Invoke(); return result; }
    public Task<LocalDocument> CreateAsync(string title, CancellationToken cancellationToken = default) =>
        Notify(store.CreateAsync(title, cancellationToken));
    public Task<LocalDocument> CreateProjectAsync(string title, CancellationToken cancellationToken = default) =>
        Notify(store.CreateProjectAsync(title, cancellationToken));
    public Task<LocalDocument> CreateProjectDocumentAsync(Guid projectId, string title, string kind, CancellationToken ct = default) =>
        Notify(store.CreateProjectDocumentAsync(projectId, title, kind, ct));
    public Task<LocalDocument> MoveStandaloneToProjectAsync(LocalDocument source, Guid targetDocumentId, CancellationToken ct = default) =>
        Notify(store.MoveStandaloneToProjectAsync(source, targetDocumentId, ct));
    public Task<LocalDocument> MakePrimaryAsync(LocalDocument document, CancellationToken ct = default) =>
        Notify(store.UpdateProjectMetadataAsync(document, makePrimary: true, ct: ct));
    public Task<LocalDocument> CreateImportedAsync(string title, string html, CancellationToken cancellationToken = default) =>
        Notify(store.CreateImportedAsync(title, html, cancellationToken));
    public Task<LocalDocument?> LoadAsync(Guid id, CancellationToken cancellationToken = default) =>
        store.GetAsync(id, cancellationToken);
    public Task<LocalDocumentList> ListAsync(LocalDocumentScope scope = LocalDocumentScope.Active, CancellationToken cancellationToken = default) =>
        store.ListAsync(scope, cancellationToken);
    public Task<LocalDocument> SaveAsync(LocalDocument document, CancellationToken cancellationToken = default) =>
        Notify(store.SaveAsync(document, cancellationToken));
    public Task<LocalDocument> AttachProjectAsync(LocalDocument document, string title, CancellationToken cancellationToken = default) =>
        SaveAsync(LocalProjectStructure.Attach(document, title), cancellationToken);
    public Task<LocalDocument> ChangeProjectAsync(LocalDocument document, LocalProjectChange change, CancellationToken cancellationToken = default) =>
        change.Action == LocalProjectAction.RenameProject
            ? Notify(store.UpdateProjectMetadataAsync(document, title: change.Title, ct: cancellationToken))
            : SaveAsync(LocalProjectStructure.Apply(document, change, DateTimeOffset.UtcNow), cancellationToken);
    public Task<LocalDocument> ChangeStructureAsync(LocalDocument document, LocalStructureChange change,
        CancellationToken cancellationToken = default) =>
        SaveAsync(LocalDocumentStructure.Apply(document, change, DateTimeOffset.UtcNow), cancellationToken);
    public Task<LocalDocument> RenameAsync(LocalDocument document, string title, CancellationToken cancellationToken = default) =>
        Notify(store.RenameAsync(document.DocumentId, document.LocalRevision, title, cancellationToken));
    public Task<LocalDocument> DuplicateAsync(Guid id, CancellationToken cancellationToken = default) =>
        Notify(store.DuplicateAsync(id, cancellationToken));
    public Task<LocalDocument> MoveToTrashAsync(LocalDocument document, CancellationToken cancellationToken = default) =>
        Notify(store.MoveToTrashAsync(document.DocumentId, document.LocalRevision, cancellationToken));
    public Task<LocalDocument> RestoreAsync(LocalDocument document, CancellationToken cancellationToken = default) =>
        Notify(store.RestoreAsync(document.DocumentId, document.LocalRevision, cancellationToken));
    public Task PermanentlyDeleteAsync(LocalDocument document, CancellationToken cancellationToken = default) =>
        store.PermanentlyDeleteAsync(document.DocumentId, document.LocalRevision, cancellationToken);
}
