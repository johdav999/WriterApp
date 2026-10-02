using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WriterApp.Device.Shared.Components;
using WriterApp.Device.Shared.Services;
using WriterApp.Device.Shared.Storage;
using Xunit;

namespace WriterApp.Tests;

public sealed class LocalDocumentStructureTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "WriterApp.StructureTests", Guid.NewGuid().ToString("N"));
    private FileLocalDocumentStore Store => new(_root);
    private LocalDocumentRepository Repo => new(Store);
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
    private Task<LocalDocument> Change(LocalDocument doc, LocalStructureAction action, Guid item = default, Guid target = default, string? title = null) =>
        Repo.ChangeStructureAsync(doc, new(action, item, target, title));

    [Fact]
    public async Task OfflineLifecycleRetainsContentIdentitiesAndTrashAfterRestart()
    {
        var doc = await Repo.CreateAsync("Offline book");
        var original = doc.Sections[0];
        var page = original.Pages[0];
        doc = await Repo.SaveAsync(doc with { Sections = [original with { Pages = [page with { Content = "<p>Räksmörgås 日本語 — café</p>" }] }] });
        doc = await Change(doc, LocalStructureAction.CreatePage, target: original.SectionId, title: "Second page");
        Guid secondPage = doc.Sections[0].Pages[1].PageId;
        doc = await Change(doc, LocalStructureAction.CreateSection, title: "Second section");
        Guid secondSection = doc.Sections[1].SectionId;
        Guid originalSecondSectionPage = doc.Sections[1].Pages[0].PageId;
        doc = await Change(doc, LocalStructureAction.RenameSection, original.SectionId, title: "First chapter");
        doc = await Change(doc, LocalStructureAction.RenamePage, page.PageId, title: "My writing");
        doc = await Change(doc, LocalStructureAction.PageDown, page.PageId);
        Assert.Equal(secondPage, doc.Sections[0].Pages[0].PageId);
        doc = await Change(doc, LocalStructureAction.SectionUp, secondSection);
        doc = await Change(doc, LocalStructureAction.MovePage, page.PageId, secondSection);
        Assert.Equal(page.PageId, doc.Sections[0].Pages[1].PageId);
        doc = await Change(doc, LocalStructureAction.DeletePage, page.PageId);
        Guid id = doc.DocumentId;
        doc = (await Store.GetAsync(id))!;
        Assert.Equal("<p>Räksmörgås 日本語 — café</p>", Assert.Single(doc.DeletedPages).Page.Content);
        Assert.Equal(originalSecondSectionPage, LocalDocumentStructure.SelectPage(doc, doc, originalSecondSectionPage));
        doc = await Change(doc, LocalStructureAction.RestorePage, page.PageId, original.SectionId);
        var restored = doc.Sections[1].Pages.Single(p => p.PageId == page.PageId);
        Assert.Equal("My writing", restored.Title);
        Assert.Equal(page.CreatedAtUtc, restored.CreatedAtUtc);
        doc = await Change(doc, LocalStructureAction.DeleteSection, original.SectionId);
        doc = (await Store.GetAsync(id))!;
        Assert.Contains(Assert.Single(doc.DeletedSections).Section.Pages, p => p.PageId == page.PageId && p.Content == restored.Content);
        doc = await Change(doc, LocalStructureAction.RestoreSection, original.SectionId);
        doc = (await Store.GetAsync(id))!;
        Assert.Empty(doc.DeletedPages); Assert.Empty(doc.DeletedSections);
        Assert.Equal(original.SectionId, doc.Sections[1].SectionId);
        Assert.Equal(restored.Content, doc.Sections[1].Pages.Single(p => p.PageId == page.PageId).Content);
        Assert.Equal(LocalSyncState.LocalOnly, doc.SyncState);
    }

    [Theory]
    [InlineData(LocalStructureAction.DeleteSection)]
    [InlineData(LocalStructureAction.DeletePage)]
    [InlineData(LocalStructureAction.MovePage)]
    [InlineData(LocalStructureAction.PageUp)]
    [InlineData(LocalStructureAction.SectionDown)]
    public async Task InvalidOrLastItemOperationsLeaveExactFileUnchanged(LocalStructureAction action)
    {
        var doc = await Repo.CreateAsync("Keep writing");
        string path = Path.Combine(_root, $"{doc.DocumentId:N}.json");
        byte[] before = await File.ReadAllBytesAsync(path);
        Guid item = action is LocalStructureAction.DeleteSection or LocalStructureAction.SectionDown ? doc.Sections[0].SectionId : doc.Sections[0].Pages[0].PageId;
        await Assert.ThrowsAsync<InvalidOperationException>(() => Change(doc, action, item, Guid.NewGuid()));
        Assert.Equal(before, await File.ReadAllBytesAsync(path));
    }

    [Fact]
    public async Task FailedCommitDoesNotPublishMutationOrLosePendingWriting()
    {
        var doc = await Repo.CreateAsync("Atomic");
        var failing = new FileLocalDocumentStore(_root, TimeProvider.System, new AtomicDocumentWriter(_ => throw new IOException("Disk full")));
        var repository = new LocalDocumentRepository(failing);
        int notifications = 0; repository.Changed += () => notifications++;
        var session = new LocalEditorSession(repository, doc);
        Guid page = doc.Sections[0].Pages[0].PageId;
        session.Edit(page, "<p>Pending text survives</p>");
        await Assert.ThrowsAsync<IOException>(() => session.SaveAsync());
        Assert.True(session.IsDirty);
        Assert.Equal("<p>Pending text survives</p>", session.ContentFor(doc.Sections[0].Pages[0]));
        await Assert.ThrowsAsync<IOException>(() => repository.ChangeStructureAsync(doc, new(LocalStructureAction.CreateSection, Title: "Unsaved section")));
        Assert.Equal(0, notifications);
        Assert.Single((await Store.GetAsync(doc.DocumentId))!.Sections);
    }

    [Fact]
    public async Task ConcurrentStructureCannotOverwriteAWriterAndDirtySessionCannotAdoptIt()
    {
        var doc = await Repo.CreateAsync("Concurrent");
        var session = new LocalEditorSession(Repo, doc);
        session.Edit(doc.Sections[0].Pages[0].PageId, "<p>Concurrent writer</p>");
        var renamed = await Change(doc, LocalStructureAction.RenamePage, doc.Sections[0].Pages[0].PageId, title: "New title");
        await Assert.ThrowsAsync<LocalDocumentConflictException>(() => session.SaveAsync());
        Assert.True(session.IsDirty);
        Assert.Throws<InvalidOperationException>(() => session.AdoptSavedDocument(renamed));
        await Assert.ThrowsAsync<LocalDocumentConflictException>(() => Change(doc, LocalStructureAction.CreateSection, title: "Stale"));
        Assert.Equal("New title", (await Store.GetAsync(doc.DocumentId))!.Sections[0].Pages[0].Title);
    }

    [Fact]
    public async Task VersionOneMigrationBacksUpExactBytesAndKeepsUnknownMetadata()
    {
        var doc = await Repo.CreateAsync("Migration");
        string path = Path.Combine(_root, $"{doc.DocumentId:N}.json");
        var json = JsonNode.Parse(await File.ReadAllBytesAsync(path))!;
        json["schemaVersion"] = 1;
        json["document"]!["futureDocument"] = JsonNode.Parse("{\"secret\":\"retained\"}");
        json["document"]!["sections"]![0]!["futureSection"] = "keep";
        json["document"]!["sections"]![0]!["pages"]![0]!["futurePage"] = 42;
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(json.ToJsonString());
        await File.WriteAllBytesAsync(path, bytes);
        var failing = new FileLocalDocumentStore(_root, TimeProvider.System, new AtomicDocumentWriter(staging =>
        { if (!staging.Contains(".v1.bak.", StringComparison.Ordinal)) throw new IOException("Interrupted migration"); }));
        await Assert.ThrowsAsync<LocalDocumentReadException>(() => failing.GetAsync(doc.DocumentId));
        Assert.Equal(bytes, await File.ReadAllBytesAsync(path));
        Assert.Equal(bytes, await File.ReadAllBytesAsync(path + ".v1.bak"));
        var migrated = (await Store.GetAsync(doc.DocumentId))!;
        var saved = await Change(migrated, LocalStructureAction.RenameSection, migrated.Sections[0].SectionId, title: "Renamed");
        Assert.Equal("retained", saved.ExtensionData!["futureDocument"].GetProperty("secret").GetString());
        Assert.Equal("keep", saved.Sections[0].ExtensionData!["futureSection"].GetString());
        Assert.Equal(42, saved.Sections[0].Pages[0].ExtensionData!["futurePage"].GetInt32());
        Assert.Equal(bytes, await File.ReadAllBytesAsync(path + ".v1.bak"));
        Assert.Equal(4, JsonNode.Parse(await File.ReadAllBytesAsync(path))!["schemaVersion"]!.GetValue<int>());
    }

    [Fact]
    public async Task SparseOrderAndUnrelatedMetadataAreNotNormalizedAndDuplicateDetachesTrash()
    {
        var doc = await Repo.CreateAsync("Sparse");
        doc = await Change(doc, LocalStructureAction.CreateSection, title: "Second");
        doc = await Change(doc, LocalStructureAction.CreateSection, title: "Untouched");
        doc = await Repo.SaveAsync(doc with { Sections = doc.Sections.Select((s, i) => s with { OrderIndex = (i + 1) * 10 }).ToArray() });
        var untouched = doc.Sections[2];
        doc = await Change(doc, LocalStructureAction.SectionUp, doc.Sections[1].SectionId);
        Assert.Equal(new[] { 10, 20, 30 }, doc.Sections.Select(s => s.OrderIndex));
        Assert.Equal(untouched.UpdatedAtUtc, doc.Sections[2].UpdatedAtUtc);
        doc = await Change(doc, LocalStructureAction.DeleteSection, doc.Sections[0].SectionId);
        var copy = await Repo.DuplicateAsync(doc.DocumentId);
        Assert.NotEqual(doc.DeletedSections[0].Section.SectionId, copy.DeletedSections[0].Section.SectionId);
        Assert.NotEqual(doc.DeletedSections[0].Section.Pages[0].PageId, copy.DeletedSections[0].Section.Pages[0].PageId);
        Assert.Null(copy.DeletedSections[0].Section.ServerSectionId);
        copy = await Change(copy, LocalStructureAction.RestoreSection, copy.DeletedSections[0].Section.SectionId);
        Assert.Equal(3, copy.Sections.Count);
    }

    [Fact]
    public async Task NavigatorRendersKeyboardMovesLastItemGuardsAndAccessibleTrash()
    {
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        var doc = LocalDocumentCodec.NewDocument(Guid.NewGuid(), "UI", Now);
        string html = await renderer.Dispatcher.InvokeAsync(async () => (await renderer.RenderComponentAsync<LocalStructureNavigator>(
            ParameterView.FromDictionary(new Dictionary<string, object?> { ["Document"] = doc }))).ToHtmlString());
        Assert.Contains("New section", html); Assert.Contains("New page", html);
        Assert.Contains("Move Page 1 up", html); Assert.Contains("Manage Section 1", html);
        Assert.Contains("disabled", html);
        Assert.DoesNotContain("disabled=\"Disabled\"", html);
    }

    [Fact]
    public void DownloadedMovesAndRemoteDeletionsPreserveLocalIdsUnknownMetadataAndRecovery()
    {
        var doc = LocalDocumentCodec.NewDocument(Guid.NewGuid(), "Mapping", Now);
        doc = LocalDocumentStructure.Apply(doc, new(LocalStructureAction.CreatePage, TargetSectionId: doc.Sections[0].SectionId, Title: "Keep"), Now);
        doc = LocalDocumentStructure.Apply(doc, new(LocalStructureAction.CreateSection, Title: "Target"), Now);
        Guid pageId = doc.Sections[0].Pages[0].PageId, serverPage = Guid.NewGuid();
        doc = doc with { Sections = doc.Sections.Select(s => s with { ServerSectionId = Guid.NewGuid(), Pages = s.Pages.Select(p => p with {
            ServerPageId = p.PageId == pageId ? serverPage : Guid.NewGuid(), ExtensionData = new() { ["opaque"] = JsonSerializer.SerializeToElement("retained") } }).ToArray() }).ToArray() };
        var upload = DeviceSyncMapping.Upload(doc);
        var first = upload.Sections[0]; var second = upload.Sections[1];
        var moved = first.Pages[0] with { OrderIndex = 1 };
        var state = new WriterApp.Shared.Sync.SyncChange(Guid.NewGuid(), "v2", false, false);
        var snapshot = new WriterApp.Shared.Sync.SyncSnapshot(state, new(state.DocumentId, Guid.NewGuid(), doc.Title, null, "manuscript", false, Now, Now,
            [first with { Pages = [first.Pages[1]] }, second with { Pages = second.Pages.Append(moved).ToArray() }]));
        var downloaded = DeviceSyncMapping.Download(snapshot, doc.DocumentId, doc, Now);
        Assert.Equal(pageId, downloaded.Sections[1].Pages[1].PageId);
        Assert.Equal(serverPage, downloaded.Sections[1].Pages[1].ServerPageId);
        Assert.Equal("retained", downloaded.Sections[1].Pages[1].ExtensionData!["opaque"].GetString());
        snapshot = snapshot with { Document = snapshot.Document! with { Sections = [snapshot.Document.Sections[0], second] } };
        var deleted = DeviceSyncMapping.Download(snapshot, doc.DocumentId, downloaded, Now);
        Assert.Equal(pageId, Assert.Single(deleted.DeletedPages).Page.PageId);
        var restored = LocalDocumentStructure.Apply(deleted, new(LocalStructureAction.RestorePage, pageId, deleted.Sections[0].SectionId), Now);
        Assert.Equal(serverPage, restored.Sections[0].Pages[1].ServerPageId);
        // The additive request capability does not change existing journal/content hashes.
        var expected = upload with { Structure = null };
        string oldHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(expected, DeviceSyncJournal.Json)));
        Assert.Equal(oldHash, DeviceSyncMapping.WritingFingerprint(doc));
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
