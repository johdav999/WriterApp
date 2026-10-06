using System.Net;
using System.Reflection;
using System.Text.Json;
using AngleSharp.Html.Parser;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using WriterApp.Application.AI;
using WriterApp.Application.Usage;
using WriterApp.Device.Shared.Components;
using WriterApp.Device.Shared.Services;
using WriterApp.Device.Shared.Storage;
using WriterApp.Shared.Canon;
using WriterApp.Shared.Sync;
using WriterApp.UI.Shared;
using Xunit;

namespace WriterApp.Tests;

public sealed class LocalConsistencyPanelTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ConsistencyPanel", Guid.NewGuid().ToString("N"));
    private const string Plain = "Elin carried a heavy suitcase. The train arrived.";
    private readonly DeviceAccountService _account = new(new Identity());
    private readonly Api _api = new(); private readonly Components _components = new();
    private readonly Js _js = new();
    private LocalDocumentRepository _repository = null!; private LocalAiStore _history = null!;
    private LocalBibleStore _canon = null!; private LocalDocument _source = null!;
    private readonly DeviceConnectivity _network = new(); private int _before, _after, _jumps, _highlightClears;
    private const string Backend = "https://test.invalid/";
    private static string Scope => LocalBibleStore.ScopeKey(new(Backend), "account-1");
    private async Task<(ServiceProvider Services, HtmlRenderer Renderer, Microsoft.AspNetCore.Components.Web.HtmlRendering.HtmlRootComponent Root)> Render(bool currentCanon = true, string? content = null)
    {
        await _account.SignInAsync(); var store = new FileLocalDocumentStore(_root); _repository = new(store);
        var doc = await store.CreateProjectAsync("Book");
        doc = await store.SaveAsync(doc with { Sections = doc.Sections.Select(s => s with { Pages = s.Pages.Select(p => p with { Content = content ?? "<p>Elin carried a <strong>heavy</strong> suitcase. The train arrived.</p>" }).ToArray() }).ToArray() });
        _source = await store.ApplySyncAsync(doc with { ServerDocumentId = Guid.NewGuid(), ServerVersion = "v1", SyncState = LocalSyncState.Synced }, doc.LocalRevision, default, projects: true);
        _api.Section = _source.Sections[0].SectionId; _history = new(Path.Combine(_root, "ai")); _canon = new(Path.Combine(_root, "canon"));
        foreach (var kind in Enum.GetValues<CanonKind>()) {
            var snapshot = new DeviceBibleSnapshot(1, _source.ServerDocumentId!.Value, kind, kind + "-token", "v1", "v1", new string('A', 64), DateTimeOffset.UtcNow,
                JsonSerializer.Serialize(new Dictionary<string, object> { ["schemaVersion"] = "1.0", [CanonContent.Collection(kind)] = Array.Empty<object>() }), 0, true);
            if (currentCanon || kind == CanonKind.Character) await _canon.SaveAsync(new(1, Scope, _source.DocumentId, _source.ServerDocumentId.Value, kind, snapshot, DateTimeOffset.UtcNow, "Ready"));
        }
        var http = new HttpClient(new RejectHttp()) { BaseAddress = new(Backend) }; var host = new DeviceHostOptions("Test", new(Backend));
        var ai = new DeviceAiService(_api, _account, _network);
        var bibles = new DeviceBibleService(new(http), _canon, _repository, _account, _network, host);
        var services = new ServiceCollection().AddLogging().AddSingleton(_repository).AddSingleton(_account).AddSingleton(_network).AddSingleton(ai).AddSingleton(bibles)
            .AddSingleton(_history).AddSingleton(new LocalAiHistoryActions(_repository, _history)).AddSingleton(new DevicePromptLibrary(http, _account, _history))
            .AddSingleton(new DeviceSyncEngine(store, new(Path.Combine(_root, "sync")), new EmptySync(), _account, _network, _repository, host))
            .AddSingleton<IJSRuntime>(_js).AddSingleton<IComponentActivator>(_components).BuildServiceProvider();
        var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        var root = await renderer.Dispatcher.InvokeAsync(() => renderer.RenderComponentAsync<LocalAiPanel>(ParameterView.FromDictionary(new Dictionary<string, object?> {
            ["DocumentId"] = _source.DocumentId, ["SectionId"] = _source.Sections[0].SectionId, ["Mode"] = "consistency",
            ["BeforeWork"] = (Func<Task<bool>>)(() => { _before++; return Task.FromResult(true); }),
            ["AfterWork"] = (Func<Task>)(() => { _after++; return Task.CompletedTask; }),
            ["JumpConsistency"] = EventCallback.Factory.Create<ConsistencyPassage>(this, p => { Assert.Equal(_source.Sections[0].Pages[0].PageId, p.PageId); Assert.Equal(p.Quote, p.PlainText.Substring(p.Start, p.Quote.Length)); _jumps++; }),
            ["ClearConsistencyHighlight"] = EventCallback.Factory.Create(this, () => { _highlightClears++; })
        })));
        return (services, renderer, root);
    }
    private Task Event(HtmlRenderer r, string name, params object?[] args) => r.Dispatcher.InvokeAsync(() => ((IHandleEvent)_components.Panel!).HandleEventAsync(new EventCallbackWorkItem((Func<Task>)(() => (Task)typeof(LocalAiPanel).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(_components.Panel, args)!)), null));
    private void Set(string name, object value) => typeof(LocalAiPanel).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(_components.Panel, value);
    private async Task<string> Html(HtmlRenderer r, Microsoft.AspNetCore.Components.Web.HtmlRendering.HtmlRootComponent root) => await r.Dispatcher.InvokeAsync(root.ToHtmlString);
    [Fact]
    public async Task ReportWithMidWordTrainAnchorReviewsAndAppliesTheExactQuotedPassage()
    {
        const string quote = "The train gently pulled into Vinterhamn just as the sun was setting, the platform bathed in a soft, fading glow. Elin stepped off, gripping her suitcase a little tighter—it suddenly felt heavier than before.";
        const string fix = "Nearby, a man leaned casually against the station lamp post, his dark wool coat wrapped snugly around him, hands tucked deep into his pockets. As the train pulled into the station, his warm, friendly smile shone through the dim light.";
        const string suffix = " Nearby, a man leaned casually against the station lamp post. Then the train entered the station.";
        const string content = "<p>" + quote + suffix + "</p>";
        var (services, renderer, root) = await Render(content: content); await using var ownedServices = services; await using var ownedRenderer = renderer;
        _api.ReportJson = JsonSerializer.Serialize(new { schemaVersion = "1.0", issues = new[] { new {
            severity = "medium", type = "timeline", message = "Inconsistent timing of Jonas' arrival relative to the train's arrival.",
            evidence = new { sectionId = _api.Section, quote }, suggestedFix = fix,
            anchor = new { plainTextStart = 61, plainTextLength = 102 }
        } } });
        await Event(renderer, "RunAi"); await Event(renderer, "ReviewConsistencySuggestion", 0);
        var html = await Html(renderer, root);
        var review = new HtmlParser().ParseDocument(html).QuerySelector(".consistency-approval")!;
        Assert.NotNull(review);
        Assert.Equal(quote, review.QuerySelectorAll(".ai-preview-text")[0].TextContent);
        Assert.Equal(fix, review.QuerySelectorAll(".ai-preview-text")[1].TextContent);
        Assert.DoesNotContain("role=\"alert\"", html);
        Assert.Equal(content, (await _repository.LoadAsync(_source.DocumentId))!.Sections[0].Pages[0].Content);
        await Event(renderer, "ApplyConsistencySuggestion");
        Assert.Equal("<p>" + fix + suffix + "</p>", (await _repository.LoadAsync(_source.DocumentId))!.Sections[0].Pages[0].Content);
        var applied = Assert.Single(await _history.HistoryAsync(_source.DocumentId), h => h.Status == "Applied");
        await new LocalAiHistoryActions(_repository, _history).ChangeAsync(_source.DocumentId, applied.Id, false);
        Assert.Equal(content, (await _repository.LoadAsync(_source.DocumentId))!.Sections[0].Pages[0].Content);
    }

    [Fact]
    public async Task RejectedReviewDisplaysTheReasonInTheClickedFindingAndCanBeRetried()
    {
        var (services, renderer, root) = await Render(); await using var ownedServices = services; await using var ownedRenderer = renderer;
        await Event(renderer, "RunAi");
        _js.RangeError = "This passage spans different formatting. Revise it manually.";
        await Event(renderer, "ReviewConsistencySuggestion", 0);
        var findings = new HtmlParser().ParseDocument(await Html(renderer, root)).QuerySelectorAll(".consistency-finding");
        Assert.Equal(_js.RangeError, findings[0].QuerySelector("[role=alert]")!.TextContent);
        Assert.Null(findings[1].QuerySelector("[role=alert]"));
        Assert.Null(findings[0].QuerySelector(".consistency-approval"));
        Assert.Contains("heavy", (await _repository.LoadAsync(_source.DocumentId))!.Sections[0].Pages[0].Content);
        _js.RangeError = null;
        await Event(renderer, "ReviewConsistencySuggestion", 0);
        Assert.Contains("Apply suggestion", await Html(renderer, root));
        Assert.DoesNotContain("role=\"alert\"", await Html(renderer, root));
    }
    [Fact]
    public async Task DuplicateTrainDeletionIsReviewedSavedAndUndoableWithoutDeletingTheFirstArrival()
    {
        const string content = "<p>The train gently pulled into Vinterhamn. Elin stepped off.</p><p>She smiled. Then the train entered the station.</p>";
        var (services, renderer, root) = await Render(content: content); await using var ownedServices = services; await using var ownedRenderer = renderer;
        const string quote = "Then the train entered the station.";
        _api.ReportJson = JsonSerializer.Serialize(new { schemaVersion = "1.0", issues = new[] { new {
            severity = "medium", type = "timeline", message = "Train arrives twice.", evidence = new { sectionId = _api.Section, quote },
            comparisonEvidence = new { sectionId = _api.Section, quote = "The train gently pulled into Vinterhamn." }, fixKind = "delete", suggestedFix = "",
            anchor = new { plainTextStart = -1, plainTextLength = quote.Length }
        } } });
        await Event(renderer, "RunAi");
        Assert.Contains("Remove this passage; keep the conflicting passage.", await Html(renderer, root));
        await Event(renderer, "ReviewConsistencySuggestion", 0);
        Assert.Contains("Suggested deletion", await Html(renderer, root));
        Assert.Equal(content, (await _repository.LoadAsync(_source.DocumentId))!.Sections[0].Pages[0].Content);
        await Event(renderer, "ApplyConsistencySuggestion");
        var saved = (await _repository.LoadAsync(_source.DocumentId))!;
        Assert.Equal(content.Replace(quote, ""), saved.Sections[0].Pages[0].Content);
        Assert.Equal(1, _api.Requests);
        var history = Assert.Single(await _history.HistoryAsync(_source.DocumentId), h => h.Status == "Applied");
        var restarted = new LocalDocumentRepository(new FileLocalDocumentStore(_root));
        var actions = new LocalAiHistoryActions(restarted, new LocalAiStore(Path.Combine(_root, "ai")));
        await actions.ChangeAsync(_source.DocumentId, history.Id, false);
        Assert.Equal(content, (await restarted.LoadAsync(_source.DocumentId))!.Sections[0].Pages[0].Content);
        await actions.ChangeAsync(_source.DocumentId, history.Id, true);
        Assert.Equal(saved.Sections[0].Pages[0].Content, (await restarted.LoadAsync(_source.DocumentId))!.Sections[0].Pages[0].Content);
    }
    [Fact]
    public async Task UnchangedLegacySuggestionGeneratesAnActualRevisionWithConflictingPassageContext()
    {
        var (services, renderer, root) = await Render(); await using var ownedServices = services; await using var ownedRenderer = renderer;
        _api.ReportJson = JsonSerializer.Serialize(new { schemaVersion = "1.0", issues = new[] { new {
            severity = "medium", type = "character", message = "Weight conflict.", evidence = new { sectionId = _api.Section, quote = "heavy" },
            comparisonEvidence = new { sectionId = _api.Section, quote = "The train arrived." }, suggestedFix = "heavy", anchor = new { plainTextStart = 15, plainTextLength = 5 }
        } } });
        await Event(renderer, "RunAi");
        Assert.Contains("Generate &amp; review fix", await Html(renderer, root));
        await Event(renderer, "ReviewConsistencySuggestion", 0);
        Assert.Equal(2, _api.Requests);
        Assert.Contains("The train arrived.", _api.Last!.Parameters!["instruction"]!.ToString());
        await Event(renderer, "ApplyConsistencySuggestion");
        Assert.Contains("<strong>light</strong>", (await _repository.LoadAsync(_source.DocumentId))!.Sections[0].Pages[0].Content);
    }
    [Fact]
    public async Task ActualPanelAnalyzesFiltersJumpsApprovesMultipleFixesAndReopensHistory()
    {
        var (services, renderer, root) = await Render(); await using var ownedServices = services; await using var ownedRenderer = renderer;
        await Event(renderer, "RunAi"); Assert.Equal(3, _api.Last!.ExpectedCanonVersions!.Count); Assert.Equal(Plain, _api.Last.SurroundingText);
        Assert.Contains("Character current", await Html(renderer, root)); Assert.Contains("Jump to passage", await Html(renderer, root));
        await renderer.Dispatcher.InvokeAsync(() => {
            typeof(ConsistencyReport).GetField("_severity", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(_components.Report, "High");
            ((IHandleEvent)_components.Report!).HandleEventAsync(new EventCallbackWorkItem((Action)(() => { })), null).GetAwaiter().GetResult();
        });
        Assert.Single(new HtmlParser().ParseDocument(await Html(renderer, root)).QuerySelectorAll(".consistency-finding"));
        await Event(renderer, "JumpFinding", 0); Assert.Equal(1, _jumps);
        await Event(renderer, "ReviewConsistencySuggestion", 0);
        Assert.Contains("Apply suggestion", await Html(renderer, root)); Assert.Equal(_source.Sections[0].Pages[0].Content, (await _repository.LoadAsync(_source.DocumentId))!.Sections[0].Pages[0].Content);
        var preview = new HtmlParser().ParseDocument(await Html(renderer, root)).QuerySelector(".consistency-approval")!;
        Assert.Equal(new[] { "Current wording", "Suggested wording" }, preview.QuerySelectorAll(".ai-preview-label").Select(e => e.TextContent));
        Assert.Equal(new[] { "heavy", "light" }, preview.QuerySelectorAll(".ai-preview-text").Select(e => e.TextContent));
        Assert.Equal(new[] { "Apply suggestion", "Discard suggestion" }, preview.QuerySelectorAll("button").Select(e => e.TextContent));
        string? evidence = Environment.GetEnvironmentVariable("WRITERAPP_P03_EVIDENCE");
        if (evidence is not null) await File.WriteAllTextAsync(Path.Combine(evidence, "consistency-review.html"), await Html(renderer, root));
        await Event(renderer, "ApplyConsistencySuggestion"); await Event(renderer, "ReviewConsistencySuggestion", 1); await Event(renderer, "ApplyConsistencySuggestion");
        var saved = (await _repository.LoadAsync(_source.DocumentId))!; Assert.Contains("<strong>light</strong>", saved.Sections[0].Pages[0].Content); Assert.Contains("The train left.", saved.Sections[0].Pages[0].Content);
        Assert.Equal(JsonSerializer.Serialize(_source.Project), JsonSerializer.Serialize(saved.Project));
        var entries = (await _history.HistoryAsync(_source.DocumentId)).Where(h => h.Status == "Applied").ToArray(); Assert.Equal(2, entries.Length);
        var restarted = new LocalDocumentRepository(new FileLocalDocumentStore(_root)); var history = new LocalAiStore(Path.Combine(_root, "ai"));
        var actions = new LocalAiHistoryActions(restarted, history); var last = entries.Single(h => h.Proposed == "The train left.");
        await actions.ChangeAsync(_source.DocumentId, last.Id, false); Assert.Contains("train arrived", (await restarted.LoadAsync(_source.DocumentId))!.Sections[0].Pages[0].Content);
        await actions.ChangeAsync(_source.DocumentId, last.Id, true); Assert.Contains("train left", (await restarted.LoadAsync(_source.DocumentId))!.Sections[0].Pages[0].Content);
        Assert.Equal(_before, _after);
    }
    [Fact]
    public async Task CheckIsVisibleBeforeCollapsedReferencesAndDiscardNeverChangesWriting()
    {
        _api.EmptyFix = true;
        var (services, renderer, root) = await Render(); await using var ownedServices = services; await using var ownedRenderer = renderer;
        string initial = await Html(renderer, root);
        string? evidence = Environment.GetEnvironmentVariable("WRITERAPP_P03_EVIDENCE");
        if (evidence is not null) await File.WriteAllTextAsync(Path.Combine(evidence, "consistency-start.html"), initial);
        var markup = new HtmlParser().ParseDocument(initial);
        Assert.False(markup.QuerySelector(".consistency-reference")!.HasAttribute("open"));
        Assert.True(initial.IndexOf("class=\"consistency-run\"", StringComparison.Ordinal) < initial.IndexOf("class=\"consistency-reference\"", StringComparison.Ordinal));
        await Event(renderer, "RunAi");
        await Event(renderer, "ReviewConsistencySuggestion", 0);
        Assert.Contains("Apply suggestion", await Html(renderer, root));
        await Event(renderer, "JumpFinding", 0);
        await Event(renderer, "DiscardConsistencySuggestion", 0);
        Assert.Equal(1, _highlightClears);
        if (evidence is not null) await File.WriteAllTextAsync(Path.Combine(evidence, "consistency-discarded.html"), await Html(renderer, root));
        Assert.Contains("Review again", await Html(renderer, root));
        Assert.DoesNotContain("Apply suggestion", await Html(renderer, root));
        await Event(renderer, "ApplyConsistencySuggestion");
        Assert.Equal(_source.Sections[0].Pages[0].Content, (await _repository.LoadAsync(_source.DocumentId))!.Sections[0].Pages[0].Content);
        Assert.DoesNotContain(await _history.HistoryAsync(_source.DocumentId), h => h.Status == "Applied");
        await Event(renderer, "ReviewConsistencySuggestion", 0);
        Assert.Contains("Apply suggestion", await Html(renderer, root));
        await Event(renderer, "DiscardConsistencySuggestion", 0);
        await Event(renderer, "RunAi");
        Assert.DoesNotContain("Review again", await Html(renderer, root));
    }
    [Fact]
    public async Task ConflictingPassageNavigationAndIntentionalDecisionSurviveAnotherCheckWithoutApplyingWriting()
    {
        var (services, renderer, root) = await Render(); await using var ownedServices = services; await using var ownedRenderer = renderer;
        await Event(renderer, "RunAi");
        Assert.Contains("Conflicting passage", await Html(renderer, root));
        await Event(renderer, "JumpComparison", 0); Assert.Equal(1, _jumps);
        await Event(renderer, "ReviewConsistencySuggestion", 0);
        await Event(renderer, "MarkIntentional", 0);
        Assert.Equal(1, _highlightClears);
        Assert.Contains("Marked intentional on this device", await Html(renderer, root));
        Assert.DoesNotContain("Apply suggestion", await Html(renderer, root));
        await Event(renderer, "RunAi");
        Assert.Contains("Check this again", await Html(renderer, root));
        await Event(renderer, "ReviewConsistencySuggestion", 0);
        Assert.DoesNotContain("Apply suggestion", await Html(renderer, root));
        await Event(renderer, "MarkIntentional", 0);
        await Event(renderer, "ReviewConsistencySuggestion", 0);
        Assert.Contains("Apply suggestion", await Html(renderer, root));
        Assert.Equal(_source.Sections[0].Pages[0].Content, (await _repository.LoadAsync(_source.DocumentId))!.Sections[0].Pages[0].Content);
        Assert.DoesNotContain(await _history.HistoryAsync(_source.DocumentId), h => h.Status == "Applied");
    }
    [Fact]
    public async Task MissingCanonAutomaticallyAttemptsPreparationAndDisclosesLimitsWhileOldBackendFailsBeforeAi()
    {
        var (services, renderer, root) = await Render(false); await using var ownedServices = services; await using var ownedRenderer = renderer;
        await Event(renderer, "RunAi"); Assert.Equal(1, _api.Requests); Assert.Contains("Some story references could not be prepared", await Html(renderer, root));
        Assert.DoesNotContain("Check with the available facts", await Html(renderer, root));
        _api.Capability = false;
        await Event(renderer, "RunAi"); Assert.Equal(1, _api.Requests); Assert.Contains("Update the backend", await Html(renderer, root));
        _api.Capability = true; await Event(renderer, "RunAi"); Assert.Single(_api.Last!.ExpectedCanonVersions!); Assert.Equal("{}", _api.Last.Parameters!["timeline_bible_json"]);
        Assert.Contains("Timeline omitted", await Html(renderer, root));
    }
    [Theory]
    [InlineData("valid")][InlineData("empty")][InlineData("invalid")][InlineData("stale")][InlineData("canon")][InlineData("account")][InlineData("echo")][InlineData("quota")][InlineData("offline")]
    public async Task GeneratedAndDirectFixBoundariesPreserveWritingUntilValidApproval(string scenario)
    {
        _api.Instruction = true;
        _api.EmptyFix = scenario == "empty";
        var (services, renderer, root) = await Render(); await using var ownedServices = services; await using var ownedRenderer = renderer;
        await Event(renderer, "RunAi");
        if (scenario == "empty") Assert.Contains("Generate &amp; review fix", await Html(renderer, root));
        if (scenario == "invalid") _api.Rewrite = "Rewrite the selected span.";
        if (scenario == "echo") _api.Echo = false;
        if (scenario == "quota") _api.Quota = 0;
        if (scenario == "offline") _network.SetOnline(false);
        await Event(renderer, "ReviewConsistencySuggestion", 0);
        if (scenario is "valid" or "empty" or "stale" or "canon" or "account") Assert.Contains("Apply suggestion", await Html(renderer, root));
        await renderer.Dispatcher.InvokeAsync(() => _account.GetTokenAsync(default));
        if (scenario == "stale") { var current = (await _repository.LoadAsync(_source.DocumentId))!; await _repository.SaveAsync(current with { Title = "Later authored title" }); }
        if (scenario == "canon") { var cache = (await _canon.ReadAsync(Scope, _source, CanonKind.Character))!; await _canon.SaveAsync(cache with { Snapshot = cache.Snapshot! with { SnapshotVersion = "new" } }); }
        if (scenario == "account") await renderer.Dispatcher.InvokeAsync(() => _account.SignOutAsync());
        await Event(renderer, "ApplyConsistencySuggestion");
        var saved = (await _repository.LoadAsync(_source.DocumentId))!;
        if (scenario is "valid" or "empty") { Assert.Contains("<strong>light</strong>", saved.Sections[0].Pages[0].Content); Assert.Single(await _history.HistoryAsync(_source.DocumentId), h => h.Status == "Applied"); }
        else { Assert.Equal(_source.Sections[0].Pages[0].Content, saved.Sections[0].Pages[0].Content); Assert.DoesNotContain(await _history.HistoryAsync(_source.DocumentId), h => h.Status == "Applied"); }
        if (scenario == "account") Assert.DoesNotContain("Apply suggestion", await Html(renderer, root));
    }
    [Fact]
    public async Task CancelledGenerationShowsLoadingAndProducesNoWritingChange()
    {
        _api.Instruction = true; var (services, renderer, root) = await Render(); await using var ownedServices = services; await using var ownedRenderer = renderer;
        await Event(renderer, "RunAi"); var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _api.Wait = async ct => { started.SetResult(); await Task.Delay(Timeout.Infinite, ct); }; Task? run = null;
        await renderer.Dispatcher.InvokeAsync(() => { run = Event(renderer, "ReviewConsistencySuggestion", 0); });
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5)); Assert.Contains("Cancel request", await Html(renderer, root));
        await renderer.Dispatcher.InvokeAsync(() => ((CancellationTokenSource)typeof(LocalAiPanel).GetField("_cancel", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(_components.Panel)!).Cancel());
        await run!; Assert.Contains("Canceled", await Html(renderer, root)); Assert.DoesNotContain("Apply suggestion", await Html(renderer, root));
        Assert.Equal(_source.Sections[0].Pages[0].Content, (await _repository.LoadAsync(_source.DocumentId))!.Sections[0].Pages[0].Content);
    }
    [Fact]
    public async Task PriorApprovalDoesNotAuthorizeOverwritingLaterUserEdits()
    {
        var (services, renderer, root) = await Render(); await using var ownedServices = services; await using var ownedRenderer = renderer;
        await Event(renderer, "RunAi"); await Event(renderer, "ReviewConsistencySuggestion", 0); await Event(renderer, "ApplyConsistencySuggestion");
        var current = (await _repository.LoadAsync(_source.DocumentId))!;
        var authored = await _repository.SaveAsync(current with { Sections = current.Sections.Select(s => s with { Pages = s.Pages.Select(p => p with { Content = p.Content.Replace("arrived", "arrived unexpectedly") }).ToArray() }).ToArray() });
        await Event(renderer, "ReviewConsistencySuggestion", 1);
        Assert.Contains("writing or planning changed", await Html(renderer, root));
        await Event(renderer, "ApplyConsistencySuggestion");
        Assert.Equal(authored.Sections[0].Pages[0].Content, (await _repository.LoadAsync(_source.DocumentId))!.Sections[0].Pages[0].Content);
        Assert.Single(await _history.HistoryAsync(_source.DocumentId), h => h.Status == "Applied");
    }
    private sealed class Components : IComponentActivator {
        public LocalAiPanel? Panel; public ConsistencyReport? Report;
        public IComponent CreateInstance(Type t) { var c = (IComponent)Activator.CreateInstance(t)!; if (c is LocalAiPanel p) Panel = p; if (c is ConsistencyReport r) Report = r; return c; }
    }
    private sealed class Identity : IDeviceIdentityClient {
        public bool IsConfigured => true;
        public Task<DeviceAccessToken?> AcquireAsync(bool interactive, CancellationToken ct) => Task.FromResult<DeviceAccessToken?>(new("synthetic", DateTimeOffset.UtcNow.AddHours(1), "Writer", "account-1"));
        public Task SignOutAsync() => Task.CompletedTask;
    }
    private sealed class EmptySync : IDeviceSyncApi {
        public Task<string> GetOwnerAsync(CancellationToken ct) => Task.FromResult("account-1");
        public Task<SyncChanges> ChangesAsync(string? cursor, CancellationToken ct) => Task.FromResult(new SyncChanges([], "cursor", false));
        public Task<SyncSnapshot> DownloadAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
        public Task<SyncMutationResult> MutateAsync(Guid id, SyncMutation req, CancellationToken ct) => throw new NotSupportedException();
    }
    private sealed class Api : IDeviceAiApi {
        public int Requests; public Guid Section; public bool Instruction, EmptyFix, Capability = true, Echo = true; public long Quota = 10; public string Rewrite = "light";
        public AiActionExecuteRequestDto? Last; public Func<CancellationToken, Task>? Wait;
        public string? ReportJson;
        public Task<AiUsageStatusDto> GetUsageAsync(CancellationToken ct) => Task.FromResult(new AiUsageStatusDto { AiEnabled = true, UiEnabled = true, SupportsDocumentVersionChecks = true, SupportsCanonVersionChecks = Capability, QuotaRemaining = Quota });
        public async Task<AiActionExecuteResponseDto> ExecuteAsync(string key, AiActionExecuteRequestDto req, CancellationToken ct) {
            Requests++; Last = req; if (Wait is not null) await Wait(ct);
            string output = key == "rewrite.selection" ? Rewrite : ReportJson ?? JsonSerializer.Serialize(new { schemaVersion = "1.0", issues = new[] {
                new { severity = "high", type = "character", message = "Suitcase weight contradicts canon", evidence = new { sectionId = Section, quote = "heavy" }, comparisonEvidence = new { sectionId = Section, quote = "The train arrived." }, suggestedFix = EmptyFix ? "" : Instruction ? "Adjust the suitcase weight." : "light", anchor = new { plainTextStart = 15, plainTextLength = 5 } },
                new { severity = "low", type = "timeline", message = "Train timing", evidence = new { sectionId = Section, quote = "The train arrived." }, comparisonEvidence = new { sectionId = Section, quote = "heavy" }, suggestedFix = "The train left.", anchor = new { plainTextStart = 30, plainTextLength = 18 } }
            }});
            return new(Guid.NewGuid(), req.OriginalText, output, "", DateTimeOffset.UtcNow, key, SourceDocumentVersion: Echo ? req.ExpectedDocumentVersion : null,
                SourceCanonVersions: Echo ? req.ExpectedCanonVersions : null);
        }
    }
    private sealed class RejectHttp : HttpMessageHandler { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)); }
    private sealed class Js : IJSRuntime, IJSObjectReference {
        public string? RangeError;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public ValueTask<T> InvokeAsync<T>(string id, object?[]? args) => InvokeAsync<T>(id, default, args);
        public ValueTask<T> InvokeAsync<T>(string id, CancellationToken ct, object?[]? a) {
            ct.ThrowIfCancellationRequested(); if (id == "import") return ValueTask.FromResult((T)(object)this);
            if (id == "consistencyPlainText") return ValueTask.FromResult((T)(object)System.Net.WebUtility.HtmlDecode(System.Text.RegularExpressions.Regex.Replace((string)a![0]!, "<[^>]*>", "")));
            if (id == "validateQualityRange") {
                if (RangeError is not null) throw new JSException(RangeError);
                return ValueTask.FromResult(default(T)!);
            }
            if (id == "previewSafeConsistencyRevision") return ValueTask.FromResult((T)(object)((string)a![0]!).Replace((string)a[3]!, (string)a[4]!, StringComparison.Ordinal));
            throw new NotSupportedException(id);
        }
    }
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}

