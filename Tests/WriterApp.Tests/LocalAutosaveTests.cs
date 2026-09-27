using WriterApp.Device.Shared.Services;
using WriterApp.Device.Shared.Storage;
using Xunit;

namespace WriterApp.Tests;

public sealed class LocalAutosaveTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "WriterApp.AutosaveTests", Guid.NewGuid().ToString("N"));
    private LocalRecoveryStore Recovery => new(Path.Combine(_root, "recovery"));
    private LocalDocumentRepository Repository => new(new FileLocalDocumentStore(_root));

    [Fact]
    public async Task RapidTypingCoalescesAndFlushKeepsFinalEdit()
    {
        var clock = new ManualTime();
        var repository = Repository;
        var document = await repository.CreateAsync("Rapid typing");
        var session = new LocalEditorSession(repository, document);
        var saved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int saves = 0;
        LocalAutosaveCoordinator? coordinator = null;
        using var owned = coordinator = new(session, Recovery, async () =>
        { await coordinator!.SaveAsync(); saves++; saved.TrySetResult(); }, clock);
        Guid page = document.Sections[0].Pages[0].PageId;
        for (int i = 0; i < 10; i++)
        {
            await coordinator.EditAsync(page, $"<p>{i}</p>");
            clock.Advance(TimeSpan.FromSeconds(1));
        }
        Assert.Equal(0, saves);
        Assert.Equal("<p>9</p>", Assert.Single((await Recovery.ListAsync()).Records).Document.Sections[0].Pages[0].Content);
        clock.Advance(TimeSpan.FromSeconds(1));
        await saved.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(1, saves);
        Assert.Equal("<p>9</p>", (await repository.LoadAsync(document.DocumentId))!.Sections[0].Pages[0].Content);
        Assert.Empty((await Recovery.ListAsync()).Records);

        await coordinator.EditAsync(page, "<p>Last keystroke before close</p>");
        var lifetime = new DeviceSaveLifetime { Flush = async _ => { await coordinator.SaveAsync(); return true; } };
        Assert.True(await lifetime.FlushAsync(closing: true));
        Assert.Equal("<p>Last keystroke before close</p>", (await repository.LoadAsync(document.DocumentId))!.Sections[0].Pages[0].Content);
    }

    [Fact]
    public async Task ContinuousTypingHasMaximumSaveInterval()
    {
        var clock = new ManualTime();
        var document = await Repository.CreateAsync("Continuous");
        var session = new LocalEditorSession(Repository, document);
        var saved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        LocalAutosaveCoordinator? coordinator = null;
        using var owned = coordinator = new(session, Recovery, async () =>
        { await coordinator!.SaveAsync(); saved.TrySetResult(); }, clock);
        for (int i = 0; i < 15; i++)
        {
            await coordinator.EditAsync(document.Sections[0].Pages[0].PageId, $"<p>{i}</p>");
            clock.Advance(TimeSpan.FromSeconds(1));
        }
        await saved.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.False(session.IsDirty);
        Assert.Equal("<p>14</p>", (await Repository.LoadAsync(document.DocumentId))!.Sections[0].Pages[0].Content);
    }

    [Fact]
    public async Task FailedPrimarySaveKeepsRecoveryAndRetryClearsFailure()
    {
        var document = await Repository.CreateAsync("Disk failure");
        bool fail = true;
        var store = new FileLocalDocumentStore(_root, TimeProvider.System, new AtomicDocumentWriter(_ =>
        { if (fail) throw new IOException("Injected failure"); }));
        var session = new LocalEditorSession(new(store), document);
        using var coordinator = new LocalAutosaveCoordinator(session, Recovery, () => Task.CompletedTask, new ManualTime());
        await coordinator.EditAsync(document.Sections[0].Pages[0].PageId, "<p>Safe recovery</p>");
        await Assert.ThrowsAsync<IOException>(() => coordinator.SaveAsync());
        Assert.NotNull(coordinator.Error);
        Assert.Equal(LocalSaveState.Error, session.SaveState);
        Assert.Single((await Recovery.ListAsync()).Records);
        Assert.Equal("", (await Repository.LoadAsync(document.DocumentId))!.Sections[0].Pages[0].Content);
        fail = false;
        await coordinator.SaveAsync();
        Assert.Null(coordinator.Error);
        Assert.False(session.IsDirty);
        Assert.Empty((await Recovery.ListAsync()).Records);
    }

    [Fact]
    public async Task InterruptedSessionRestoresCopyWithoutOverwritingNewerOriginal()
    {
        var document = await Repository.CreateAsync("Original");
        var session = new LocalEditorSession(Repository, document);
        using (var coordinator = new LocalAutosaveCoordinator(session, Recovery, () => Task.CompletedTask, new ManualTime()))
            await coordinator.EditAsync(document.Sections[0].Pages[0].PageId, "<p>Unsaved at interruption</p>");
        await Repository.RenameAsync(document, "Changed elsewhere");
        var service = new LocalRecoveryService(Recovery, Repository);
        var candidate = Assert.Single((await service.DiscoverAsync()).Records);
        var copy = await service.RestoreAsync(candidate);
        Assert.NotEqual(document.DocumentId, copy.DocumentId);
        Assert.NotEqual(document.Sections[0].Pages[0].PageId, copy.Sections[0].Pages[0].PageId);
        Assert.Null(copy.ServerDocumentId);
        Assert.Equal("<p>Unsaved at interruption</p>", copy.Sections[0].Pages[0].Content);
        Assert.Equal("Changed elsewhere", (await Repository.LoadAsync(document.DocumentId))!.Title);
        Assert.Empty((await service.DiscoverAsync()).Records);
    }

    [Fact]
    public async Task CrashAfterCommitDoesNotOfferObsoleteRecoveryAndReversionIsPreserved()
    {
        var document = await Repository.CreateAsync("Reversion");
        var session = new LocalEditorSession(Repository, document);
        using var coordinator = new LocalAutosaveCoordinator(session, Recovery, () => Task.CompletedTask, new ManualTime());
        Guid page = document.Sections[0].Pages[0].PageId;
        await coordinator.EditAsync(page, "<p>Temporary</p>");
        await coordinator.EditAsync(page, "");
        Assert.Empty((await new LocalRecoveryService(Recovery, Repository).DiscoverAsync()).Records);
        await coordinator.EditAsync(page, "<p>Committed</p>");
        await session.SaveAsync(); // Simulate termination before coordinator's journal cleanup.
        Assert.Empty((await new LocalRecoveryService(Recovery, Repository).DiscoverAsync()).Records);
    }

    [Fact]
    public async Task JournalFailureDoesNotBlockPrimarySaveAndCorruptJournalDoesNotHideHealthyRecord()
    {
        var document = await Repository.CreateAsync("Journal failure");
        var journal = new LocalRecoveryStore(Path.Combine(_root, "recovery"), TimeProvider.System,
            new AtomicDocumentWriter(_ => throw new IOException("Journal volume failure")));
        var session = new LocalEditorSession(Repository, document);
        using var coordinator = new LocalAutosaveCoordinator(session, journal, () => Task.CompletedTask, new ManualTime());
        await coordinator.EditAsync(document.Sections[0].Pages[0].PageId, "<p>Primary still works</p>");
        Assert.NotNull(coordinator.Error);
        await coordinator.SaveAsync();
        Assert.Null(coordinator.Error);
        Assert.False(session.IsDirty);
        await Recovery.WriteAsync(session.Document);
        await File.WriteAllTextAsync(Path.Combine(_root, "recovery", "broken.json"), "{broken");
        var list = await Recovery.ListAsync();
        Assert.Single(list.Records);
        Assert.Single(list.Issues);
        Assert.True(File.Exists(Path.Combine(_root, "recovery", "broken.json")));
    }

    [Fact]
    public async Task SeparateSessionsCannotReplaceEachOthersRecovery()
    {
        var document = await Repository.CreateAsync("Two windows");
        var first = new LocalEditorSession(Repository, document);
        var second = new LocalEditorSession(Repository, document);
        using var a = new LocalAutosaveCoordinator(first, Recovery, () => Task.CompletedTask, new ManualTime());
        using var b = new LocalAutosaveCoordinator(second, Recovery, () => Task.CompletedTask, new ManualTime());
        Guid page = document.Sections[0].Pages[0].PageId;
        await a.EditAsync(page, "<p>First window</p>");
        await b.EditAsync(page, "<p>Second window</p>");
        Assert.Equal(2, (await Recovery.ListAsync()).Records.Count);
        await a.SaveAsync();
        await Assert.ThrowsAsync<LocalDocumentConflictException>(() => b.SaveAsync());
        Assert.Equal("<p>Second window</p>", Assert.Single((await Recovery.ListAsync()).Records).Document.Sections[0].Pages[0].Content);
    }

    [Fact]
    public async Task EditDuringPendingSaveRemainsRecoverableAndFinalFlushCommitsIt()
    {
        var real = new FileLocalDocumentStore(_root);
        var document = await real.CreateAsync("Pending save");
        var delayed = new LocalEditorSessionTests.DelayedStore(real);
        var session = new LocalEditorSession(new(delayed), document);
        using var coordinator = new LocalAutosaveCoordinator(session, Recovery, () => Task.CompletedTask, new ManualTime());
        Guid page = document.Sections[0].Pages[0].PageId;
        await coordinator.EditAsync(page, "<p>First snapshot</p>");
        Task saving = coordinator.SaveAsync();
        await delayed.Started.Task;
        Task editing = coordinator.EditAsync(page, "<p>Final edit</p>");
        Task closing = coordinator.SaveAsync();
        delayed.Release.TrySetResult();
        await Task.WhenAll(saving, editing, closing);
        Assert.False(session.IsDirty);
        Assert.Equal("<p>Final edit</p>", (await real.GetAsync(document.DocumentId))!.Sections[0].Pages[0].Content);
        Assert.Empty((await Recovery.ListAsync()).Records);
    }

    [Fact]
    public async Task InterruptedJournalReplacementPreservesPreviousRecord()
    {
        var document = await Repository.CreateAsync("Atomic journal");
        await Recovery.WriteAsync(document);
        var journal = new LocalRecoveryStore(Path.Combine(_root, "recovery"), TimeProvider.System,
            new AtomicDocumentWriter(_ => throw new IOException("Power loss before replacement")));
        await Assert.ThrowsAsync<IOException>(() => journal.WriteAsync(document with { Title = "Uncommitted" }));
        Assert.Equal("Atomic journal", Assert.Single((await Recovery.ListAsync()).Records).Document.Title);
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }

    private sealed class ManualTime : TimeProvider
    {
        private TimeSpan _now;
        private readonly List<ManualTimer> _timers = [];
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new ManualTimer(this, callback, state);
            _timers.Add(timer); timer.Change(dueTime, period); return timer;
        }
        public void Advance(TimeSpan elapsed)
        {
            _now += elapsed;
            foreach (var timer in _timers.ToArray()) timer.FireIfDue();
        }
        private sealed class ManualTimer(ManualTime owner, TimerCallback callback, object? state) : ITimer
        {
            private TimeSpan? _due;
            public bool Change(TimeSpan dueTime, TimeSpan period)
            { _due = dueTime == Timeout.InfiniteTimeSpan ? null : owner._now + dueTime; return true; }
            public void FireIfDue()
            { if (_due is not null && _due <= owner._now) { _due = null; callback(state); } }
            public void Dispose() => _due = null;
            public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
        }
    }
}
