using WriterApp.Device.Shared.Storage;

namespace WriterApp.Tests;

// A slow in-flight file read deliberately ignores cancellation, as OS I/O may do.
internal sealed class PanelReadGateStore(string root) : ILocalDocumentStore
{
    public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public async Task<LocalDocument?> GetAsync(Guid id, CancellationToken ct = default)
    {
        using (var lease = new FileStream(Path.Combine(root, ".store.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None)) {
            Entered.TrySetResult();
            await Release.Task;
        }
        return await new FileLocalDocumentStore(root).GetAsync(id);
    }
    public Task<LocalDocument> CreateAsync(string title, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<LocalDocument> CreateImportedAsync(string title, string html, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<LocalDocumentList> ListAsync(LocalDocumentScope scope = LocalDocumentScope.Active, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<LocalDocument> SaveAsync(LocalDocument document, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<LocalDocument> RenameAsync(Guid id, long revision, string title, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<LocalDocument> DuplicateAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<LocalDocument> MoveToTrashAsync(Guid id, long revision, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<LocalDocument> RestoreAsync(Guid id, long revision, CancellationToken ct = default) => throw new NotSupportedException();
    public Task PermanentlyDeleteAsync(Guid id, long revision, CancellationToken ct = default) => throw new NotSupportedException();
}
