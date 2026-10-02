using WriterApp.Device.Shared.Services;
using WriterApp.Device.Shared.Storage;
using Xunit;

namespace WriterApp.Tests;

public sealed class LocalEditorSessionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "WriterApp.EditorTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task MultiplePagesSaveLocallyAndReopenWithoutServer()
    {
        var repository = new LocalDocumentRepository(new FileLocalDocumentStore(_root));
        LocalDocument document = await repository.CreateAsync("Offline book");
        LocalSection second = document.Sections[0] with { SectionId = Guid.NewGuid(), OrderIndex = 1,
            Pages = [document.Sections[0].Pages[0] with { PageId = Guid.NewGuid() }] };
        document = await repository.SaveAsync(document with { Sections = [document.Sections[0], second] });
        var session = new LocalEditorSession(repository, document);
        session.Edit(document.Sections[0].Pages[0].PageId, "<h1>First</h1><p><strong>Bold</strong></p>");
        session.Edit(second.Pages[0].PageId, "<blockquote><p>Second</p></blockquote>");
        Assert.Equal(LocalSaveState.Unsaved, session.SaveState);
        await session.SaveAsync();
        Assert.False(session.IsDirty);
        Assert.Equal(LocalSaveState.Saved, session.SaveState);
        LocalDocument reopened = (await new LocalDocumentRepository(new FileLocalDocumentStore(_root)).LoadAsync(document.DocumentId))!;
        Assert.Equal("<h1>First</h1><p><strong>Bold</strong></p>", reopened.Sections[0].Pages[0].Content);
        Assert.Equal("<blockquote><p>Second</p></blockquote>", reopened.Sections[1].Pages[0].Content);
    }

    [Fact]
    public async Task FailedSaveRetainsTextAndCanRetry()
    {
        var store = new FileLocalDocumentStore(_root);
        LocalDocument document = await store.CreateAsync("Draft");
        bool fail = true;
        var failing = new FileLocalDocumentStore(_root, TimeProvider.System, new AtomicDocumentWriter(_ =>
        { if (fail) throw new IOException("Simulated full disk"); }));
        var session = new LocalEditorSession(new LocalDocumentRepository(failing), document);
        LocalPage page = document.Sections[0].Pages[0];
        session.Edit(page.PageId, "<p>Keep my writing</p>");
        await Assert.ThrowsAsync<IOException>(() => session.SaveAsync());
        Assert.True(session.IsDirty);
        Assert.Equal(LocalSaveState.Error, session.SaveState);
        Assert.Equal("<p>Keep my writing</p>", session.ContentFor(page));
        Assert.Equal("", (await store.GetAsync(document.DocumentId))!.Sections[0].Pages[0].Content);
        fail = false;
        await session.SaveAsync();
        Assert.False(session.IsDirty);
    }

    [Theory]
    [InlineData("<p>Newer edit</p>")]
    [InlineData("")]
    public async Task EditsDuringSaveIncludingReversionRemainPending(string newer)
    {
        var real = new FileLocalDocumentStore(_root);
        LocalDocument document = await real.CreateAsync("Draft");
        var delayed = new DelayedStore(real);
        var session = new LocalEditorSession(new LocalDocumentRepository(delayed), document);
        LocalPage page = document.Sections[0].Pages[0];
        session.Edit(page.PageId, "<p>In flight</p>");
        Task saving = session.SaveAsync();
        await delayed.Started.Task;
        session.Edit(page.PageId, newer);
        delayed.Release.SetResult();
        await saving;
        Assert.True(session.IsDirty);
        Assert.Equal(newer, session.ContentFor(page));
        await session.SaveAsync();
        Assert.False(session.IsDirty);
        Assert.Equal(newer, (await real.GetAsync(document.DocumentId))!.Sections[0].Pages[0].Content);
    }

    [Fact]
    public async Task StaleSaveKeepsInMemoryWritingAndDoesNotOverwriteDisk()
    {
        var repository = new LocalDocumentRepository(new FileLocalDocumentStore(_root));
        LocalDocument document = await repository.CreateAsync("Draft");
        var session = new LocalEditorSession(repository, document);
        await repository.RenameAsync(document, "Updated elsewhere");
        session.Edit(document.Sections[0].Pages[0].PageId, "<p>Local changes</p>");
        await Assert.ThrowsAsync<LocalDocumentConflictException>(() => session.SaveAsync());
        Assert.True(session.IsDirty);
        Assert.Equal("Updated elsewhere", (await repository.LoadAsync(document.DocumentId))!.Title);
    }

    [Fact]
    public async Task SyncRefreshRejectsChangedWritingAndRetainsUnsavedEdits()
    {
        var repository = new LocalDocumentRepository(new FileLocalDocumentStore(_root));
        var document = await repository.CreateAsync("Draft");
        var session = new LocalEditorSession(repository, document);
        var page = document.Sections[0].Pages[0];
        session.Edit(page.PageId, "<p>Unsaved writing</p>");
        await repository.RenameAsync(document, "Changed elsewhere");

        Assert.False(await session.RefreshSyncMetadataAsync());
        Assert.Equal(document.LocalRevision, session.Document.LocalRevision);
        Assert.Equal("Draft", session.Document.Title);
        Assert.True(session.IsDirty);
        Assert.Equal("<p>Unsaved writing</p>", session.ContentFor(page));
        await Assert.ThrowsAsync<LocalDocumentConflictException>(() => session.SaveAsync());
    }

    internal sealed class DelayedStore(ILocalDocumentStore inner) : ILocalDocumentStore
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<LocalDocument> SaveAsync(LocalDocument document, CancellationToken cancellationToken = default)
        { Started.TrySetResult(); await Release.Task.WaitAsync(cancellationToken); return await inner.SaveAsync(document, cancellationToken); }
        public Task<LocalDocument> CreateAsync(string title, CancellationToken cancellationToken = default) => inner.CreateAsync(title, cancellationToken);
        public Task<LocalDocument> CreateImportedAsync(string title, string html, CancellationToken cancellationToken = default) => inner.CreateImportedAsync(title, html, cancellationToken);
        public Task<LocalDocument?> GetAsync(Guid id, CancellationToken cancellationToken = default) => inner.GetAsync(id, cancellationToken);
        public Task<LocalDocumentList> ListAsync(LocalDocumentScope scope = LocalDocumentScope.Active, CancellationToken cancellationToken = default) => inner.ListAsync(scope, cancellationToken);
        public Task<LocalDocument> RenameAsync(Guid id, long revision, string title, CancellationToken cancellationToken = default) => inner.RenameAsync(id, revision, title, cancellationToken);
        public Task<LocalDocument> DuplicateAsync(Guid id, CancellationToken cancellationToken = default) => inner.DuplicateAsync(id, cancellationToken);
        public Task<LocalDocument> MoveToTrashAsync(Guid id, long revision, CancellationToken cancellationToken = default) => inner.MoveToTrashAsync(id, revision, cancellationToken);
        public Task<LocalDocument> RestoreAsync(Guid id, long revision, CancellationToken cancellationToken = default) => inner.RestoreAsync(id, revision, cancellationToken);
        public Task PermanentlyDeleteAsync(Guid id, long revision, CancellationToken cancellationToken = default) => inner.PermanentlyDeleteAsync(id, revision, cancellationToken);
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
