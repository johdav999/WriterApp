using WriterApp.Device.Shared.Storage;

namespace WriterApp.Device.Shared.Services;

/// <summary>Device UI entry point. Callers use document identities and returned revisions, never filesystem paths.</summary>
public sealed class LocalDocumentRepository(ILocalDocumentStore store)
{
    public event Action? Changed;
    private readonly Dictionary<Guid, int> _editing = [];
    public void BeginEditing(Guid id) { lock (_editing) _editing[id] = _editing.GetValueOrDefault(id) + 1; }
    public void EndEditing(Guid id)
    { lock (_editing) { if (_editing.GetValueOrDefault(id) <= 1) _editing.Remove(id); else _editing[id]--; } }
    internal bool IsEditing(Guid id) { lock (_editing) return _editing.ContainsKey(id); }
    private async Task<LocalDocument> Notify(Task<LocalDocument> task)
    { var result = await task; Changed?.Invoke(); return result; }
    public Task<LocalDocument> CreateAsync(string title, CancellationToken cancellationToken = default) =>
        Notify(store.CreateAsync(title, cancellationToken));
    public Task<LocalDocument?> LoadAsync(Guid id, CancellationToken cancellationToken = default) =>
        store.GetAsync(id, cancellationToken);
    public Task<LocalDocumentList> ListAsync(LocalDocumentScope scope = LocalDocumentScope.Active, CancellationToken cancellationToken = default) =>
        store.ListAsync(scope, cancellationToken);
    public Task<LocalDocument> SaveAsync(LocalDocument document, CancellationToken cancellationToken = default) =>
        Notify(store.SaveAsync(document, cancellationToken));
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
