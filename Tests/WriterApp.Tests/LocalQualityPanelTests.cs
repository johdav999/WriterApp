using System.Reflection;
using AngleSharp.Html.Parser;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using WriterApp.Application.AI;
using WriterApp.Application.Documents;
using WriterApp.Application.Usage;
using WriterApp.Device.Shared.Components;
using WriterApp.Device.Shared.Services;
using WriterApp.Device.Shared.Storage;
using WriterApp.Shared.Sync;
using Xunit;

namespace WriterApp.Tests;

public sealed partial class LocalQualityPanelTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "WriterApp.QualityPanelTests", Guid.NewGuid().ToString("N"));
    private const string Plain = "🧭 Elin carried carried her suitcase. The door was opened by Anna. Later that day, she left.";
    private readonly Api _api = new();
    private readonly DeviceAccountService _account = new(new Identity());
    private readonly DeviceConnectivity _network = new();
    private readonly Components _components = new();
    private readonly Js _js = new();
    private LocalDocumentRepository Repository => new(new FileLocalDocumentStore(_root));
    private LocalAiStore History => new(Path.Combine(_root, "ai"));
    private int _before, _after, _navigated;
    private IReadOnlyList<QualityHighlight> _highlights = [];
    private async Task<(ServiceProvider Services, HtmlRenderer Renderer, Microsoft.AspNetCore.Components.Web.HtmlRendering.HtmlRootComponent Root)> Render(string sourceText = Plain)
    {
        var store = new FileLocalDocumentStore(_root); var repository = new LocalDocumentRepository(store);
        var doc = await repository.CreateImportedAsync("Quality fixture", "<p>" + sourceText + "</p>");
        doc = await store.ApplySyncAsync(doc with { ServerDocumentId = Guid.NewGuid(), ServerVersion = "v1", SyncState = LocalSyncState.Synced }, doc.LocalRevision, default);
        var ai = new DeviceAiService(_api, _account, _network);
        var services = new ServiceCollection().AddLogging().AddSingleton(repository).AddSingleton(_account).AddSingleton(ai)
            .AddSingleton(new DeviceGlossaryService(new(new HttpClient(_glossary) { BaseAddress = new("https://test.invalid/") }),
                new(Path.Combine(_root, "glossary")), repository, _account, _network, new("Test", new("https://test.invalid/"))))
            .AddSingleton(new LocalQualityActions(repository, History, ai))
            .AddSingleton(new DeviceQualityDismissals(new(Path.Combine(_root, "quality-decisions")), new(new HttpClient(new DecisionHandler()) { BaseAddress = new("https://test.invalid/") }),
                repository, _account, _network, new("Test", new("https://test.invalid/"))))
            .AddSingleton(new DeviceSyncEngine(store, new(Path.Combine(_root, "sync")), new EmptySync(), _account, _network, repository, new("Test", new("https://test.invalid/"))))
            .AddSingleton<IJSRuntime>(_js).AddSingleton<IComponentActivator>(_components).BuildServiceProvider();
        var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        var root = await renderer.Dispatcher.InvokeAsync(() => renderer.RenderComponentAsync<LocalQualityPanel>(ParameterView.FromDictionary(new Dictionary<string, object?> {
            [nameof(LocalQualityPanel.DocumentId)] = doc.DocumentId, [nameof(LocalQualityPanel.PageId)] = doc.Sections[0].Pages[0].PageId,
            [nameof(LocalQualityPanel.CurrentHtml)] = doc.Sections[0].Pages[0].Content,
            [nameof(LocalQualityPanel.BeforeWork)] = (Func<Task<bool>>)(() => { _before++; return Task.FromResult(true); }),
            [nameof(LocalQualityPanel.AfterWork)] = (Func<Task>)(() => { _after++; return Task.CompletedTask; }),
            [nameof(LocalQualityPanel.Capture)] = (Func<CancellationToken, Task<QualityEditorSource>>)(async ct => {
                var current = (await repository.LoadAsync(doc.DocumentId, ct))!;
                return new(current, doc.Sections[0].Pages[0].PageId, new(current.Sections[0].Pages[0].Content, sourceText, sourceText, 0, sourceText.Length, 1, sourceText.Length+1, 0));
            }),
            [nameof(LocalQualityPanel.Navigate)] = (Func<string, QualityHighlight, Task<bool>>)((plain, item) => {
                Assert.Equal(sourceText, plain); Assert.Equal(item.ExpectedText, sourceText[item.From..item.To]); _navigated++; return Task.FromResult(true);
            }),
            [nameof(LocalQualityPanel.Highlights)] = (Func<string, IReadOnlyList<QualityHighlight>, string?, Task>)((plain, items, selected) => { _highlights = items; return Task.CompletedTask; })
        })));
        return (services, renderer, root);
    }
    private Task Event(HtmlRenderer renderer, string name, params object?[] args) => renderer.Dispatcher.InvokeAsync(() =>
        ((IHandleEvent)_components.Panel!).HandleEventAsync(new EventCallbackWorkItem((Func<Task>)(() =>
            (Task)typeof(LocalQualityPanel).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(_components.Panel, args)!)), null));
    private LocalQualityAnalysis Analysis => (LocalQualityAnalysis)typeof(LocalQualityPanel).GetField("_analysis", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_components.Panel)!;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnsafeTargetedReviewShowsOnlyActionableErrorAndLeavesWritingAndHistoryUnchanged(bool previewFailure)
    {
        await _account.SignInAsync();
        var (services, renderer, root) = await Render();
        await using var ownedServices = services; await using var ownedRenderer = renderer;
        await Event(renderer, "Analyze"); var a = Analysis;
        var issue = a.Issues.Single(i => QualityIssueCapabilities.IsPassiveVoiceIssue(i));
        const string message = "This changed wording spans mixed formatting. Review a smaller passage or revise it manually to preserve its formatting.";
        _js.FailureMethod = previewFailure ? "previewTargetedQualityRevision" : "validateTargetedQualityRange";
        _js.Error = message + "\r\nError: " + message + "\r\n    at Module.xb (https://0.0.0.1/editor/device-editor.js:16711:17)";
        await Event(renderer, "Review", issue.IssueKey);
        var html = new HtmlParser().ParseDocument(await renderer.Dispatcher.InvokeAsync(root.ToHtmlString));
        var alert = Assert.Single(html.QuerySelectorAll("[role=alert]"));
        Assert.StartsWith(message, alert.TextContent.Trim());
        Assert.Contains("Writing is unchanged", alert.TextContent);
        Assert.Equal(issue.IssueKey, alert.Closest("li")!.GetAttribute("data-quality-issue"));
        Assert.DoesNotContain("Error:", html.Body!.TextContent);
        Assert.DoesNotContain("device-editor.js", html.Body.TextContent);
        Assert.DoesNotContain("Approve &amp; apply", html.DocumentElement.OuterHtml);
        Assert.Equal(previewFailure ? 1 : 0, _api.Requests);
        Assert.Equal(LocalDocumentCodec.Encode(a.Source), LocalDocumentCodec.Encode((await Repository.LoadAsync(a.Source.DocumentId))!));
        Assert.Empty(await History.HistoryAsync(a.Source.DocumentId));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualPanelChecksOfflineFiltersJumpsPreviewsDismissesAndAppliesOnlyAfterApproval(bool selection)
    {
        _network.SetOnline(false);
        var (services, renderer, root) = await Render();
        await using var ownedServices = services; await using var ownedRenderer = renderer;
        if (selection) typeof(LocalQualityPanel).GetField("_selection", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(_components.Panel, true);
        await Event(renderer, "Analyze");
        var a = Analysis; var repeated = a.Issues.Single(i => QualityIssueCapabilities.IsRepeatedWordIssue(i));
        var html = await renderer.Dispatcher.InvokeAsync(root.ToHtmlString);
        Assert.Contains("completed locally", html); Assert.Contains("Information only", html);
        Assert.Contains(selection ? "Checked selection" : "Checked page", html); Assert.Equal(0, _api.Requests);
        Assert.NotEmpty(_highlights);
        var timeline = a.Issues.Single(i => i.RuleId == "consistency.timeline_hint");
        await Event(renderer, "Dismiss", timeline.IssueKey);
        Assert.DoesNotContain(_highlights, h => h.IssueKey == timeline.IssueKey);
        Assert.Equal(0, _api.Requests); Assert.Empty(await History.HistoryAsync(a.Source.DocumentId));
        await Event(renderer, "Analyze"); // Exact-source dismissal now survives reruns.
        Assert.DoesNotContain(_highlights, h => h.IssueKey == timeline.IssueKey);
        await Event(renderer, "RuleChanged", repeated.RuleId); Assert.Single(_highlights);
        await Event(renderer, "SeverityChanged", "warning"); Assert.Empty(_highlights);
        await Event(renderer, "SeverityChanged", "all"); await Event(renderer, "Select", repeated.IssueKey);
        await Event(renderer, "Jump", repeated.IssueKey); Assert.Equal(1, _navigated);
        string before = (await Repository.LoadAsync(a.Source.DocumentId))!.Sections[0].Pages[0].Content;
        await Event(renderer, "RuleChanged", "all");
        await Event(renderer, "Review", repeated.IssueKey);
        html = await renderer.Dispatcher.InvokeAsync(root.ToHtmlString);
        Assert.Contains("Approve &amp; apply quality fix", html); Assert.Contains("No AI request was needed", html);
        var rendered = new HtmlParser().ParseDocument(html);
        var review = rendered.QuerySelector("section[aria-label='Review targeted quality change']");
        Assert.NotNull(review);
        Assert.Equal(repeated.IssueKey, review!.Closest("li")?.GetAttribute("data-quality-issue"));
        Assert.Contains("quality-selected", review.Closest("li")!.ClassList);
        Assert.Equal(before, (await Repository.LoadAsync(a.Source.DocumentId))!.Sections[0].Pages[0].Content);
        Assert.Empty(await History.HistoryAsync(a.Source.DocumentId));
        await renderer.Dispatcher.InvokeAsync(() => typeof(LocalQualityPanel).GetMethod("DismissPreview", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(_components.Panel, null));
        await Event(renderer, "Review", repeated.IssueKey);
        await Event(renderer, "Apply");
        var saved = (await Repository.LoadAsync(a.Source.DocumentId))!;
        Assert.DoesNotContain("carried carried", saved.Sections[0].Pages[0].Content);
        Assert.Equal("Applied", Assert.Single(await History.HistoryAsync(a.Source.DocumentId)).Status);
        Assert.Equal(0, _api.Requests); Assert.Equal(_before, _after);
        var done = await renderer.Dispatcher.InvokeAsync(root.ToHtmlString);
        Assert.Contains("Open History to undo", done); Assert.DoesNotContain("Approve &amp; apply", done);
        Assert.Empty(_highlights);
        var evidence = Environment.GetEnvironmentVariable("WRITERAPP_P04_EVIDENCE");
        if (evidence is not null && !selection) await File.WriteAllTextAsync(Path.Combine(evidence, "quality-review.html"),
            "<!doctype html><html><head><meta charset='utf-8'><title>Quality component fixture</title><style>body{max-width:800px;margin:2rem auto;padding:1rem;font:16px system-ui}button,select{margin:.4rem;padding:.5rem}blockquote{border-left:3px solid #aaa;padding-left:1rem}li{margin:1rem 0}</style></head><body>" + html + "</body></html>");
    }
    [Fact]
    public async Task RepeatedWordReviewAcceptsSafeReductionAndErrorStaysBesideClickedFinding()
    {
        const string text = "The clock rang while another clock answered the clock in the hall.";
        await _account.SignInAsync();
        var (services, renderer, root) = await Render(text);
        await using var ownedServices = services; await using var ownedRenderer = renderer;
        await Event(renderer, "Analyze"); var a = Analysis;
        Assert.DoesNotContain(a.Issues, i => i.AnchorText?.Equals("the", StringComparison.OrdinalIgnoreCase) == true);
        var issue = a.Issues.First(i => i.AnchorText == "clock");
        _api.Output = "The clock sounded while another clock answered the clock in the hall.";
        await Event(renderer, "Review", issue.IssueKey);
        var rejected = new HtmlParser().ParseDocument(await renderer.Dispatcher.InvokeAsync(root.ToHtmlString));
        var alert = Assert.Single(rejected.QuerySelectorAll("[role=alert]"));
        Assert.Equal(issue.IssueKey, alert.Closest("li")!.GetAttribute("data-quality-issue"));
        Assert.Contains("Writing is unchanged", alert.TextContent);
        Assert.DoesNotContain("Approve &amp; apply", rejected.DocumentElement.OuterHtml);
        Assert.Equal(a.Html, (await Repository.LoadAsync(a.Source.DocumentId))!.Sections[0].Pages[0].Content);
        var evidence = Environment.GetEnvironmentVariable("WRITERAPP_TARGETED_QUALITY_EVIDENCE");
        if (evidence is not null) await File.WriteAllTextAsync(Path.Combine(evidence, "rejected.html"), rejected.DocumentElement.OuterHtml);
        _api.Output = "The clock rang while another clock answered the chime in the hall.";
        await Event(renderer, "Review", issue.IssueKey);
        var reviewed = new HtmlParser().ParseDocument(await renderer.Dispatcher.InvokeAsync(root.ToHtmlString));
        Assert.Empty(reviewed.QuerySelectorAll("[role=alert]"));
        var review = reviewed.QuerySelector("section[aria-label='Review targeted quality change']")!;
        Assert.Equal(issue.IssueKey, review.Closest("li")!.GetAttribute("data-quality-issue"));
        Assert.Contains("Approve & apply", review.TextContent);
        if (evidence is not null) await File.WriteAllTextAsync(Path.Combine(evidence, "review.html"), reviewed.DocumentElement.OuterHtml);
        Assert.Equal(a.Html, (await Repository.LoadAsync(a.Source.DocumentId))!.Sections[0].Pages[0].Content);
        Assert.Empty(await History.HistoryAsync(a.Source.DocumentId));
        await Event(renderer, "Apply");
        Assert.Equal("<p>" + _api.Output + "</p>", (await Repository.LoadAsync(a.Source.DocumentId))!.Sections[0].Pages[0].Content);
        Assert.Equal("Applied", Assert.Single(await History.HistoryAsync(a.Source.DocumentId)).Status);
    }

    [Fact]
    public async Task LongParagraphReviewKeepsApprovalWithItsFindingAndSavesTheReviewedSplitOffline()
    {
        const string text = "The train gently pulled into Vinterhamn just as the sun was setting, the platform bathed in a soft, fading glow. Elin stepped off, gripping her suitcase a little tighter—it suddenly felt heavier than before. Nearby, a man leaned casually against the station lamp post, his dark wool coat wrapped snugly around him, hands tucked deep into his pockets. At first, his face seemed unfamiliar, but as the train slowed, his warm, friendly smile shone through the dim light. A thin frost dusted the wooden benches, cold and crisp against the lingering warmth of the day. The platform stretched out ahead, catching the last glimmers of orange in the sky. It had been eleven years since she’d last been here. At the far end, the old station clock ticked steadily, its hands stubbornly three minutes slow, just like always. Some things, she thought, really never change.";
        _network.SetOnline(false);
        var (services, renderer, root) = await Render(text);
        await using var ownedServices = services; await using var ownedRenderer = renderer;
        await Event(renderer, "Analyze"); var a = Analysis;
        var issue = Assert.Single(a.Issues, i => i.RuleId == "readability.paragraph_length");
        Assert.True(a.Issues.Count > 2);
        await Event(renderer, "Review", issue.IssueKey);
        string html = await renderer.Dispatcher.InvokeAsync(root.ToHtmlString);
        var rendered = new HtmlParser().ParseDocument(html);
        var review = rendered.QuerySelector("section[aria-label='Review targeted quality change']")!;
        var finding = review.Closest("li")!;
        Assert.Equal(issue.IssueKey, finding.GetAttribute("data-quality-issue"));
        Assert.Equal("-1", review.GetAttribute("tabindex"));
        var approve = Assert.Single(review.QuerySelectorAll("button"), b => b.TextContent.Contains("Approve & apply"));
        Assert.False(approve.HasAttribute("disabled"));
        Assert.True(review.InnerHtml.IndexOf("Approve &amp; apply", StringComparison.Ordinal) < review.InnerHtml.IndexOf("ai-preview-columns", StringComparison.Ordinal));
        Assert.NotNull(finding.NextElementSibling); // Other findings follow the inline review.
        Assert.Empty(await History.HistoryAsync(a.Source.DocumentId));
        Assert.Equal(a.Html, (await Repository.LoadAsync(a.Source.DocumentId))!.Sections[0].Pages[0].Content);
        var preview = (LocalQualityPreview)typeof(LocalQualityPanel).GetField("_preview", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_components.Panel)!;
        Assert.Contains("\n\n", preview.Proposed);
        Assert.Equal(text, preview.Proposed.Replace("\n\n", " ", StringComparison.Ordinal));
        await Event(renderer, "Apply");
        Assert.Equal(preview.AfterHtml, (await Repository.LoadAsync(a.Source.DocumentId))!.Sections[0].Pages[0].Content);
        Assert.Equal("Applied", Assert.Single(await History.HistoryAsync(a.Source.DocumentId)).Status);
        Assert.Equal(0, _api.Requests);
        var evidence = Environment.GetEnvironmentVariable("WRITERAPP_QUALITY_EVIDENCE");
        if (evidence is not null) await File.WriteAllTextAsync(Path.Combine(evidence, "paragraph-review.html"), html);
    }
    [Fact]
    public async Task EditedSourceInvalidatesPanelAndStaleReviewNeverSavesWriting()
    {
        var (services, renderer, root) = await Render(); await using var ownedServices = services; await using var ownedRenderer = renderer;
        await Event(renderer, "Analyze"); var a = Analysis;
        var key = a.Issues.First(i => QualityIssueCapabilities.IsRepeatedWordIssue(i)).IssueKey;
        await Event(renderer, "Review", key);
        var edited = await Repository.SaveAsync(a.Source with { Sections = a.Source.Sections.Select(s => s with {
            Pages = s.Pages.Select(p => p with { Content = p.Content + "<p>Later writing</p>" }).ToArray() }).ToArray() });
        await Event(renderer, "Apply");
        Assert.Contains("changed", await renderer.Dispatcher.InvokeAsync(root.ToHtmlString));
        Assert.Equal(LocalDocumentCodec.Encode(edited), LocalDocumentCodec.Encode((await Repository.LoadAsync(edited.DocumentId))!));
        Assert.Empty(await History.HistoryAsync(edited.DocumentId));
        await renderer.Dispatcher.InvokeAsync(() => _components.Panel!.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string,object?> {
            [nameof(LocalQualityPanel.CurrentHtml)] = edited.Sections[0].Pages[0].Content })));
        var html = await renderer.Dispatcher.InvokeAsync(root.ToHtmlString);
        Assert.Contains("Run quality checks again", html); Assert.DoesNotContain("Approve &amp; apply", html); Assert.Empty(_highlights);
    }
    [Theory]
    [InlineData("offline")]
    [InlineData("auth")]
    [InlineData("quota")]
    [InlineData("invalid")]
    public async Task GeneratedFixErrorsAreActionableAndPreserveSource(string failure)
    {
        if (failure != "auth") await _account.SignInAsync();
        if (failure == "offline") _network.SetOnline(false);
        if (failure == "quota") _api.Quota = 0;
        if (failure == "invalid") _api.Output = "{\"analysis\":\"Remove passive voice\"}";
        var (services, renderer, root) = await Render(); await using var ownedServices = services; await using var ownedRenderer = renderer;
        await Event(renderer, "Analyze"); var a = Analysis;
        await Event(renderer, "Review", a.Issues.Single(i => QualityIssueCapabilities.IsPassiveVoiceIssue(i)).IssueKey);
        var html = await renderer.Dispatcher.InvokeAsync(root.ToHtmlString);
        Assert.NotEmpty(new HtmlParser().ParseDocument(html).QuerySelectorAll("[role=alert]"));
        Assert.DoesNotContain("Approve &amp; apply", html);
        Assert.Equal(LocalDocumentCodec.Encode(a.Source), LocalDocumentCodec.Encode((await Repository.LoadAsync(a.Source.DocumentId))!));
        Assert.Empty(await History.HistoryAsync(a.Source.DocumentId));
    }
    [Fact]
    public async Task GeneratedPreviewSurvivesTokenRenewalButAccountSwitchRemovesIt()
    {
        await _account.SignInAsync(); var (services, renderer, root) = await Render();
        await using var ownedServices = services; await using var ownedRenderer = renderer;
        await Event(renderer, "Analyze"); var a = Analysis;
        await Event(renderer, "Review", a.Issues.Single(i => QualityIssueCapabilities.IsPassiveVoiceIssue(i)).IssueKey);
        Assert.Contains("AI-generated revision", await renderer.Dispatcher.InvokeAsync(root.ToHtmlString));
        await renderer.Dispatcher.InvokeAsync(() => _account.GetTokenAsync(default));
        Assert.Contains("Approve &amp; apply", await renderer.Dispatcher.InvokeAsync(root.ToHtmlString));
        await renderer.Dispatcher.InvokeAsync(() => _account.SignOutAsync());
        Assert.DoesNotContain("Approve &amp; apply", await renderer.Dispatcher.InvokeAsync(root.ToHtmlString));
        Assert.Equal(1, _api.Requests); Assert.Empty(await History.HistoryAsync(a.Source.DocumentId));
    }
    [Fact]
    public async Task GeneratedFixAppliesOnlyItsBoundPassageAndDurableHistoryRetainsOriginal()
    {
        await _account.SignInAsync(); var (services, renderer, root) = await Render();
        await using var ownedServices = services; await using var ownedRenderer = renderer;
        await Event(renderer, "Analyze"); var a = Analysis;
        await Event(renderer, "Review", a.Issues.Single(i => QualityIssueCapabilities.IsPassiveVoiceIssue(i)).IssueKey);
        Assert.Empty(await History.HistoryAsync(a.Source.DocumentId));
        await Event(renderer, "Apply");
        var after = (await Repository.LoadAsync(a.Source.DocumentId))!;
        Assert.Equal(a.Html.Replace("The door was opened by Anna.", "Anna opened the door.", StringComparison.Ordinal), after.Sections[0].Pages[0].Content);
        Assert.Equal("Applied", Assert.Single(await History.HistoryAsync(a.Source.DocumentId)).Status);
        Assert.Contains("applied and saved", await renderer.Dispatcher.InvokeAsync(root.ToHtmlString));
    }
    [Fact]
    public async Task CancelledGeneratedRequestShowsLoadingThenReturnsWithoutPreviewOrMutation()
    {
        await _account.SignInAsync(); var (services, renderer, root) = await Render();
        await using var ownedServices = services; await using var ownedRenderer = renderer;
        await Event(renderer, "Analyze"); var a = Analysis;
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _api.Wait = async ct => { started.SetResult(); await Task.Delay(Timeout.Infinite, ct); };
        Task? run = null;
        await renderer.Dispatcher.InvokeAsync(() => { run = Event(renderer, "Review", a.Issues.Single(i => QualityIssueCapabilities.IsPassiveVoiceIssue(i)).IssueKey); });
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Contains("Cancel quality request", await renderer.Dispatcher.InvokeAsync(root.ToHtmlString));
        await renderer.Dispatcher.InvokeAsync(() => ((CancellationTokenSource)typeof(LocalQualityPanel).GetField("_cancel", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(_components.Panel)!).Cancel());
        await run!;
        var html = await renderer.Dispatcher.InvokeAsync(root.ToHtmlString);
        Assert.Contains("request canceled", html); Assert.DoesNotContain("Approve &amp; apply", html);
        Assert.Equal(LocalDocumentCodec.Encode(a.Source), LocalDocumentCodec.Encode((await Repository.LoadAsync(a.Source.DocumentId))!));
    }
    private sealed class Components : IComponentActivator
    {
        public LocalQualityPanel? Panel;
        public IComponent CreateInstance(Type type) { var value = (IComponent)Activator.CreateInstance(type)!; if (value is LocalQualityPanel panel) Panel = panel; return value; }
    }
    private sealed class Identity : IDeviceIdentityClient
    {
        public bool IsConfigured => true;
        public Task<DeviceAccessToken?> AcquireAsync(bool interactive, CancellationToken ct) => Task.FromResult<DeviceAccessToken?>(new("synthetic", DateTimeOffset.UtcNow.AddHours(1), "Writer", "account-1"));
        public Task SignOutAsync() => Task.CompletedTask;
    }
    private sealed class EmptySync : IDeviceSyncApi
    {
        public Task<string> GetOwnerAsync(CancellationToken ct) => Task.FromResult("account-1");
        public Task<SyncChanges> ChangesAsync(string? cursor, CancellationToken ct) => Task.FromResult(new SyncChanges([], "cursor", false));
        public Task<SyncSnapshot> DownloadAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
        public Task<SyncMutationResult> MutateAsync(Guid id, SyncMutation request, CancellationToken ct) => throw new NotSupportedException();
    }
    private sealed class Api : IDeviceAiApi
    {
        public int Requests; public long Quota = 10; public string Output = "Anna opened the door.";
        public Func<int, AiActionExecuteRequestDto, string>? OutputForCall;
        public readonly List<AiActionExecuteRequestDto> Inputs = [];
        public Func<CancellationToken, Task>? Wait;
        public Task<AiUsageStatusDto> GetUsageAsync(CancellationToken ct) => Task.FromResult(new AiUsageStatusDto { AiEnabled = true, UiEnabled = true, QuotaRemaining = Quota, SupportsDocumentVersionChecks = true });
        public async Task<AiActionExecuteResponseDto> ExecuteAsync(string key, AiActionExecuteRequestDto request, CancellationToken ct)
        { Requests++; Inputs.Add(request); if (Wait is not null) await Wait(ct); return new(Guid.NewGuid(), request.OriginalText, OutputForCall?.Invoke(Requests, request) ?? Output, "", DateTimeOffset.UtcNow, key, SourceDocumentVersion: request.ExpectedDocumentVersion); }
    }
    private sealed class Js : IJSRuntime, IJSObjectReference
    {
        public string? FailureMethod, Error;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => InvokeAsync<TValue>(identifier, default, args);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken ct, object?[]? args)
        {
            ct.ThrowIfCancellationRequested();
            if (identifier == FailureMethod) throw new JSException(Error!);
            if (identifier == "import") return ValueTask.FromResult((TValue)(object)this);
            if (identifier == "validateTargetedQualityRange") return ValueTask.FromResult(default(TValue)!);
            if (identifier == "previewTargetedQualityRevision") return ValueTask.FromResult((TValue)(object)((string)args![0]!).Replace((string)args[4]!, (string)args[5]!, StringComparison.Ordinal));
            throw new NotSupportedException(identifier);
        }
    }
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
