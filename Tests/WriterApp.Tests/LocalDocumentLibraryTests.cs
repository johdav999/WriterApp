using WriterApp.Device.Shared.Services;
using WriterApp.Device.Shared.Storage;
using Xunit;

namespace WriterApp.Tests;

public sealed class LocalDocumentLibraryTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "WriterApp.LibraryTests", Guid.NewGuid().ToString("N"));

    private LocalDocumentLibrary NewLibrary() => new(new LocalDocumentRepository(new FileLocalDocumentStore(_root)));

    [Fact]
    public async Task SignedOutOfflineLibraryKeepsLifecycleStateAcrossRestart()
    {
        LocalDocumentLibrary library = NewLibrary();
        await library.RefreshAsync(LocalDocumentScope.Active);
        Assert.Empty(library.Documents);

        LocalDocument original = await library.CreateAsync("My first story");
        Assert.Equal(original.DocumentId, Assert.Single(library.Documents).DocumentId);
        original = await library.RenameAsync(original, "Revised story");
        LocalDocument copy = await library.DuplicateAsync(original);
        Assert.Equal(2, library.Documents.Count);
        Assert.Equal("Revised story (copy)", copy.Title);

        await library.MoveToTrashAsync(original);
        Assert.Equal(copy.DocumentId, Assert.Single(library.Documents).DocumentId);
        await library.RefreshAsync(LocalDocumentScope.Trash);
        Assert.Equal(original.DocumentId, Assert.Single(library.Documents).DocumentId);

        LocalDocumentLibrary restarted = NewLibrary();
        await restarted.RefreshAsync(LocalDocumentScope.Trash);
        Assert.Equal(original.DocumentId, Assert.Single(restarted.Documents).DocumentId);
        await restarted.RestoreAsync(restarted.Documents[0]);
        Assert.Empty(restarted.Documents);
        await restarted.RefreshAsync(LocalDocumentScope.Active);
        Assert.Equal(2, restarted.Documents.Count);

        LocalDocument restored = restarted.Documents.Single(item => item.DocumentId == original.DocumentId);
        await restarted.MoveToTrashAsync(restored);
        await restarted.RefreshAsync(LocalDocumentScope.Trash);
        await restarted.PermanentlyDeleteAsync(Assert.Single(restarted.Documents));
        Assert.Empty(restarted.Documents);
        await restarted.RefreshAsync(LocalDocumentScope.Active);
        Assert.Equal(copy.DocumentId, Assert.Single(restarted.Documents).DocumentId);
    }

    [Fact]
    public async Task StaleMutationDoesNotReportSuccessAndRefreshShowsActualDiskState()
    {
        LocalDocumentLibrary library = NewLibrary();
        LocalDocument document = await library.CreateAsync("First title");
        LocalDocumentLibrary other = NewLibrary();
        await other.RefreshAsync(LocalDocumentScope.Active);
        await other.RenameAsync(other.Documents[0], "Changed elsewhere");

        await Assert.ThrowsAsync<LocalDocumentConflictException>(() => library.RenameAsync(document, "Stale title"));
        Assert.Equal("First title", Assert.Single(library.Documents).Title);
        await library.RefreshAsync(LocalDocumentScope.Active);
        Assert.Equal("Changed elsewhere", Assert.Single(library.Documents).Title);
        Assert.Equal(2, Assert.Single(library.Documents).LocalRevision);
    }

    [Fact]
    public async Task BrokenFileAppearsAsIssueAlongsideHealthyDocument()
    {
        LocalDocumentLibrary library = NewLibrary();
        LocalDocument healthy = await library.CreateAsync("Available");
        await File.WriteAllTextAsync(Path.Combine(_root, $"{Guid.NewGuid():N}.json"), "{damaged");
        await library.RefreshAsync(LocalDocumentScope.Active);

        Assert.Equal(healthy.DocumentId, Assert.Single(library.Documents).DocumentId);
        Assert.Equal(LocalDocumentIssueKind.Corrupt, Assert.Single(library.Issues).Kind);
        await library.RefreshAsync(LocalDocumentScope.Trash);
        Assert.Empty(library.Documents);
        Assert.Single(library.Issues);
    }

    [Fact]
    public async Task StatusLabelsPrioritizeUnsavedWorkAndNeverDescribePendingChangesAsSynced()
    {
        LocalDocument local = await NewLibrary().CreateAsync("Draft");
        Assert.Equal("Offline · Saved locally", DeviceDocumentStatus.For(local).Label);
        Assert.Equal("Unsaved", DeviceDocumentStatus.For(local, LocalSaveState.Unsaved).Label);
        Assert.Equal("Saving", DeviceDocumentStatus.For(local, LocalSaveState.Saving).Label);
        Assert.Equal("Save failed", DeviceDocumentStatus.For(local, LocalSaveState.Error).Label);

        LocalDocument cloud = local with { ServerDocumentId = Guid.NewGuid(), SyncState = LocalSyncState.PendingUpload };
        Assert.Equal("Waiting to sync", DeviceDocumentStatus.For(cloud).Label);
        Assert.Equal("Syncing", DeviceDocumentStatus.For(cloud, isSyncing: true).Label);
        Assert.Equal("Sync error", DeviceDocumentStatus.For(cloud with { SyncState = LocalSyncState.Error }).Label);
        Assert.Equal("Conflict", DeviceDocumentStatus.For(cloud with { SyncState = LocalSyncState.Conflict }, isSyncing: true).Label);
        Assert.Equal("Synced", DeviceDocumentStatus.For(cloud with { SyncState = LocalSyncState.Synced }).Label);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
