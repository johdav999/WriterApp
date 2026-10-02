using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using WriterApp.Device.Shared.Services;
using WriterApp.Device.Shared.Storage;
using Xunit;

namespace WriterApp.Tests;

public sealed class FileLocalDocumentStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "WriterApp.Tests", Guid.NewGuid().ToString("N"));
    private readonly TestClock _clock = new();
    private FileLocalDocumentStore Store() => new(_root, _clock);
    private string DocumentPath(Guid id) => Path.Combine(_root, $"{id:N}.json");

    [Fact]
    public async Task LifecycleSurvivesRestartAndTrashProtectsAgainstAccidentalDelete()
    {
        var repository = new LocalDocumentRepository(Store());
        LocalDocument first = await repository.CreateAsync("  Första ✍  ");
        Assert.Equal("Första ✍", first.Title);
        Assert.Equal(LocalContentFormat.Html, first.Sections[0].Pages[0].ContentFormat);
        Assert.Equal(1, first.LocalRevision);
        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.PermanentlyDeleteAsync(first));

        _clock.Advance();
        LocalDocument second = await repository.CreateAsync("Second");
        Assert.Equal(new[] { second.DocumentId, first.DocumentId },
            (await repository.ListAsync()).Documents.Select(document => document.DocumentId));

        _clock.Advance();
        first = await repository.RenameAsync(first, "Renamed");
        Assert.Equal(2, first.LocalRevision);
        Assert.Equal(first.DocumentId, (await repository.ListAsync()).Documents[0].DocumentId);
        repository = new LocalDocumentRepository(Store());
        Assert.Equal("Renamed", (await repository.LoadAsync(first.DocumentId))!.Title);
        _clock.Advance();
        first = await repository.MoveToTrashAsync(first);
        Assert.NotNull(first.DeletedAtUtc);
        Assert.Equal(_clock.GetUtcNow(), first.DeletedAtUtc);
        Assert.Equal(first.DeletedAtUtc, first.UpdatedAtUtc);
        Assert.Single((await repository.ListAsync()).Documents);
        Assert.Equal(first.DocumentId, Assert.Single((await repository.ListAsync(LocalDocumentScope.Trash)).Documents).DocumentId);
        Assert.Equal(2, (await repository.ListAsync(LocalDocumentScope.All)).Documents.Count);
        Assert.NotNull((await repository.LoadAsync(first.DocumentId))!.DeletedAtUtc);
        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.SaveAsync(first));
        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.RenameAsync(first, "Hidden edit"));
        first = await repository.RestoreAsync(first);
        Assert.Null(first.DeletedAtUtc);
        Assert.Equal(2, (await repository.ListAsync()).Documents.Count);
        first = await repository.MoveToTrashAsync(first);
        await repository.PermanentlyDeleteAsync(first);
        Assert.Null(await new LocalDocumentRepository(Store()).LoadAsync(first.DocumentId));
        Assert.Single((await repository.ListAsync(LocalDocumentScope.All)).Documents);
    }

    [Fact]
    public async Task MultiSectionHtmlAndMetadataRoundTripInExplicitOrder()
    {
        var store = Store();
        LocalDocument document = await store.CreateAsync("Novel");
        LocalSection first = document.Sections[0] with { LanguageCode = "sv", NarrativePurpose = "Opening" };
        LocalPage page = first.Pages[0] with { Content = "<p>Hej <strong>världen</strong> 🌍</p>" };
        first = first with { Pages = [page with { PageId = Guid.NewGuid(), OrderIndex = 1, Content = "<h2>Next</h2>" }, page] };
        LocalSection second = first with { SectionId = Guid.NewGuid(), Title = "Chapter 2", OrderIndex = 1,
            Pages = [page with { PageId = Guid.NewGuid(), Content = "<p>The end.</p>" }] };
        _clock.Advance();
        document = await store.SaveAsync(document with { LanguageCode = "sv", Sections = [second, first] });
        LocalDocument loaded = (await Store().GetAsync(document.DocumentId))!;
        Assert.Equal(new[] { first.SectionId, second.SectionId }, loaded.Sections.Select(section => section.SectionId));
        Assert.Equal(new[] { 0, 1 }, loaded.Sections[0].Pages.Select(item => item.OrderIndex));
        Assert.Equal(page.Content, loaded.Sections[0].Pages[0].Content);
        Assert.Equal("Opening", loaded.Sections[0].NarrativePurpose);
        Assert.Equal("sv", loaded.LanguageCode);
        Assert.Equal(document.UpdatedAtUtc, loaded.Sections[0].Pages[0].UpdatedAtUtc);
        using JsonDocument json = JsonDocument.Parse(await File.ReadAllTextAsync(DocumentPath(document.DocumentId)));
        Assert.Equal(4, json.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("Html", json.RootElement.GetProperty("document").GetProperty("sections")[0]
            .GetProperty("pages")[0].GetProperty("contentFormat").GetString());
    }

    [Fact]
    public async Task DuplicateDetachesAllIdentitiesAndLinkedTrashIsRetained()
    {
        LocalDocument original = await Store().CreateAsync("Original");
        original = original with { ServerDocumentId = Guid.NewGuid(), ServerProjectId = Guid.NewGuid(),
            ServerVersion = "opaque-etag", SyncState = LocalSyncState.Synced, LastSyncedAtUtc = _clock.GetUtcNow(),
            Sections = [original.Sections[0] with { ServerSectionId = Guid.NewGuid(),
                Pages = [original.Sections[0].Pages[0] with { ServerPageId = Guid.NewGuid(), Content = "<p>Keep me</p>" }] }] };
        await SeedAsync(original);
        original = await Store().MoveToTrashAsync(original.DocumentId, original.LocalRevision);
        _clock.Advance();
        LocalDocument copy = await Store().DuplicateAsync(original.DocumentId);
        Assert.NotEqual(original.DocumentId, copy.DocumentId);
        Assert.Equal("Original (copy)", copy.Title);
        Assert.Null(copy.DeletedAtUtc);
        Assert.Null(copy.ServerDocumentId);
        Assert.Null(copy.ServerProjectId);
        Assert.Null(copy.ServerVersion);
        Assert.Null(copy.LastSyncedAtUtc);
        Assert.Equal(LocalSyncState.LocalOnly, copy.SyncState);
        Assert.Equal(1, copy.LocalRevision);
        Assert.NotEqual(original.Sections[0].SectionId, copy.Sections[0].SectionId);
        Assert.NotEqual(original.Sections[0].Pages[0].PageId, copy.Sections[0].Pages[0].PageId);
        Assert.Null(copy.Sections[0].ServerSectionId);
        Assert.Null(copy.Sections[0].Pages[0].ServerPageId);
        Assert.Equal("<p>Keep me</p>", (await Store().GetAsync(copy.DocumentId))!.Sections[0].Pages[0].Content);
        Assert.Equal(LocalSyncState.PendingUpload, original.SyncState);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Store().PermanentlyDeleteAsync(original.DocumentId, original.LocalRevision));
        // Local copies are fully deletable; linked trash must await the future sync deletion protocol.
        copy = await Store().MoveToTrashAsync(copy.DocumentId, copy.LocalRevision);
        await Store().PermanentlyDeleteAsync(copy.DocumentId, copy.LocalRevision);
        Assert.NotNull(await Store().GetAsync(original.DocumentId));
    }

    [Fact]
    public async Task InterruptedReplacementPreservesLastValidDocumentAndIgnoresOrphan()
    {
        LocalDocument document = await Store().CreateAsync("Saved");
        byte[] before = await File.ReadAllBytesAsync(DocumentPath(document.DocumentId));
        var failing = new FileLocalDocumentStore(_root, _clock, new AtomicDocumentWriter(staged =>
        {
            Assert.Contains("Unsaved", File.ReadAllText(staged));
            File.Copy(staged, DocumentPath(document.DocumentId) + ".crash.tmp");
            throw new IOException("Simulated interruption after flush, before replacement.");
        }));
        await Assert.ThrowsAsync<IOException>(() => failing.RenameAsync(document.DocumentId, document.LocalRevision, "Unsaved"));
        Assert.Equal(before, await File.ReadAllBytesAsync(DocumentPath(document.DocumentId)));
        Assert.Equal("Saved", (await Store().GetAsync(document.DocumentId))!.Title);
        Assert.Single((await Store().ListAsync()).Documents);
        Assert.Empty((await Store().ListAsync()).Issues);
        document = await Store().RenameAsync(document.DocumentId, document.LocalRevision, "Retry succeeded");
        Assert.Equal(2, document.LocalRevision);
    }

    [Fact]
    public async Task CancellationAfterFlushDoesNotReplaceDocument()
    {
        LocalDocument document = await Store().CreateAsync("Saved");
        using var cancellation = new CancellationTokenSource();
        var failing = new FileLocalDocumentStore(_root, _clock, new AtomicDocumentWriter(_ => cancellation.Cancel()));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => failing.RenameAsync(document.DocumentId, document.LocalRevision, "Cancelled", cancellation.Token));
        Assert.Equal("Saved", (await Store().GetAsync(document.DocumentId))!.Title);
        Assert.Empty(Directory.EnumerateFiles(_root, "*.tmp"));
    }

    [Theory]
    [InlineData("{broken", LocalDocumentIssueKind.Corrupt)]
    [InlineData("null", LocalDocumentIssueKind.Corrupt)]
    [InlineData("{}", LocalDocumentIssueKind.Corrupt)]
    [InlineData("{\"schemaVersion\":1}", LocalDocumentIssueKind.Corrupt)]
    [InlineData("{\"schemaVersion\":99,\"document\":{}}", LocalDocumentIssueKind.UnsupportedVersion)]
    [InlineData("{\"schemaVersion\":\"future\"}", LocalDocumentIssueKind.UnsupportedVersion)]
    public async Task UnreadableFilesDoNotHideHealthyDocumentsAndAreNeverOverwritten(string data, LocalDocumentIssueKind kind)
    {
        LocalDocument healthy = await Store().CreateAsync("Healthy");
        Guid brokenId = Guid.NewGuid();
        string path = DocumentPath(brokenId);
        await File.WriteAllTextAsync(path, data);
        LocalDocumentList list = await Store().ListAsync();
        Assert.Equal(healthy.DocumentId, Assert.Single(list.Documents).DocumentId);
        Assert.Equal(kind, Assert.Single(list.Issues).Kind);
        Assert.Equal(Path.GetFileName(path), list.Issues[0].FileName);
        LocalDocumentReadException error = await Assert.ThrowsAsync<LocalDocumentReadException>(() => Store().GetAsync(brokenId));
        Assert.Equal(kind, error.Issue.Kind);
        await Assert.ThrowsAsync<LocalDocumentReadException>(() => Store().SaveAsync(healthy with { DocumentId = brokenId }));
        Assert.Equal(data, await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task IdentityMismatchAndInvalidNestedDataAreReportedWithoutDiscardingBytes()
    {
        LocalDocument document = await Store().CreateAsync("Healthy");
        Guid mismatch = Guid.NewGuid();
        await File.WriteAllBytesAsync(DocumentPath(mismatch), LocalDocumentCodec.Encode(document));
        LocalDocument invalid = document with { DocumentId = Guid.NewGuid(), Sections = [document.Sections[0] with { Pages = null! }] };
        await File.WriteAllTextAsync(DocumentPath(invalid.DocumentId), JsonSerializer.Serialize(new { schemaVersion = 1, document = invalid },
            new JsonSerializerOptions(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } }));
        LocalDocumentList result = await Store().ListAsync();
        Assert.Single(result.Documents);
        Assert.Equal(2, result.Issues.Count);
        Assert.All(result.Issues, issue => Assert.Equal(LocalDocumentIssueKind.Corrupt, issue.Kind));
    }

    [Theory]
    [InlineData("<p>Legacy HTML &amp; text</p>", LocalContentFormat.Html)]
    [InlineData("{\"type\":\"doc\",\"content\":[]}", LocalContentFormat.LegacyJson)]
    [InlineData("An old plain draft", LocalContentFormat.LegacyText)]
    public async Task MigrationPreservesExactPayloadAndBackupAndIsIdempotent(string content, LocalContentFormat format)
    {
        Guid id = Guid.NewGuid();
        byte[] legacy = await SeedLegacyAsync(id, content);
        LocalDocument document = (await Store().GetAsync(id))!;
        Assert.Equal(id, document.DocumentId);
        Assert.Equal("Legacy", document.Title);
        Assert.Equal(_clock.GetUtcNow(), document.CreatedAtUtc);
        Assert.Equal("legacy-version", document.ServerVersion);
        Assert.Null(document.ServerDocumentId);
        Assert.Equal(content, document.Sections[0].Pages[0].Content);
        Assert.Equal(format, document.Sections[0].Pages[0].ContentFormat);
        Assert.Equal(legacy, await File.ReadAllBytesAsync(DocumentPath(id) + ".legacy.bak"));
        LocalDocument again = (await Store().GetAsync(id))!;
        Assert.Equal(document.Sections[0].SectionId, again.Sections[0].SectionId);
        Assert.Equal(document.Sections[0].Pages[0].PageId, again.Sections[0].Pages[0].PageId);
        document = await Store().RenameAsync(id, document.LocalRevision, "Edited");
        Assert.Equal(legacy, await File.ReadAllBytesAsync(DocumentPath(id) + ".legacy.bak"));
        document = await Store().MoveToTrashAsync(id, document.LocalRevision);
        await File.WriteAllTextAsync(DocumentPath(id) + ".tmp", "old staging");
        await Store().PermanentlyDeleteAsync(id, document.LocalRevision);
        Assert.Empty(Directory.EnumerateFiles(_root, $"{id:N}.json*"));
    }

    [Fact]
    public async Task InterruptedMigrationCanRetryWithoutLosingLegacySource()
    {
        Guid id = Guid.NewGuid();
        byte[] legacy = await SeedLegacyAsync(id, "<p>Keep this</p>");
        var failing = new FileLocalDocumentStore(_root, _clock, new AtomicDocumentWriter(path =>
        {
            if (!path.Contains(".legacy.bak.", StringComparison.Ordinal)) throw new IOException("Stop migration");
        }));
        LocalDocumentList failed = await failing.ListAsync();
        Assert.Empty(failed.Documents);
        Assert.Equal(LocalDocumentIssueKind.Unavailable, Assert.Single(failed.Issues).Kind);
        Assert.Equal(legacy, await File.ReadAllBytesAsync(DocumentPath(id)));
        Assert.Equal(legacy, await File.ReadAllBytesAsync(DocumentPath(id) + ".legacy.bak"));
        Assert.Equal("<p>Keep this</p>", (await Store().GetAsync(id))!.Sections[0].Pages[0].Content);
    }

    [Fact]
    public async Task MismatchedMigrationBackupIsPreservedAndSurfaced()
    {
        Guid id = Guid.NewGuid();
        byte[] legacy = await SeedLegacyAsync(id, "Original");
        await File.WriteAllTextAsync(DocumentPath(id) + ".legacy.bak", "different backup");
        Assert.Single((await Store().ListAsync()).Issues);
        Assert.Equal(legacy, await File.ReadAllBytesAsync(DocumentPath(id)));
        Assert.Equal("different backup", await File.ReadAllTextAsync(DocumentPath(id) + ".legacy.bak"));
    }

    [Fact]
    public async Task StaleMutationsCannotOverwriteRenameRestoreOrDelete()
    {
        LocalDocument original = await Store().CreateAsync("Original");
        LocalDocument current = await Store().RenameAsync(original.DocumentId, original.LocalRevision, "New title");
        await Assert.ThrowsAsync<LocalDocumentConflictException>(() => Store().SaveAsync(original));
        await Assert.ThrowsAsync<LocalDocumentConflictException>(() => Store().RenameAsync(original.DocumentId, original.LocalRevision, "Stale"));
        await Assert.ThrowsAsync<LocalDocumentConflictException>(() => Store().MoveToTrashAsync(original.DocumentId, original.LocalRevision));
        await Assert.ThrowsAsync<LocalDocumentConflictException>(() => Store().RestoreAsync(original.DocumentId, original.LocalRevision));
        await Assert.ThrowsAsync<LocalDocumentConflictException>(() => Store().PermanentlyDeleteAsync(original.DocumentId, original.LocalRevision));
        current = await Store().MoveToTrashAsync(current.DocumentId, current.LocalRevision);
        LocalDocument staleTrash = current;
        current = await Store().RestoreAsync(current.DocumentId, current.LocalRevision);
        await Assert.ThrowsAsync<LocalDocumentConflictException>(() => Store().PermanentlyDeleteAsync(staleTrash.DocumentId, staleTrash.LocalRevision));
        Assert.Equal(current.LocalRevision, (await Store().GetAsync(current.DocumentId))!.LocalRevision);
    }

    [Fact]
    public async Task ConcurrentSnapshotsWithinOneStoreProduceOneWinner()
    {
        var store = Store();
        LocalDocument document = await store.CreateAsync("Original");
        async Task<bool> TryRename(string title)
        {
            try { await store.RenameAsync(document.DocumentId, document.LocalRevision, title); return true; }
            catch (LocalDocumentConflictException) { return false; }
        }
        bool[] results = await Task.WhenAll(TryRename("A"), TryRename("B"));
        Assert.Single(results, success => success);
        Assert.Equal(2, (await store.GetAsync(document.DocumentId))!.LocalRevision);
    }

    [Fact]
    public async Task CompetingStoreCannotWriteWhileAnotherHoldsFilesystemLease()
    {
        LocalDocument document = await Store().CreateAsync("Original");
        using (var lease = new FileStream(Path.Combine(_root, ".store.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            await Assert.ThrowsAsync<IOException>(() => Store().RenameAsync(document.DocumentId, document.LocalRevision, "Blocked"));
        Assert.Equal("Original", (await Store().GetAsync(document.DocumentId))!.Title);
    }

    [Fact]
    public async Task InvalidEditsAndMissingDocumentNeverCreateOrDamageFiles()
    {
        var store = Store();
        await Assert.ThrowsAsync<ArgumentException>(() => store.CreateAsync("  "));
        LocalDocument document = await store.CreateAsync("Original");
        await Assert.ThrowsAsync<JsonException>(() => store.SaveAsync(document with { Sections = [document.Sections[0], document.Sections[0]] }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveAsync(document with { ServerDocumentId = Guid.NewGuid() }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveAsync(document with { DeletedAtUtc = _clock.GetUtcNow() }));
        await Assert.ThrowsAsync<FileNotFoundException>(() => store.SaveAsync(document with { DocumentId = Guid.NewGuid() }));
        await Assert.ThrowsAsync<FileNotFoundException>(() => store.DuplicateAsync(Guid.NewGuid()));
        Assert.Null(await store.GetAsync(Guid.NewGuid()));
        Assert.Equal(1, (await store.GetAsync(document.DocumentId))!.LocalRevision);
    }

    [Fact]
    public async Task ClockRollbackDoesNotMoveModifiedTimeBackwards()
    {
        LocalDocument document = await Store().CreateAsync("Original");
        _clock.Now = _clock.Now.AddDays(-1);
        document = await Store().RenameAsync(document.DocumentId, document.LocalRevision, "Renamed");
        Assert.Equal(document.CreatedAtUtc, document.UpdatedAtUtc);
    }

    [Fact]
    public async Task DeviceRegistrationProvidesRepositoryWithoutPlatformDependencies()
    {
        var services = new ServiceCollection();
        services.AddWriterAppDeviceCore(new DeviceHostOptions("Test", DeviceHostOptions.ProductionApiBaseAddress), _root);
        using ServiceProvider provider = services.BuildServiceProvider();
        LocalDocumentRepository repository = provider.GetRequiredService<LocalDocumentRepository>();
        LocalDocument document = await repository.CreateAsync("Offline");
        Assert.Equal(document.DocumentId, Assert.Single((await repository.ListAsync()).Documents).DocumentId);
    }

    [Fact]
    public async Task UnavailableFileDoesNotPreventOtherDocumentsLoading()
    {
        LocalDocument healthy = await Store().CreateAsync("Healthy");
        LocalDocument busy = await Store().CreateAsync("Busy");
        using (var lease = new FileStream(DocumentPath(busy.DocumentId), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            LocalDocumentList result = await Store().ListAsync();
            Assert.Equal(healthy.DocumentId, Assert.Single(result.Documents).DocumentId);
            Assert.Equal(LocalDocumentIssueKind.Unavailable, Assert.Single(result.Issues).Kind);
        }
        Assert.Equal(2, (await Store().ListAsync()).Documents.Count);
    }

    [Theory]
    [InlineData(LocalSyncState.Synced, LocalSyncState.PendingUpload)]
    [InlineData(LocalSyncState.Conflict, LocalSyncState.Conflict)]
    public async Task LocalEditsPreserveSyncIdentityAndMarkPendingWithoutClearingConflicts(LocalSyncState state, LocalSyncState expected)
    {
        LocalDocument document = await Store().CreateAsync("Cloud document");
        document = document with { ServerDocumentId = Guid.NewGuid(), ServerVersion = "v7", SyncState = state,
            LastSyncedAtUtc = _clock.GetUtcNow() };
        await SeedAsync(document);
        _clock.Advance();
        LocalDocument updated = await Store().RenameAsync(document.DocumentId, document.LocalRevision, "Edited offline");
        Assert.Equal(expected, updated.SyncState);
        Assert.Equal(document.ServerVersion, updated.ServerVersion);
        Assert.Equal(document.ServerDocumentId, updated.ServerDocumentId);
        Assert.Equal(document.LastSyncedAtUtc, updated.LastSyncedAtUtc);
        Assert.Equal(2, updated.LocalRevision);
    }

    [Fact]
    public async Task LocalDuplicateRemainsIndependentAfterSourceEditAndDelete()
    {
        LocalDocument source = await Store().CreateAsync("Original");
        source = await Store().SaveAsync(source with { Sections = [source.Sections[0] with
        { Pages = [source.Sections[0].Pages[0] with { Content = "<p>Original content</p>" }] }] });
        LocalDocument copy = await Store().DuplicateAsync(source.DocumentId);
        source = await Store().SaveAsync(source with { Sections = [source.Sections[0] with
        { Pages = [source.Sections[0].Pages[0] with { Content = "<p>Changed content</p>" }] }] });
        source = await Store().MoveToTrashAsync(source.DocumentId, source.LocalRevision);
        await Store().PermanentlyDeleteAsync(source.DocumentId, source.LocalRevision);
        Assert.Equal("<p>Original content</p>", (await Store().GetAsync(copy.DocumentId))!.Sections[0].Pages[0].Content);
    }

    private Task SeedAsync(LocalDocument document) => File.WriteAllBytesAsync(DocumentPath(document.DocumentId), LocalDocumentCodec.Encode(document));

    private async Task<byte[]> SeedLegacyAsync(Guid id, string content)
    {
        Directory.CreateDirectory(_root);
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(new
        {
            documentId = id, title = "Legacy", contentJson = content,
            updatedAtUtc = _clock.GetUtcNow(), serverVersion = "legacy-version"
        });
        await File.WriteAllBytesAsync(DocumentPath(id), bytes);
        return bytes;
    }

    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.Parse("2026-09-27T10:00:00Z");
        public override DateTimeOffset GetUtcNow() => Now;
        public void Advance() => Now = Now.AddMinutes(1);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
