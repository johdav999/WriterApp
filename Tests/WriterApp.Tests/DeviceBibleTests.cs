using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AngleSharp.Html.Parser;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WriterApp.Device.Shared.Components;
using WriterApp.Device.Shared.Services;
using WriterApp.Device.Shared.Storage;
using WriterApp.Shared.Canon;
using WriterApp.UI.Shared;
using Xunit;

namespace WriterApp.Tests;

public sealed class DeviceBibleTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "WriterApp.BibleTests", Guid.NewGuid().ToString("N"));
    private readonly Identity _identity = new();
    private readonly Handler _handler = new();
    private readonly DeviceConnectivity _network = new();
    private DeviceAccountService Account => _account ??= new(_identity);
    private DeviceAccountService? _account;
    private FileLocalDocumentStore Documents => new(Path.Combine(_root, "documents"));
    private LocalBibleStore Cache => new(Path.Combine(_root, "canon"));
    private DeviceBibleService Service(string backend = "https://test.invalid/") => new(
        new(new HttpClient(_handler) { BaseAddress = new(backend) }), Cache, new(Documents), Account, _network, new("Test", new(backend)));
    private async Task<LocalDocument> Book()
    {
        await Account.SignInAsync();
        var store = Documents; var doc = await store.CreateProjectAsync("Book");
        doc = await store.SaveAsync(doc with { Sections = doc.Sections.Select(s => s with { Pages = s.Pages.Select(p => p with { Content = "<p>Keep rich <em>writing</em>.</p>" }).ToArray() }).ToArray() });
        doc = await store.ApplySyncAsync(doc with { ServerDocumentId = Guid.NewGuid(), ServerVersion = "v1", SyncState = LocalSyncState.Synced }, doc.LocalRevision, default, projects: true);
        _handler.DocumentId = doc.ServerDocumentId!.Value; return doc;
    }
    private static string Content(CanonKind kind, string name = "Anna") => JsonSerializer.Serialize(
        new Dictionary<string,object> { ["schemaVersion"] = "1.0", [CanonContent.Collection(kind)] = new[] {
            new Dictionary<string,string> { ["id"] = "id-1", [kind == CanonKind.Timeline ? "title" : "name"] = name } } });

    [Theory]
    [InlineData(CanonKind.Character)]
    [InlineData(CanonKind.Place)]
    [InlineData(CanonKind.Timeline)]
    public async Task EveryKindRefreshesReloadsAcrossRestartAndIsAvailableOffline(CanonKind kind)
    {
        var doc = await Book(); var service = Service();
        var result = await service.LoadAsync(doc, kind, true, false, doc.Sections[0].SectionId, default);
        Assert.Equal(new[] { "GET", "POST" }, _handler.Calls);
        Assert.True(_handler.Request!.FullRebuild); Assert.Equal("v1", _handler.Request.ExpectedDocumentVersion);
        Assert.Equal("missing", _handler.Request.ExpectedSnapshotVersion);
        _network.SetOnline(false);
        var cached = await Service().CachedAsync(doc, kind);
        Assert.Equal("Ready", cached!.RefreshState); Assert.Equal(result.Snapshot, cached.Snapshot);
        Assert.Single((await Service().ContextAsync(doc)).Snapshots);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service().LoadAsync(doc, kind, true, false, null, default));
        Assert.Equal(2, _handler.Calls.Count);
        Assert.Equal(LocalDocumentCodec.Encode(doc), LocalDocumentCodec.Encode((await Documents.GetAsync(doc.DocumentId))!));
    }

    [Fact]
    public async Task ConsistencyPreparesMissingReferencesOnceWithoutChangingWriting()
    {
        var doc = await Book(); var service = Service();
        var progress = new List<CanonKind>();
        var context = await service.PrepareConsistencyAsync(doc, doc.Sections[0].SectionId, kind => { progress.Add(kind); return Task.CompletedTask; }, default);
        Assert.Equal(3, context.Snapshots.Count); Assert.Equal(6, _handler.Calls.Count); Assert.Equal(3, progress.Count);
        await Service().PrepareConsistencyAsync(doc, doc.Sections[0].SectionId, null, default);
        Assert.Equal(6, _handler.Calls.Count);
        Assert.Equal(LocalDocumentCodec.Encode(doc), LocalDocumentCodec.Encode((await Documents.GetAsync(doc.DocumentId))!));
    }
    [Theory][InlineData("disconnect", false)][InlineData("stale", true)][InlineData("wrong-document", true)]
    public async Task ConsistencyMayOmitUnavailableReferencesButNeverDowngradesStalenessOrIdentityFailures(string failure, bool reject)
    {
        var doc = await Book(); _handler.Failure = failure;
        var task = Service().PrepareConsistencyAsync(doc, doc.Sections[0].SectionId, null, default);
        if (reject) await Assert.ThrowsAnyAsync<Exception>(() => task);
        else Assert.Empty((await task).Snapshots);
        Assert.Equal(LocalDocumentCodec.Encode(doc), LocalDocumentCodec.Encode((await Documents.GetAsync(doc.DocumentId))!));
    }

    [Theory]
    [InlineData("wrong-document")]
    [InlineData("wrong-kind")]
    [InlineData("stale")]
    [InlineData("invalid-json")]
    [InlineData("partial")]
    [InlineData("invalid-reference")]
    [InlineData("disconnect")]
    [InlineData("cancel")]
    [InlineData("account")]
    [InlineData("local-edit")]
    [InlineData("unconfirmed")]
    public async Task FailedRefreshKeepsPriorCacheAndRetryStartsByReadingRemoteState(string failure)
    {
        var doc = await Book(); var service = Service();
        var original = await service.LoadAsync(doc, CanonKind.Character, true, true, null, default);
        using var cancellation = new CancellationTokenSource();
        _handler.Failure = failure;
        _handler.BeforeReply = async () =>
        {
            if (failure == "cancel") cancellation.Cancel();
            if (failure == "account") await Account.SignOutAsync();
            if (failure == "local-edit")
                await Documents.SaveAsync(doc with { Title = "Concurrent authored title" });
        };
        await Assert.ThrowsAnyAsync<Exception>(() => service.LoadAsync(doc, CanonKind.Character, true, true, null, cancellation.Token));
        _handler.BeforeReply = null; _handler.Failure = null;
        if (failure == "account") await Account.SignInAsync();
        var cached = await service.CachedAsync(doc, CanonKind.Character);
        Assert.Equal(original.Snapshot, cached!.Snapshot); Assert.Equal("Interrupted", cached.RefreshState);
        Assert.Empty((await service.ContextAsync(doc)).Snapshots);
        if (failure != "local-edit")
        {
            _handler.Calls.Clear();
            var retry = await service.LoadAsync(doc, CanonKind.Character, true, false, null, default);
            Assert.Equal(new[] { "GET", "POST" }, _handler.Calls);
            Assert.False(_handler.Request!.FullRebuild); Assert.Equal("ready-token", _handler.Request.ExpectedSnapshotVersion);
            Assert.Equal("Ready", retry.RefreshState);
            Assert.Equal(LocalDocumentCodec.Encode(doc), LocalDocumentCodec.Encode((await Documents.GetAsync(doc.DocumentId))!));
        }
        else Assert.Equal("Concurrent authored title", (await Documents.GetAsync(doc.DocumentId))!.Title);
    }

    [Fact]
    public async Task AccountAndBackendScopesUseStableIdentityAndNeverRevealOtherCanon()
    {
        var doc = await Book(); await Service().LoadAsync(doc, CanonKind.Place, true, true, null, default);
        await Account.GetTokenAsync(default); // Silent token renewal must preserve generation and scope.
        Assert.NotNull(await Service().CachedAsync(doc, CanonKind.Place));
        Assert.Null(await Service("https://another.invalid/").CachedAsync(doc, CanonKind.Place));
        await Account.SignOutAsync();
        await Assert.ThrowsAsync<DeviceSignInRequiredException>(() => Service().CachedAsync(doc, CanonKind.Place));
        _identity.Id = "account-2"; await Account.SignInAsync();
        Assert.Null(await Service().CachedAsync(doc, CanonKind.Place)); Assert.Empty((await Service().ContextAsync(doc)).Snapshots);
        _identity.Id = "account-1"; await Account.SignInAsync(); Assert.NotNull(await Service().CachedAsync(doc, CanonKind.Place));
        var scope = LocalBibleStore.ScopeKey(new("https://test.invalid/"), _identity.Id!);
        var file = Path.Combine(_root, "canon", scope, doc.DocumentId.ToString("N"), "Place.json");
        var bytes = await File.ReadAllTextAsync(file);
        Assert.DoesNotContain("account-1", bytes); Assert.DoesNotContain("secret-access-token", bytes);
    }

    [Fact]
    public async Task CacheRejectsFutureSchemaTamperingAndWrongCloudIdentityWithoutReplacingFiles()
    {
        var doc = await Book(); var saved = await Service().LoadAsync(doc, CanonKind.Character, true, true, null, default);
        var path = Path.Combine(_root, "canon", saved.Scope, doc.DocumentId.ToString("N"), "Character.json");
        var original = await File.ReadAllTextAsync(path);
        await Assert.ThrowsAsync<InvalidDataException>(() => Cache.SaveAsync(saved with { Version = 99 }));
        Assert.Equal(original, await File.ReadAllTextAsync(path));
        await Assert.ThrowsAsync<InvalidDataException>(() => Cache.ReadAsync(saved.Scope, doc with { ServerDocumentId = Guid.NewGuid() }, saved.Kind));
        var future = original.Replace("\"version\":1", "\"version\":99");
        await File.WriteAllTextAsync(path, future);
        await Assert.ThrowsAsync<InvalidDataException>(() => Service().LoadAsync(doc, saved.Kind, true, true, null, default));
        Assert.Equal(future, await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task UnsynchronizedOrUnsupportedIdentityCannotMakeRequestsAndStaleCacheIsExcludedFromContext()
    {
        var doc = await Book(); await Service().LoadAsync(doc, CanonKind.Character, true, true, null, default);
        var dirty = doc with { SyncState = LocalSyncState.PendingUpload };
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service().LoadAsync(dirty, CanonKind.Character, true, true, null, default));
        Assert.Empty((await Service().ContextAsync(dirty)).Snapshots);
        Assert.Empty((await Service().ContextAsync(doc with { ServerVersion = "v2" })).Snapshots);
        _identity.Id = null; await Account.SignInAsync(); Assert.False(Service().CanView);
        await Assert.ThrowsAsync<DeviceSignInRequiredException>(() => Service().LoadAsync(doc, CanonKind.Character, true, true, null, default));
        Assert.Equal(2, _handler.Calls.Count);
    }

    [Fact]
    public void TypedContextReportsUnknownEntityReferencesAndParserRejectsDuplicateIds()
    {
        var id = Guid.NewGuid();
        var timeline = new DeviceBibleSnapshot(1, id, CanonKind.Timeline, "token", "v1", "v1", new('A',64), DateTimeOffset.UtcNow,
            "{\"schemaVersion\":\"1.0\",\"events\":[{\"title\":\"Arrival\",\"participants\":[\"unknown\"],\"locationId\":\"missing\"}]}",0,true);
        Assert.Equal(2, new DeviceCanonContext(id, "v1", new Dictionary<CanonKind,DeviceBibleSnapshot> { [CanonKind.Timeline] = timeline }).UnresolvedReferences().Count);
        Assert.Throws<InvalidDataException>(() => CanonContent.Parse(CanonKind.Character, "{\"schemaVersion\":\"1.0\",\"characters\":[{\"id\":\"same\",\"name\":\"A\"},{\"id\":\"same\",\"name\":\"B\"}]}"));
        Assert.Throws<InvalidDataException>(() => CanonContent.Parse(CanonKind.Character, "{\"schemaVersion\":\"1.0\",\"characters\":[{\"name\":\"A\",\"facts\":[],\"facts\":[]}]}"));
    }

    [Fact]
    public async Task RestartRetainsPreviousSnapshotWhenProcessStoppedDuringRefresh()
    {
        var doc = await Book(); var original = await Service().LoadAsync(doc, CanonKind.Timeline, true, true, null, default);
        await Cache.SaveAsync(original with { RefreshState = "Refreshing" });
        _network.SetOnline(false);
        var restarted = await Service().CachedAsync(doc, CanonKind.Timeline);
        Assert.Equal(original.Snapshot, restarted!.Snapshot); Assert.Equal("Refreshing", restarted.RefreshState);
        Assert.Empty((await Service().ContextAsync(doc)).Snapshots);
        _network.SetOnline(true); _handler.Calls.Clear();
        Assert.Equal("Ready", (await Service().LoadAsync(doc, CanonKind.Timeline, false, false, null, default)).RefreshState);
        Assert.Equal(new[] { "GET" }, _handler.Calls);
    }

    [Fact]
    public async Task SharedCanonReaderEncodesProviderHtmlAndRendersNestedFields()
    {
        var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        var json = "{\"schemaVersion\":\"1.0\",\"characters\":[{\"name\":\"<script>unsafe()</script>\",\"facts\":[{\"fact\":\"Lives by the sea\"}]}]}";
        string html = await renderer.Dispatcher.InvokeAsync(async () => (await renderer.RenderComponentAsync<StoryCanonView>(ParameterView.FromDictionary(new Dictionary<string,object?> {
            [nameof(StoryCanonView.Kind)] = CanonKind.Character, [nameof(StoryCanonView.Content)] = CanonContent.Parse(CanonKind.Character, json),
            [nameof(StoryCanonView.Status)] = "Stale canon" }))).ToHtmlString());
        var dom = new HtmlParser().ParseDocument(html);
        Assert.Empty(dom.QuerySelectorAll("script")); Assert.Contains("<script>unsafe()</script>", dom.Body!.TextContent);
        Assert.Contains("Lives by the sea", dom.Body.TextContent); Assert.Contains("Stale canon", dom.Body.TextContent);
    }


    [Fact]
    public async Task RealPanelUpdatesAllAndHandlesLoadingTokenRenewalOfflineErrorAndAccountSwitch()
    {
        var doc = await Book(); var repository = new LocalDocumentRepository(Documents);
        var components = new CaptureComponents();
        var services = new ServiceCollection().AddLogging()
            .AddSingleton(Account).AddSingleton(_network).AddSingleton(repository).AddSingleton(Service())
            .AddSingleton(new DeviceSyncEngine(Documents, new(Path.Combine(_root, "sync")), new EmptySync(),
                Account, _network, repository, new("Test", new("https://test.invalid/"))))
            .AddSingleton<IComponentActivator>(components).BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        var root = await renderer.Dispatcher.InvokeAsync(() => renderer.RenderComponentAsync<DeviceBiblePanel>(
            ParameterView.FromDictionary(new Dictionary<string,object?> { [nameof(DeviceBiblePanel.DocumentId)] = doc.DocumentId })));
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _handler.BeforeReply = async () => {
            await Account.GetTokenAsync(default);
            await repository.CreateAsync("Unrelated local document");
            started.TrySetResult();
            await release.Task;
        };
        Task? run = null;
        await renderer.Dispatcher.InvokeAsync(() => {
            run = ((IHandleEvent)components.Panel!).HandleEventAsync(new EventCallbackWorkItem((Func<Task>)(() =>
                (Task)typeof(DeviceBiblePanel).GetMethod("Run", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                    .Invoke(components.Panel, new object?[] { null, true, false })!)), null);
        });
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var loading = await renderer.Dispatcher.InvokeAsync(() => root.ToHtmlString());
        Assert.Contains("Cancel canon request", loading);
        Assert.All(new HtmlParser().ParseDocument(loading).QuerySelectorAll("fieldset"), f => Assert.True(f.HasAttribute("disabled")));
        release.SetResult(); await run!;
        var completed = await renderer.Dispatcher.InvokeAsync(() => root.ToHtmlString());
        Assert.DoesNotContain("Canon request canceled", completed);
        Assert.Equal(3, (await Service().ContextAsync(doc)).Snapshots.Count);
        Assert.Equal(6, _handler.Calls.Count);
        _network.SetOnline(false);
        string offline = "";
        for (int i = 0; i < 100; i++)
        {
            offline = await renderer.Dispatcher.InvokeAsync(() => root.ToHtmlString());
            if (offline.Contains("Offline: showing story facts") && offline.Contains("Anna")) break;
            await Task.Delay(10);
        }
        Assert.Contains("Offline: showing story facts", offline); Assert.Contains("Anna", offline);
        string? evidence = Environment.GetEnvironmentVariable("WRITERAPP_P02_EVIDENCE");
        if (evidence is not null) await File.WriteAllTextAsync(Path.Combine(evidence, "canon-offline.html"), "<!doctype html><html><head><meta charset=\"utf-8\"><title>Desktop canon rendering fixture</title></head><body>" + offline + "</body></html>");
        await renderer.Dispatcher.InvokeAsync(() => Account.SignOutAsync());
        var signedOut = await renderer.Dispatcher.InvokeAsync(() => root.ToHtmlString());
        Assert.DoesNotContain("Anna", signedOut); Assert.Contains("Sign in", signedOut);
        await renderer.Dispatcher.InvokeAsync(() => Account.SignInAsync());
        _network.SetOnline(true); _handler.BeforeReply = null; _handler.Failure = "disconnect";
        await renderer.Dispatcher.InvokeAsync(() => ((IHandleEvent)components.Panel!).HandleEventAsync(new EventCallbackWorkItem((Func<Task>)(() =>
            (Task)typeof(DeviceBiblePanel).GetMethod("Run", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .Invoke(components.Panel, new object?[] { CanonKind.Character, true, true })!)), null));
        var failed = await renderer.Dispatcher.InvokeAsync(() => root.ToHtmlString());
        Assert.Contains("Disconnected", failed); Assert.Contains("interrupted", failed); Assert.Contains("Anna", failed);
        await services.DisposeAsync();
    }

    private sealed class CaptureComponents : IComponentActivator
    {
        public DeviceBiblePanel? Panel;
        public IComponent CreateInstance(Type type)
        {
            var value = (IComponent)Activator.CreateInstance(type)!;
            if (value is DeviceBiblePanel panel) Panel = panel;
            return value;
        }
    }
    private sealed class EmptySync : IDeviceSyncApi
    {
        public Task<string> GetOwnerAsync(CancellationToken ct) => Task.FromResult("account-1");
        public Task<WriterApp.Shared.Sync.SyncChanges> ChangesAsync(string? cursor, CancellationToken ct) =>
            Task.FromResult(new WriterApp.Shared.Sync.SyncChanges([], "cursor", false));
        public Task<WriterApp.Shared.Sync.SyncSnapshot> DownloadAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
        public Task<WriterApp.Shared.Sync.SyncMutationResult> MutateAsync(Guid id, WriterApp.Shared.Sync.SyncMutation request, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class Identity : IDeviceIdentityClient
    {
        public string? Id = "account-1";
        public bool IsConfigured => true;
        public Task<DeviceAccessToken?> AcquireAsync(bool interactive, CancellationToken ct) =>
            Task.FromResult<DeviceAccessToken?>(new("secret-access-token", DateTimeOffset.UtcNow.AddHours(1), "Same display name", Id));
        public Task SignOutAsync() => Task.CompletedTask;
    }
    private sealed class Handler : HttpMessageHandler
    {
        public Guid DocumentId;
        public string? Failure;
        public Func<Task>? BeforeReply;
        public DeviceBibleRefreshRequest? Request;
        public List<string> Calls = [];
        private bool _exists;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls.Add(request.Method.Method);
            bool post = request.Method == HttpMethod.Post;
            if (post) Request = await request.Content!.ReadFromJsonAsync<DeviceBibleRefreshRequest>(ct);
            if (post && BeforeReply is not null) await BeforeReply();
            if (post && Failure == "disconnect") throw new HttpRequestException("Disconnected");
            if (post && Failure == "stale") return new(HttpStatusCode.Conflict);
            var kind = Enum.GetValues<CanonKind>().Single(k => request.RequestUri!.AbsolutePath.Contains("/" + k.ToString().ToLowerInvariant() + "/"));
            var content = _exists || post ? Content(kind) : JsonSerializer.Serialize(new Dictionary<string,object> { ["schemaVersion"] = "1.0", [CanonContent.Collection(kind)] = Array.Empty<object>() });
            if (post && Failure == "invalid-json") content = "{";
            if (post && Failure == "partial") content = "{\"schemaVersion\":\"1.0\",\"characters\":[{\"id\":\"a\"}]}";
            if (post && Failure == "invalid-reference") content = "{\"schemaVersion\":\"1.0\",\"characters\":[{\"name\":\"A\",\"facts\":[{\"sectionId\":\"bad\"}]}]}";
            var snapshot = new DeviceBibleSnapshot(1, post && Failure == "wrong-document" ? Guid.NewGuid() : DocumentId,
                post && Failure == "wrong-kind" ? CanonKind.Place : kind, _exists || post ? "ready-token" : "missing",
                post && Failure == "unconfirmed" ? null : _exists || post ? "v1" : null, "v1",
                _exists || post ? new('A', 64) : "", _exists || post ? DateTimeOffset.UtcNow : null, content, 0, _exists || post);
            if (post) _exists = true;
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(snapshot) };
        }
    }
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}

