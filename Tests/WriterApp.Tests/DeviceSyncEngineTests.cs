using System.Threading.Channels;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using WriterApp.Device.Shared.Components;
using WriterApp.Device.Shared.Pages;
using WriterApp.Device.Shared.Services;
using WriterApp.Device.Shared.Storage;
using WriterApp.Shared.Sync;
using Xunit;

namespace WriterApp.Tests;

public sealed partial class DeviceSyncEngineTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "WriterApp.SyncEngineTests", Guid.NewGuid().ToString("N"));
    private readonly FakeApi _api = new();
    private readonly DeviceConnectivity _network = new();
    private readonly List<DeviceSyncEngine> _engines = [];
    private FileLocalDocumentStore Store => new(_root);
    private DeviceSyncEngine Engine(TimeProvider? time = null, LocalDocumentRepository? repo = null, string backend = "https://test.invalid/", DeviceAccountService? account = null)
    {
        var store = Store;
        var engine = new DeviceSyncEngine(store, new(Path.Combine(_root, "sync")), _api,
            account ?? new(new UnconfiguredDeviceIdentityClient()), _network, repo ?? new(store), new("Test", new Uri(backend)), time);
        _engines.Add(engine); return engine;
    }
    private async Task<LocalDocument> EditAsync(Guid id, string text)
    {
        var doc = (await Store.GetAsync(id))!;
        return await Store.SaveAsync(doc with { Sections = doc.Sections.Select(s => s with
            { Pages = s.Pages.Select(p => p with { Content = text }).ToArray() }).ToArray() });
    }
    private static string Text(LocalDocument doc) => doc.Sections[0].Pages[0].Content;

    [Fact]
    public async Task LocalProjectsUploadThroughProjectAwareSync()
    {
        var doc = await Store.CreateProjectAsync("Local project");
        var engine = Engine();
        await engine.EnableAsync(doc.DocumentId);
        Assert.Equal("Synchronization complete.", engine.Message);
        Assert.NotNull(Assert.Single(_api.Requests).Document!.Project);
        Assert.NotNull((await Store.GetAsync(doc.DocumentId))!.ServerDocumentId);
        Assert.Equal(doc.Project!.ProjectId, (await Store.GetAsync(doc.DocumentId))!.Project!.ProjectId);
    }

    [Fact]
    public async Task StructureSyncAndRestoreSurviveRestartWithStableIdentities()
    {
        var repository = new LocalDocumentRepository(Store);
        var doc = await repository.CreateAsync("Structure sync");
        var engine = Engine(repo: repository);
        await engine.EnableAsync(doc.DocumentId);
        doc = (await Store.GetAsync(doc.DocumentId))!;
        Guid firstSection = doc.Sections[0].SectionId, page = doc.Sections[0].Pages[0].PageId;
        doc = await repository.ChangeStructureAsync(doc, new(LocalStructureAction.CreatePage, TargetSectionId: firstSection, Title: "Keep"));
        doc = await repository.ChangeStructureAsync(doc, new(LocalStructureAction.CreateSection, Title: "Target"));
        Guid target = doc.Sections[1].SectionId;
        doc = await repository.ChangeStructureAsync(doc, new(LocalStructureAction.MovePage, page, target));
        await engine.SyncAsync();
        Assert.Equal(page, _api.Documents.Values.Single().Document!.Sections[1].Pages[1].Id);
        doc = (await Store.GetAsync(doc.DocumentId))!;
        doc = await repository.ChangeStructureAsync(doc, new(LocalStructureAction.DeletePage, page));
        engine.Dispose();
        engine = Engine();
        await engine.SyncAsync();
        Assert.Contains(page, _api.Requests.Last(r => r.Document is not null).Document!.Structure!.RemovedPageIds);
        doc = (await Store.GetAsync(doc.DocumentId))!;
        Assert.Equal(page, Assert.Single(doc.DeletedPages).Page.PageId);
        doc = await repository.ChangeStructureAsync(doc, new(LocalStructureAction.RestorePage, page, firstSection));
        await engine.SyncAsync();
        Assert.Equal(page, _api.Documents.Values.Single().Document!.Sections[0].Pages[1].Id);
        Assert.Empty((await Store.GetAsync(doc.DocumentId))!.DeletedPages);
    }

    [Theory]
    [InlineData(LocalStructureAction.RenamePage)]
    [InlineData(LocalStructureAction.MovePage)]
    [InlineData(LocalStructureAction.DeletePage)]
    public async Task ConcurrentRemoteEditAndLocalStructureProduceDurableConflictCopies(LocalStructureAction action)
    {
        var repository = new LocalDocumentRepository(Store);
        var doc = await repository.CreateAsync("Concurrent structure");
        Guid source = doc.Sections[0].SectionId, page = doc.Sections[0].Pages[0].PageId;
        doc = await repository.ChangeStructureAsync(doc, new(LocalStructureAction.CreatePage, TargetSectionId: source, Title: "Keep"));
        doc = await repository.ChangeStructureAsync(doc, new(LocalStructureAction.CreateSection, Title: "Target"));
        Guid target = doc.Sections[1].SectionId;
        var engine = Engine(repo: repository);
        await engine.EnableAsync(doc.DocumentId);
        doc = (await Store.GetAsync(doc.DocumentId))!;
        doc = await repository.ChangeStructureAsync(doc, new(action, page, target, "Local title"));
        _api.EditRemote("<p>Remote author’s edit</p>");
        await engine.SyncAsync();
        Assert.True(engine.Status(doc.DocumentId)!.Conflict);
        engine.Dispose(); engine = Engine(); await engine.SyncAsync();
        Assert.True(engine.Status(doc.DocumentId)!.Conflict);
        var all = (await Store.ListAsync()).Documents;
        Assert.Contains(all, d => d.Sections.SelectMany(s => s.Pages).Any(p => p.Content == "<p>Remote author’s edit</p>"));
        var retained = all.Single(d => d.DocumentId == doc.DocumentId);
        if (action == LocalStructureAction.DeletePage) Assert.Equal(page, Assert.Single(retained.DeletedPages).Page.PageId);
        if (action == LocalStructureAction.RenamePage) Assert.Equal("Local title", retained.Sections.SelectMany(s => s.Pages).Single(p => p.PageId == page).Title);
        if (action == LocalStructureAction.MovePage) Assert.Contains(retained.Sections.Single(s => s.SectionId == target).Pages, p => p.PageId == page);
    }

    [Theory]
    [MemberData(nameof(DeviceContentCompatibilityTests.Supported), MemberType = typeof(DeviceContentCompatibilityTests))]
    [MemberData(nameof(DeviceContentCompatibilityTests.Unsupported), MemberType = typeof(DeviceContentCompatibilityTests))]
    public async Task RemoteContentSurvivesDeviceOpenAndRoundTripWithoutDestructiveUpload(string source)
    {
        var engine = Engine();
        var original = await Store.CreateAsync("Compatibility fixture");
        await engine.EnableAsync(original.DocumentId);
        _api.EditRemote(source);
        await engine.SyncAsync();
        var downloaded = (await Store.GetAsync(original.DocumentId))!;
        Assert.Equal(source, Text(downloaded));
        int uploads = _api.Requests.Count;
        var session = new LocalEditorSession(new(Store), downloaded);
        await session.SaveAsync();
        await engine.SyncAsync();
        Assert.Equal(uploads, _api.Requests.Count);
        Assert.Equal(source, _api.Documents.Values.Single().Document!.Sections[0].Pages[0].Content);
        if (DeviceContentCompatibility.CanEdit(source, LocalContentFormat.Html))
        {
            string edited = source + "<p>Device edit — 日本語</p>";
            session.Edit(downloaded.Sections[0].Pages[0].PageId, edited);
            await session.SaveAsync();
            await engine.SyncAsync();
            Assert.Equal(edited, _api.Documents.Values.Single().Document!.Sections[0].Pages[0].Content);
        }
        else
            Assert.Throws<InvalidDataException>(() => session.Edit(downloaded.Sections[0].Pages[0].PageId, "<p>Lost original</p>"));
    }

    [Fact]
    public async Task SignInLocalSaveAndReconnectionTriggerDebouncedSynchronization()
    {
        var clock = new StepTime(); var account = new DeviceAccountService(new Identity());
        var repo = new LocalDocumentRepository(Store); var engine = Engine(clock, repo, account: account);
        engine.Start();
        Task Finished()
        {
            var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            void Changed() { if (!engine.IsRunning && engine.Message == "Synchronization complete.") { engine.Changed -= Changed; source.TrySetResult(); } }
            engine.Changed += Changed;
            return source.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        var finished = Finished(); await account.SignInAsync(); await clock.TickAsync(); await finished;
        var doc = await Store.CreateAsync("Automatic"); await engine.EnableAsync(doc.DocumentId);
        doc = (await Store.GetAsync(doc.DocumentId))!;
        finished = Finished(); await repo.RenameAsync(doc, "Renamed automatically"); await clock.TickAsync(); await finished;
        Assert.Equal("Renamed automatically", _api.Documents.Values.Single().Document!.Title);
        _network.SetOnline(false); await EditAsync(doc.DocumentId, "<p>Reconnect me</p>");
        finished = Finished(); _network.SetOnline(true); await clock.TickAsync(); await finished;
        Assert.Equal("<p>Reconnect me</p>", _api.Documents.Values.Single().Document!.Sections[0].Pages[0].Content);
    }

    [Fact]
    public async Task TwoDevicesDiscoverUpdatesAndPermanentDeletionWithoutReuploading()
    {
        var first = Engine(); var doc = await Store.CreateAsync("Shared"); await first.EnableAsync(doc.DocumentId);
        string peerRoot = Path.Combine(_root, "peer"); var peerStore = new FileLocalDocumentStore(peerRoot);
        using var peer = new DeviceSyncEngine(peerStore, new(Path.Combine(peerRoot, "sync")), _api,
            new(new UnconfiguredDeviceIdentityClient()), new(), new(peerStore), new("Peer", new Uri("https://test.invalid/")));
        await peer.SyncAsync(); var peerDoc = (await peerStore.ListAsync()).Documents.Single();
        Assert.NotEqual(doc.DocumentId, peerDoc.DocumentId);
        await EditAsync(doc.DocumentId, "<p>First device</p>"); await first.SyncAsync(); await peer.SyncAsync();
        Assert.Equal("<p>First device</p>", Text((await peerStore.GetAsync(peerDoc.DocumentId))!));
        doc = (await Store.GetAsync(doc.DocumentId))!; await Store.MoveToTrashAsync(doc.DocumentId, doc.LocalRevision);
        await first.DeleteAsync(doc.DocumentId); await peer.SyncAsync();
        Assert.True(peer.Status(peerDoc.DocumentId)!.Deleted);
        int requests = _api.Requests.Count; await peer.SyncAsync(); Assert.Equal(requests, _api.Requests.Count);
        Assert.Equal("<p>First device</p>", Text((await peerStore.GetAsync(peerDoc.DocumentId))!));
    }

    [Fact]
    public async Task ConfirmedCloudDeletionCanQueueOfflineForVerifiedAccount()
    {
        var engine = Engine(); var doc = await Store.CreateAsync("Draft"); await engine.EnableAsync(doc.DocumentId);
        doc = (await Store.GetAsync(doc.DocumentId))!; await Store.MoveToTrashAsync(doc.DocumentId, doc.LocalRevision);
        _network.SetOnline(false); await engine.DeleteAsync(doc.DocumentId);
        Assert.Single(_api.Requests);
        _network.SetOnline(true); await Engine().SyncAsync(); Assert.True(_api.Documents.Values.Single().State.IsDeleted);
    }

    [Fact]
    public async Task OfflineChangesSurviveRestartAndUploadOnlyAfterExplicitEnrollment()
    {
        var doc = await Store.CreateAsync("Draft");
        var engine = Engine(); await engine.SyncAsync(); Assert.Empty(_api.Requests);
        await engine.EnableAsync(doc.DocumentId); Assert.Single(_api.Documents);
        _network.SetOnline(false);
        await EditAsync(doc.DocumentId, "<p>Offline writing</p>");
        await engine.SyncAsync(); Assert.Single(_api.Requests);
        var restarted = Engine(); _network.SetOnline(true); await restarted.SyncAsync();
        Assert.Equal("<p>Offline writing</p>", _api.Documents.Values.Single().Document!.Sections[0].Pages[0].Content);
        Assert.Equal(LocalSyncState.Synced, (await Store.GetAsync(doc.DocumentId))!.SyncState);
    }

    [Fact]
    public async Task LostAcknowledgmentReplaysSameOperationAfterRestart()
    {
        var doc = await Store.CreateAsync("Draft"); var time = new StepTime();
        _api.LoseAcknowledgments = true;
        var task = Engine(time).EnableAsync(doc.DocumentId);
        await time.TickAsync(); await time.TickAsync(); await task;
        Assert.Equal(3, _api.Requests.Count);
        Assert.Single(_api.Requests.Select(x => x.OperationId).Distinct());
        Assert.Single(_api.Documents);
        _api.LoseAcknowledgments = false;
        await Engine().SyncAsync();
        Assert.Equal(4, _api.Requests.Count);
        Assert.Single(_api.Requests.Select(x => x.OperationId).Distinct());
        Assert.Equal(LocalSyncState.Synced, (await Store.GetAsync(doc.DocumentId))!.SyncState);
    }

    [Fact]
    public async Task EditsDuringUploadBecomeANewOperationWithoutLosingTheOriginalReceipt()
    {
        var doc = await Store.CreateAsync("Draft");
        _api.BeforeResponse = async () => { _api.BeforeResponse = null; await EditAsync(doc.DocumentId, "<p>Newer edit</p>"); };
        await Engine().EnableAsync(doc.DocumentId);
        Assert.Equal(2, _api.Requests.Count);
        Assert.Equal(2, _api.Requests.Select(x => x.OperationId).Distinct().Count());
        Assert.Equal("<p>Newer edit</p>", Text((await Store.GetAsync(doc.DocumentId))!));
        Assert.Equal("<p>Newer edit</p>", _api.Documents.Values.Single().Document!.Sections[0].Pages[0].Content);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ConcurrentEditsCreateDurableCopiesAndResolveWithoutLosingEitherVersion(bool keepLocal)
    {
        var doc = await Store.CreateAsync("Draft"); var engine = Engine(); await engine.EnableAsync(doc.DocumentId);
        await EditAsync(doc.DocumentId, "<p>My local edit</p>");
        _api.EditRemote("<p>Other device</p>");
        await engine.SyncAsync();
        Assert.Single(engine.Conflicts);
        Assert.Equal("<p>My local edit</p>", Text((await Store.GetAsync(doc.DocumentId))!));
        var restarted = Engine(); await restarted.SyncAsync(); Assert.Single(restarted.Conflicts);
        Assert.Equal(2, (await Store.ListAsync()).Documents.Count);
        await restarted.ResolveAsync(doc.DocumentId, keepLocal);
        Assert.Empty(restarted.Conflicts);
        var documents = (await Store.ListAsync()).Documents;
        Assert.Contains(documents, d => d.Title.EndsWith("(local conflict copy)") && Text(d) == "<p>My local edit</p>");
        Assert.Contains(documents, d => d.Title.EndsWith("(cloud conflict copy)") && Text(d) == "<p>Other device</p>");
        Assert.Equal(keepLocal ? "<p>My local edit</p>" : "<p>Other device</p>", Text((await Store.GetAsync(doc.DocumentId))!));
    }

    [Fact]
    public async Task DeletedCloudDocumentNeverResurrectsAndOfflineWritingIsRetained()
    {
        var doc = await Store.CreateAsync("Draft"); var engine = Engine(); await engine.EnableAsync(doc.DocumentId);
        await EditAsync(doc.DocumentId, "<p>Keep me</p>"); _api.DeleteRemote();
        await engine.SyncAsync(); Assert.True(engine.Conflicts.Single().Deleted);
        await engine.ResolveAsync(doc.DocumentId, true);
        await EditAsync(doc.DocumentId, "<p>Still local</p>"); int requests = _api.Requests.Count;
        await Engine().SyncAsync(); Assert.Equal(requests, _api.Requests.Count);
        Assert.True(_api.Documents.Values.Single().State.IsDeleted);
        Assert.Contains((await Store.ListAsync()).Documents, d => Text(d) == "<p>Keep me</p>" && d.ServerDocumentId is null);
    }

    [Fact]
    public async Task TrashRestoreAndPermanentDeletionFollowVersionedProtocol()
    {
        var doc = await Store.CreateAsync("Draft"); var engine = Engine(); await engine.EnableAsync(doc.DocumentId);
        doc = (await Store.GetAsync(doc.DocumentId))!; await Store.MoveToTrashAsync(doc.DocumentId, doc.LocalRevision);
        await engine.SyncAsync(); Assert.True(_api.Documents.Values.Single().State.IsTrashed);
        doc = (await Store.GetAsync(doc.DocumentId))!; await Store.RestoreAsync(doc.DocumentId, doc.LocalRevision);
        await engine.SyncAsync(); Assert.False(_api.Documents.Values.Single().State.IsTrashed);
        doc = (await Store.GetAsync(doc.DocumentId))!; await Store.MoveToTrashAsync(doc.DocumentId, doc.LocalRevision);
        await engine.DeleteAsync(doc.DocumentId);
        Assert.True(_api.Documents.Values.Single().State.IsDeleted);
        Assert.NotNull(await Store.GetAsync(doc.DocumentId));
    }

    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    public async Task AuthenticationOrEntitlementFailureStopsWithoutLosingQueuedWriting(int status)
    {
        var doc = await Store.CreateAsync("Draft"); var engine = Engine(); await engine.EnableAsync(doc.DocumentId);
        await EditAsync(doc.DocumentId, "<p>Saved offline</p>");
        _api.FailStatus = status; await engine.SyncAsync();
        Assert.Equal(2, _api.Requests.Count);
        Assert.Equal("<p>Saved offline</p>", Text((await Store.GetAsync(doc.DocumentId))!));
        _api.FailStatus = null; await Engine().SyncAsync();
        Assert.Equal(_api.Requests[1].OperationId, _api.Requests[2].OperationId);
    }

    [Fact]
    public async Task ValidationFailureIsNotRetriedUntilUserFixesIt()
    {
        var doc = await Store.CreateAsync("Draft"); var engine = Engine(); _api.FailStatus = 422;
        await engine.EnableAsync(doc.DocumentId); await engine.SyncAsync(); Assert.Single(_api.Requests);
        Assert.NotNull(engine.Status(doc.DocumentId)!.Error);
        await EditAsync(doc.DocumentId, "<p>Fixed</p>"); _api.FailStatus = null;
        await engine.RetryAsync(doc.DocumentId);
        Assert.Equal(2, _api.Requests.Count);
        Assert.NotEqual(_api.Requests[0].OperationId, _api.Requests[1].OperationId);
    }

    [Fact]
    public async Task AccountAndBackendChangesCannotUploadPreviouslyLinkedLocalWriting()
    {
        var doc = await Store.CreateAsync("Private"); await Engine().EnableAsync(doc.DocumentId);
        _api.Owner = "other"; _api.Documents.Clear();
        var other = Engine(); await other.EnableAsync(doc.DocumentId); Assert.Single(_api.Requests);
        Assert.Contains("linked", other.Message);
        var backend = Engine(backend: "https://elsewhere.invalid/"); await backend.SyncAsync(); Assert.Single(_api.Requests);
    }

    [Fact]
    public async Task OpenEditorReceivesConflictCopyInsteadOfBackgroundContentReplacement()
    {
        var doc = await Store.CreateAsync("Draft"); var repo = new LocalDocumentRepository(Store); var engine = Engine(repo: repo);
        await engine.EnableAsync(doc.DocumentId);
        repo.BeginEditing(doc.DocumentId); _api.EditRemote("<p>Incoming</p>");
        await engine.SyncAsync(); Assert.Single(engine.Conflicts);
        Assert.Equal("", Text((await Store.GetAsync(doc.DocumentId))!));
    }

    [Fact]
    public async Task EditorCanSaveAfterMetadataOnlyAcknowledgment()
    {
        var store = Store; var doc = await store.CreateAsync("Draft"); var repo = new LocalDocumentRepository(store);
        var session = new LocalEditorSession(repo, doc); session.Edit(doc.Sections[0].Pages[0].PageId, "<p>Typing</p>");
        await Engine(repo: repo).EnableAsync(doc.DocumentId);
        await session.SaveAsync(); Assert.False(session.IsDirty);
        Assert.Equal("<p>Typing</p>", Text((await Store.GetAsync(doc.DocumentId))!));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OpenEditorRefreshesCloudLinkWithoutLosingPendingTyping(bool typing)
    {
        var doc = await Store.CreateProjectAsync("Draft");
        var repo = new LocalDocumentRepository(Store);
        var session = new LocalEditorSession(repo, doc);
        var page = doc.Sections[0].Pages[0];
        if (typing) session.Edit(page.PageId, "<p>Still typing</p>");
        session.RememberPage(page.PageId);
        var saveState = session.SaveState;
        await Engine(repo: repo).EnableAsync(doc.DocumentId);

        Assert.True(await session.RefreshSyncMetadataAsync());
        Assert.NotNull(session.Document.ServerDocumentId);
        Assert.Equal(LocalSyncState.Synced, session.Document.SyncState);
        Assert.Equal(saveState, session.SaveState);
        Assert.Equal(typing ? "<p>Still typing</p>" : "", session.ContentFor(page));
        Assert.Equal(page.PageId, session.Snapshot().Project!.LastPageId);
        Assert.False(await session.RefreshSyncMetadataAsync());
        await session.SaveAsync();
        Assert.False(session.IsDirty);
        Assert.Equal(typing ? "<p>Still typing</p>" : "", Text((await Store.GetAsync(doc.DocumentId))!));
    }

    [Theory]
    [InlineData("scene", "Suggest scene details")]
    [InlineData("consistency", "Check consistency")]
    [InlineData("quality", "Analyze style &amp; quality")]
    [InlineData("synopsis", "Improve synopsis field")]
    public async Task OpenCoachEnablesAfterCloudSyncWithoutChangingPanel(string mode, string label)
    {
        var doc = await Store.CreateProjectAsync("Draft");
        using var services = RenderServices();
        var engine = services.GetRequiredService<DeviceSyncEngine>();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        var output = await renderer.Dispatcher.InvokeAsync(() => renderer.RenderComponentAsync<LocalAiPanel>(
            ParameterView.FromDictionary(new Dictionary<string, object?> { ["DocumentId"] = doc.DocumentId, ["Mode"] = mode })));
        string before = await renderer.Dispatcher.InvokeAsync(output.ToHtmlString);
        Assert.Contains("Enable cloud sync explicitly", before);
        Assert.True(ButtonDisabled(before, label));

        await renderer.Dispatcher.InvokeAsync(() => engine.EnableAsync(doc.DocumentId));
        Assert.Equal("Synchronization complete.", engine.Message);
        string after = await WaitForHtmlAsync(renderer, output.ToHtmlString, html => !html.Contains("Enable cloud sync explicitly"));
        Assert.DoesNotContain("Enable cloud sync explicitly", after);
        Assert.False(ButtonDisabled(after, label));
    }

    [Fact]
    public async Task OpenWritingToolsEnableAfterCloudSyncWithoutReopeningDocument()
    {
        var doc = await Store.CreateProjectAsync("Draft");
        using var services = RenderServices();
        await services.GetRequiredService<DeviceAccountService>().SignInAsync();
        var engine = services.GetRequiredService<DeviceSyncEngine>();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        var output = await renderer.Dispatcher.InvokeAsync(() => renderer.RenderComponentAsync<DocumentWorkspace>(
            ParameterView.FromDictionary(new Dictionary<string, object?> { ["DocumentId"] = doc.DocumentId })));
        string before = await renderer.Dispatcher.InvokeAsync(output.ToHtmlString);
        Assert.Contains("Sync this document to use AI writing tools.", before);
        var markup = new AngleSharp.Html.Parser.HtmlParser().ParseDocument(before);
        var statusBar = markup.QuerySelector(".editor-status-bar");
        Assert.NotNull(statusBar);
        Assert.Single(statusBar.QuerySelectorAll("button"), b => b.TextContent == "Enable cloud sync");
        Assert.DoesNotContain(markup.QuerySelectorAll(".context-drawer button"), b => b.TextContent.Contains("Enable cloud sync"));
        string[] labels = ["Preview rewrite", "Expand", "Shorten", "Change tone", "Show, don&#x27;t tell", "Summarize section", "Preview custom action", "Translate selection"];
        foreach (string label in labels) Assert.True(ButtonDisabled(before, label));

        await renderer.Dispatcher.InvokeAsync(() => engine.EnableAsync(doc.DocumentId));
        Assert.Equal("Synchronization complete.", engine.Message);
        string after = await WaitForHtmlAsync(renderer, output.ToHtmlString,
            html => labels.All(label => !ButtonDisabled(html, label)));
        Assert.DoesNotContain("Sync this document to use AI writing tools.",
            new AngleSharp.Html.Parser.HtmlParser().ParseDocument(after).QuerySelector(".writing-panel")!.TextContent);
        Assert.DoesNotContain("Enable cloud sync</button>", after);
        Assert.Contains("Cloud linked", new AngleSharp.Html.Parser.HtmlParser().ParseDocument(after).QuerySelector(".editor-status-bar")!.TextContent);
        foreach (string label in labels) Assert.False(ButtonDisabled(after, label), "Action stayed disabled after sync: " + label);
    }

    [Fact]
    public async Task UnreachableLocalBackendReportsCauseAndEnrollmentCanBeRetried()
    {
        var document = await Store.CreateAsync("Keep writing");
        var repository = new LocalDocumentRepository(Store);
        var engine = Engine(repo: repository, backend: "http://localhost:5387/");
        _api.OwnerFailure = new HttpRequestException("Internal transport details must not be displayed.");

        await engine.EnableAsync(document.DocumentId);
        Assert.False(engine.IsRunning);
        Assert.Contains("Cannot reach the configured backend at http://localhost:5387", engine.LastError);
        Assert.Contains("Start the local API", engine.Message);
        Assert.DoesNotContain("Internal transport details", engine.Message);
        Assert.Null((await Store.GetAsync(document.DocumentId))!.ServerDocumentId);
        Assert.Empty(_api.Requests);

        _api.OwnerFailure = null;
        await engine.EnableAsync(document.DocumentId);
        Assert.Null(engine.LastError);
        Assert.Equal("Synchronization complete.", engine.Message);
        Assert.NotNull((await Store.GetAsync(document.DocumentId))!.ServerDocumentId);
        Assert.Single(_api.Requests);
    }

    [Fact]
    public async Task CompactCloudSyncShowsFailureAndRetryInStatusBar()
    {
        var doc = await Store.CreateProjectAsync("Draft");
        using var services = RenderServices();
        var engine = services.GetRequiredService<DeviceSyncEngine>();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        var output = await renderer.Dispatcher.InvokeAsync(() => renderer.RenderComponentAsync<DocumentSyncStatus>(
            ParameterView.FromDictionary(new Dictionary<string, object?> { ["Document"] = doc, ["Compact"] = true })));
        _api.OwnerFailure = new HttpRequestException("Hidden transport detail");
        await renderer.Dispatcher.InvokeAsync(() => engine.EnableAsync(doc.DocumentId));

        string html = await WaitForHtmlAsync(renderer, output.ToHtmlString, text => text.Contains("Sync failed"));
        Assert.Contains("Cannot reach the configured backend", html);
        Assert.Contains("role=\"alert\"", html);
        Assert.False(ButtonDisabled(html, "Enable cloud sync"));
        Assert.False(ButtonDisabled(html, "Retry cloud sync"));
    }

    private ServiceProvider RenderServices() => new ServiceCollection()
        .AddLogging()
        .AddWriterAppDeviceCore(new("Test", new Uri("https://test.invalid/")), _root)
        .AddSingleton<IDeviceIdentityClient, Identity>()
        .AddSingleton<IDeviceAiApi, LocalWritingTests.Api>()
        .AddSingleton<IDeviceSyncApi>(_api)
        .AddSingleton<IJSRuntime, NoJs>()
        .AddSingleton<NavigationManager, TestNavigation>()
        .BuildServiceProvider();

    private static bool ButtonDisabled(string html, string label)
    {
        var match = Regex.Match(html, "<button(?<attributes>[^>]*)>" + Regex.Escape(label) + "</button>");
        Assert.True(match.Success, "Missing button: " + label);
        return match.Groups["attributes"].Value.Contains("disabled");
    }
    private static async Task<string> WaitForHtmlAsync(HtmlRenderer renderer, Func<string> read, Func<string, bool> ready)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (true)
        {
            string html = await renderer.Dispatcher.InvokeAsync(read);
            if (ready(html)) return html;
            await Task.Delay(10, timeout.Token);
        }
    }
    private sealed class NoJs : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => throw new InvalidOperationException("Static render must not invoke JS.");
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken ct, object?[]? args) => InvokeAsync<TValue>(identifier, args);
    }
    private sealed class TestNavigation : NavigationManager
    {
        public TestNavigation() => Initialize("https://test.invalid/", "https://test.invalid/");
        protected override void NavigateToCore(string uri, bool forceLoad) => throw new InvalidOperationException("Unexpected navigation.");
    }

    [Fact]
    public async Task CancellationRetainsImmutableOperationForRestart()
    {
        var doc = await Store.CreateAsync("Draft"); using var cancel = new CancellationTokenSource();
        _api.BeforeResponse = () => { cancel.Cancel(); return Task.CompletedTask; };
        await Engine().EnableAsync(doc.DocumentId, cancel.Token);
        _api.BeforeResponse = null; await Engine().SyncAsync();
        Assert.Equal(2, _api.Requests.Count); Assert.Single(_api.Requests.Select(x => x.OperationId).Distinct());
    }

    [Fact]
    public async Task CorruptJournalStopsSafelyWithoutUploadingOrOverwritingIt()
    {
        var doc = await Store.CreateAsync("Draft"); await Engine().EnableAsync(doc.DocumentId);
        string path = Directory.GetFiles(Path.Combine(_root, "sync"), "*.json").Single();
        await File.WriteAllTextAsync(path, "broken"); await EditAsync(doc.DocumentId, "<p>Retained</p>");
        await Engine().SyncAsync(); Assert.Single(_api.Requests); Assert.Equal("broken", await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task RemoteDownloadPreservesIdentityAcrossRestartAndIncrementalUpdates()
    {
        var source = LocalDocumentCodec.NewDocument(Guid.NewGuid(), "Remote", DateTimeOffset.UtcNow);
        await _api.MutateAsync(Guid.NewGuid(), new(Guid.NewGuid(), null, "upload", DeviceSyncMapping.Upload(source)), default);
        await Engine().SyncAsync(); var doc = (await Store.ListAsync()).Documents.Single();
        _api.EditRemote("<p>New remote</p>"); await Engine().SyncAsync();
        var downloaded = (await Store.ListAsync()).Documents.Single(); Assert.Equal(doc.DocumentId, downloaded.DocumentId);
        Assert.Equal("<p>New remote</p>", Text(downloaded));
    }

    private sealed class StepTime : TimeProvider
    {
        private readonly Channel<Action> _ticks = Channel.CreateUnbounded<Action>();
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        { var timer = new TimerStub(() => callback(state)); _ticks.Writer.TryWrite(timer.Fire); return timer; }
        public async Task TickAsync() => (await _ticks.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10)))();
        private sealed class TimerStub(Action action) : ITimer
        {
            private bool _disposed;
            public void Fire() { if (!_disposed) action(); }
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;
            public void Dispose() => _disposed = true;
            public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
        }
    }
    private sealed class Identity : IDeviceIdentityClient
    {
        public bool IsConfigured => true;
        public Task<DeviceAccessToken?> AcquireAsync(bool interactive, CancellationToken cancellationToken) =>
            Task.FromResult<DeviceAccessToken?>(new("test", DateTimeOffset.UtcNow.AddHours(1), "Test"));
        public Task SignOutAsync() => Task.CompletedTask;
    }
    private sealed class FakeApi : IDeviceSyncApi
    {
        public string Owner = "paid";
        public Exception? OwnerFailure;
        public Dictionary<Guid, SyncSnapshot> Documents = [];
        private readonly Dictionary<Guid, SyncMutationResult> _receipts = [];
        public List<SyncMutation> Requests = [];
        public int? FailStatus;
        public bool LoseAcknowledgments;
        public Func<Task>? BeforeResponse;
        private int _version;
        public Task<string> GetOwnerAsync(CancellationToken ct) => OwnerFailure is { } failure ? Task.FromException<string>(failure) : Task.FromResult(Owner);
        public Task<SyncChanges> ChangesAsync(string? cursor, CancellationToken ct) => Task.FromResult(new SyncChanges(Documents.Values.Select(x => x.State).ToArray(), "cursor", false));
        public Task<SyncSnapshot> DownloadAsync(Guid id, CancellationToken ct) => Task.FromResult(Documents[id]);
        public async Task<SyncMutationResult> MutateAsync(Guid id, SyncMutation request, CancellationToken ct)
        {
            Requests.Add(request);
            if (FailStatus is { } status) throw new DeviceSyncApiException(status, "test_rejection", "Rejected");
            if (!_receipts.TryGetValue(request.OperationId, out var result))
            {
                Documents.TryGetValue(id, out var current);
                if (current is not null && (current.State.IsDeleted || current.State.Version != request.ExpectedVersion))
                    throw new DeviceSyncApiException(409, current.State.IsDeleted ? "document_deleted" : "version_conflict", "Conflict");
                bool trashed = request.Action == "trash" || current?.State.IsTrashed == true && request.Action != "restore";
                var state = new SyncChange(id, (++_version).ToString(), request.Action == "delete", trashed);
                var upload = request.Document;
                var document = upload is null ? current?.Document : new SyncDocument(id, upload.Project?.Id ?? current?.Document?.ProjectId ?? Guid.NewGuid(), upload.Title, upload.LanguageCode, "manuscript", false,
                    DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, upload.Sections, upload.Project);
                if (request.Action == "rename") document = document! with { Title = request.Title! };
                Documents[id] = new(state, state.IsDeleted ? null : document);
                result = new(request.OperationId, state); _receipts[request.OperationId] = result;
            }
            if (BeforeResponse is not null) await BeforeResponse();
            if (LoseAcknowledgments) throw new DeviceSyncApiException(503, "lost_ack", "Lost acknowledgment");
            return result;
        }
        public void EditRemote(string content)
        {
            var pair = Documents.Single(); var d = pair.Value.Document!;
            Documents[pair.Key] = new(pair.Value.State with { Version = (++_version).ToString() }, d with
                { Sections = d.Sections.Select(s => s with { Pages = s.Pages.Select(p => p with { Content = content }).ToArray() }).ToArray() });
        }
        public void DeleteRemote()
        { var pair = Documents.Single(); Documents[pair.Key] = new(pair.Value.State with { Version = (++_version).ToString(), IsDeleted = true }, null); }
    }
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync()
    {
        _network.SetOnline(false);
        foreach (var engine in _engines) engine.Dispose();
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(5));
        foreach (var engine in _engines) await engine.SyncAsync(timeout.Token);
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
