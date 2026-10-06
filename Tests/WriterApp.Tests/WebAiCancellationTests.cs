using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using WriterApp.Application.AI;
using WriterApp.Application.Documents;
using WriterApp.Application.Usage;
using PlainTextMapper = WriterApp.Application.State.PlainTextMapper;
using WriterApp.Client.Components.Editor;
using WriterApp.Client.Pages;
using WriterApp.Client.Services;
using WriterApp.Client.State;
using WriterApp.Data;
using WriterApp.Shared;
using WriterApp.UI.Shared;
using Xunit;

namespace WriterApp.Tests;

public sealed partial class DesktopAiWebPersistenceTests
{
    // Completes the remote work before blocking its response, deliberately ignoring
    // cancellation. Assertions cover local protection even when the provider has run.
    private sealed class RequestGate(string suffix, int occurrence = 1)
    {
        public readonly TaskCompletionSource Ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public CancellationToken Token;
        public int Seen;
        public bool Matches(string path) => path.EndsWith(suffix, StringComparison.Ordinal) && ++Seen == occurrence;
    }
    private sealed class CancellationTransport(HttpMessageHandler inner, params RequestGate[] gates) : DelegatingHandler(inner)
    {
        public Func<object?>? Owner;
        public readonly List<(string Path, CancellationToken Token, object? Owner)> Requests = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage message, CancellationToken ct)
        {
            var path = message.RequestUri!.AbsolutePath;
            Requests.Add((path, ct, Owner?.Invoke()));
            RequestGate? gate = null;
            foreach (var candidate in gates) { if (candidate.Matches(path)) gate ??= candidate; }
            var response = await base.SendAsync(message, ct);
            if (gate is not null)
            {
                gate.Token = ct;
                gate.Ready.TrySetResult();
                await gate.Release.Task; // Intentional: model a non-cooperative remote transport.
            }
            return response;
        }
    }
    private sealed class CancellationEditorJs(string html) : IJSRuntime, IJSObjectReference
    {
        public RequestGate? ReadGate;
        public bool TargetedRangeValid = true;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public ValueTask<T> InvokeAsync<T>(string name, object?[]? args) => InvokeAsync<T>(name, default, args);
        public async ValueTask<T> InvokeAsync<T>(string name, CancellationToken ct, object?[]? args)
        {
            ct.ThrowIfCancellationRequested();
            if (name == "tiptapEditor.getPlainText" && ReadGate is { } gate && ++gate.Seen == 1)
            {
                gate.Token = ct; gate.Ready.TrySetResult(); await gate.Release.Task;
            }
            object? value = name switch
            {
                "tiptapEditor.getPlainText" => PlainTextMapper.ToPlainText(html),
                "tiptapEditor.getSelectionText" => PlainTextMapper.ToPlainText(html),
                "tiptapEditor.getSelectionDocRange" => new { from = 1, to = PlainTextMapper.ToPlainText(html).Length + 1 },
                "tiptapEditor.scrollToQualityIssue" or "tiptapEditor.highlightQualityIssue" => true,
                "tiptapEditor.validateTargetedQualityRange" => TargetedRangeValid,
                "tiptapEditor.resolvePlainRangeDetailed" => Range(args![1]!),
                _ => null
            };
            return value is null ? default(T)! : JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        }
        private static object Range(object input)
        {
            var json = JsonSerializer.SerializeToElement(input);
            int from = json.GetProperty("from").GetInt32(), to = json.GetProperty("to").GetInt32();
            return new { resolved = true, from, to, docFrom = from + 1, docTo = to + 1 };
        }
    }
    private static async Task ConfigureCancellationEditor(TranslationFixture f, DocumentEditor editor, CancellationTransport transport, PageDto page)
    {
        editor.Http.Dispose();
        editor.Http = new HttpClient(transport, false) { BaseAddress = new("http://localhost/") };
        editor.Http.DefaultRequestHeaders.Add("X-Test-Owner", "ordinary-author");
        editor.AuthMeStateService = new(editor.Http, new DeletedAccountStateService(), new DuplicateAccountStateService());
        await editor.AuthMeStateService.RefreshAsync();
        var accessProperty = typeof(DocumentEditor).GetProperty("FeatureAccessService", TranslationPrivate)!;
        var loggerType = typeof(NullLogger<>).MakeGenericType(accessProperty.PropertyType);
        accessProperty.SetValue(editor, Activator.CreateInstance(accessProperty.PropertyType,
            [editor.AuthMeStateService, editor.Navigation, loggerType.GetField("Instance")!.GetValue(null)!]));
        TranslationSet(editor, "_activePage", page);
        TranslationSet(editor, "_aiUsageStatus", new AiUsageStatusDto { UiEnabled = true, AiEnabled = true, QuotaRemaining = 1000 });
        TranslationSet(editor, "_onboardingAiRequirementMet", true);
        ((ICollection<string>)TranslationField(editor, "_availableActionKeys")!).Add("continuity.check_section");
        ((ICollection<string>)TranslationField(editor, "_availableActionKeys")!).Add("continuity.apply_fix");
        editor.LayoutStateService = new(editor.JSRuntime);
        editor.CurrentSceneStateService = new();
        editor.GlobalSearchNavigationService = new();
        editor.OnboardingOverlayStateService = new();
        editor.LastOpenedDocumentStateService = new(new CancellationEditorJs(page.Content));
    }
    private static PageEditor CancellationPage(DocumentEditor editor, PageDto page, EditorSaveCoordinator coordinator)
    {
        var result = new PageEditor();
        typeof(PageEditor).GetProperty(nameof(PageEditor.Page))!.SetValue(result, page);
        var js = new CancellationEditorJs(page.Content);
        typeof(PageEditor).GetProperty("JSRuntime", TranslationPrivate)!.SetValue(result, js);
        typeof(PageEditor).GetProperty("Logger", TranslationPrivate)!.SetValue(result, NullLogger<PageEditor>.Instance);
        typeof(PageEditor).GetProperty("SaveCoordinator", TranslationPrivate)!.SetValue(result, coordinator);
        typeof(PageEditor).GetField("_content", TranslationPrivate)!.SetValue(result, page.Content);
        typeof(PageEditor).GetField("_editorInstance", TranslationPrivate)!.SetValue(result, js);
        TranslationSet(editor, "_pageEditor", result);
        return result;
    }
    private static Task CancellationFlow(TranslationRenderer renderer, DocumentEditor editor, string flow)
    {
        if (flow == "quality") return TranslationEvent(renderer, editor, "RunQualityChecksAsync");
        if (flow == "consistency") return TranslationEvent(renderer, editor, "ExecuteContinuityActionAsync", "continuity.check_section", "Checked report", null);
        var action = Activator.CreateInstance(typeof(DocumentEditor).GetNestedType("AiActionOption", BindingFlags.NonPublic)!,
            [flow == "tighten" ? "tighten.selection" : "expand.section", "Writing suggestion", "Revise", flow == "tighten", new Dictionary<string, object?>(), null, true, false, null])!;
        var method = typeof(DocumentEditor).GetMethods(TranslationPrivate).Single(m => m.Name == "OnAiActionSelected" && m.GetParameters().Length == 2);
        return renderer.Dispatcher.InvokeAsync(() => (Task)method.Invoke(editor, [action, false])!);
    }
    private static Task CancelRequest(TranslationRenderer renderer, DocumentEditor editor) => renderer.Dispatcher.InvokeAsync(() =>
        typeof(DocumentEditor).GetMethod("CancelClientAiRequest", TranslationPrivate)!.Invoke(editor, null));
    private static async Task AssertCancellationWritingUnchanged(TranslationFixture f)
    {
        await using var db = new AppDbContext(f.Options);
        var pages = await db.Pages.ToArrayAsync();
        for (int i = 0; i < f.Pages.Length; i++) Assert.Equal(f.Html[i], pages.Single(p => p.Id == f.Pages[i]).Content);
    }

    [Theory]
    [InlineData("writing", "preparation")][InlineData("writing", "generation")]
    [InlineData("quality", "preparation")][InlineData("quality", "generation")]
    [InlineData("consistency", "preparation")][InlineData("consistency", "generation")]
    [InlineData("consistency", "references")]
    [InlineData("writing", "history")][InlineData("consistency", "history")]
    public async Task ClientAiCancellationDiscardsLatePreparationAndGenerationForEveryFlow(string flow, string stage)
    {
        await using var f = new TranslationFixture { Configure = s => s.AddScoped<IQualityCheckService, QualityCheckService>() }; await f.Start();
        using var provider = new CheckedProvider(f); using var editor = CheckedEditor(f, provider);
        string suffix = stage == "preparation" ? $"/web-source/{f.Document}" : stage == "references" ? "/device/refresh" : stage == "history" ? "/history" : flow == "quality" ? "/quality-checks/run" : "/execute";
        var gate = new RequestGate(suffix); using var transport = new CancellationTransport(provider, gate);
        var page = (await f.Http.GetFromJsonAsync<PageDto[]>($"api/sections/{f.Sections[0]}/pages"))![0];
        await ConfigureCancellationEditor(f, editor, transport, page);
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new TranslationRenderer(services, services.GetRequiredService<ILoggerFactory>()); await renderer.Attach(editor);
        var operation = CancellationFlow(renderer, editor, flow);
        await gate.Ready.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.NotNull(TranslationField(editor, "_clientAiRequest")); Assert.True(gate.Token.CanBeCanceled);
        await CancelRequest(renderer, editor);
        Assert.True(gate.Token.IsCancellationRequested); Assert.Null(TranslationField(editor, "_clientAiRequest"));
        Assert.False((bool)TranslationField(editor, "_qualityLoading")!); Assert.False((bool)TranslationField(editor, "_continuityBusy")!);
        Assert.Contains("may still use your quota", TranslationField(editor, "_clientAiRequestMessage")!.ToString());
        gate.Release.TrySetResult(); await operation.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Null(TranslationField(editor, "_pendingAiProposal")); Assert.Null(TranslationField(editor, "_continuityReport"));
        Assert.Null(TranslationField(editor, "_qualityCheckedSource")); Assert.Empty((System.Collections.IDictionary)TranslationField(editor, "_checkedProposals")!);
        Assert.False(editor.AiCommandStatusService.Current.IsInProgress);
        await AssertCancellationWritingUnchanged(f);
    }

    [Theory][InlineData("writing")][InlineData("quality")][InlineData("consistency")]
    public async Task ClientAiCancellationOldCleanupCannotClearAnImmediatelyStartedFreshRequest(string flow)
    {
        await using var f = new TranslationFixture { Configure = s => s.AddScoped<IQualityCheckService, QualityCheckService>() }; await f.Start();
        using var provider = new CheckedProvider(f); using var editor = CheckedEditor(f, provider);
        string suffix = flow == "quality" ? "/quality-checks/run" : "/execute";
        var old = new RequestGate(suffix); var fresh = new RequestGate(suffix, 2);
        using var transport = new CancellationTransport(provider, old, fresh);
        var page = (await f.Http.GetFromJsonAsync<PageDto[]>($"api/sections/{f.Sections[0]}/pages"))![0];
        await ConfigureCancellationEditor(f, editor, transport, page);
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new TranslationRenderer(services, services.GetRequiredService<ILoggerFactory>()); await renderer.Attach(editor);
        var oldOperation = CancellationFlow(renderer, editor, flow); await old.Ready.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await CancelRequest(renderer, editor);
        var newOperation = CancellationFlow(renderer, editor, flow); await fresh.Ready.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var owner = TranslationField(editor, "_clientAiRequest");
        old.Release.TrySetResult(); await oldOperation.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Same(owner, TranslationField(editor, "_clientAiRequest")); Assert.True(editor.AiCommandStatusService.Current.IsInProgress);
        Assert.Null(TranslationField(editor, "_pendingAiProposal")); Assert.Null(TranslationField(editor, "_clientAiRequestMessage"));
        fresh.Release.TrySetResult(); await newOperation.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Null(TranslationField(editor, "_clientAiRequest"));
        Assert.NotNull(TranslationField(editor, flow == "writing" ? "_pendingAiProposal" : flow == "quality" ? "_qualityCheckedSource" : "_continuityReport"));
        await AssertCancellationWritingUnchanged(f);
    }

    [Theory]
    [InlineData("quality", "sentence_length", false)][InlineData("quality", "sentence_length", true)]
    [InlineData("quality", "repeated_words", true)][InlineData("quality", "passive_voice", true)]
    [InlineData("consistency", "", false)][InlineData("consistency", "", true)]
    public async Task ClientAiCancellationTokenOwnsTargetedGenerationAndStrictRetry(string flow, string rule, bool strict)
    {
        await using var f = new TranslationFixture { Configure = s => s.AddScoped<IQualityCheckService, QualityCheckService>() };
        if (flow == "quality") f.Html[0] = "<p>" + TargetedQualityRetryTests.Text(rule == "sentence_length" ? "readability.sentence_length" : "style." + rule).Before + "</p>";
        await f.Start();
        using var provider = new CheckedProvider(f); using var editor = CheckedEditor(f, provider);
        var retry = new RequestGate("/execute", (flow == "quality" ? 1 : 2) + (strict ? 1 : 0)); using var transport = new CancellationTransport(provider, retry);
        var page = (await f.Http.GetFromJsonAsync<PageDto[]>($"api/sections/{f.Sections[0]}/pages"))![0];
        await ConfigureCancellationEditor(f, editor, transport, page);
        transport.Owner = () => TranslationField(editor, "_clientAiRequest");
        await using var coordinator = new EditorSaveCoordinator(new RecoveryDraftService(new CancellationEditorJs(page.Content)), NullLogger<EditorSaveCoordinator>.Instance);
        CancellationPage(editor, page, coordinator);
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new TranslationRenderer(services, services.GetRequiredService<ILoggerFactory>()); await renderer.Attach(editor);
        object issue;
        if (flow == "quality")
        {
            await CancellationFlow(renderer, editor, "quality");
            Assert.NotNull(TranslationField(editor, "_qualityCheckedSource"));
            issue = ((List<PageQualityIssueDto>)TranslationField(editor, "_qualityIssues")!).First(i => i.RuleId.EndsWith(rule, StringComparison.Ordinal));
            TranslationSet(editor, "_targetedStrictRetry", strict);
        }
        else
        {
            provider.ConsistencyOutput = JsonSerializer.Serialize(new { schemaVersion = "1.0", issues = new[] {
                new { severity = "high", type = "character", message = "Resolve contradiction", evidence = new { sectionId = f.Sections[0], quote = PlainTextMapper.ToPlainText(page.Content) }, suggestedFix = "", anchor = new { plainTextStart = 0, plainTextLength = PlainTextMapper.ToPlainText(page.Content).Length } } } });
            await CancellationFlow(renderer, editor, "consistency");
            var report = TranslationField(editor, "_continuityReport")!;
            issue = ((System.Collections.IEnumerable)report.GetType().GetProperty("Issues")!.GetValue(report)!).Cast<object>().Single();
        }
        provider.OutputForCall = (_, _) => flow == "quality" ? PlainTextMapper.ToPlainText(page.Content) : "";
        var operation = TranslationEvent(renderer, editor, flow == "quality" ? "OpenQualityProposalAsync" : "OpenContinuityProposalAsync", issue);
        await retry.Ready.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var generations = transport.Requests.Where(r => r.Path.EndsWith("rewrite.selection/execute")).ToArray();
        Assert.Equal(strict ? 2 : 1, generations.Length); Assert.All(generations, r => Assert.True(r.Token.CanBeCanceled));
        Assert.NotNull(generations[0].Owner); Assert.All(generations, r => Assert.Same(generations[0].Owner, r.Owner));
        await CancelRequest(renderer, editor); Assert.True(retry.Token.IsCancellationRequested);
        retry.Release.TrySetResult(); await operation.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.False((bool)TranslationField(editor, "_isQualityProposalOpen")!); Assert.False((bool)TranslationField(editor, "_isContinuityProposalOpen")!);
        Assert.Null(TranslationField(editor, "_proposalIssue")); Assert.Null(TranslationField(editor, "_pendingContinuityIssue"));
        await AssertCancellationWritingUnchanged(f);
    }

    [Theory][InlineData("tab")][InlineData("section")][InlineData("document")][InlineData("dispose")][InlineData("account")][InlineData("backend")]
    public async Task ClientAiCancellationLifecycleChangesInvalidateInFlightResults(string transition)
    {
        await using var f = new TranslationFixture(); await f.Start(); using var provider = new CheckedProvider(f); using var editor = CheckedEditor(f, provider);
        var gate = new RequestGate("/execute"); using var transport = new CancellationTransport(provider, gate);
        var page = (await f.Http.GetFromJsonAsync<PageDto[]>($"api/sections/{f.Sections[0]}/pages"))![0];
        await ConfigureCancellationEditor(f, editor, transport, page);
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider(); await using var renderer = new TranslationRenderer(services, services.GetRequiredService<ILoggerFactory>()); await renderer.Attach(editor);
        var operation = CancellationFlow(renderer, editor, "consistency"); await gate.Ready.Task.WaitAsync(TimeSpan.FromSeconds(10));
        if (transition == "tab")
        {
            var tab = typeof(DocumentEditor).GetNestedType("ContextTab", BindingFlags.NonPublic)!;
            await TranslationEvent(renderer, editor, "SetContextTabAsync", Enum.Parse(tab, "Annotations"), false, false);
        }
        else if (transition == "section") await TranslationEvent(renderer, editor, "OnSectionSelected", f.Sections[1]);
        else await renderer.Dispatcher.InvokeAsync(() =>
        {
            if (transition == "dispose") ((DocumentEditor)editor).Dispose();
            if (transition == "account") typeof(DocumentEditor).GetMethod("OnAuthMeStateChanged", TranslationPrivate)!.Invoke(editor, null);
            if (transition == "document") typeof(DocumentEditor).GetProperty(nameof(DocumentEditor.DocumentId))!.SetValue(editor, Guid.NewGuid());
            if (transition == "backend") { editor.Http.Dispose(); editor.Http = new HttpClient(transport, false) { BaseAddress = new("http://other-backend/") }; }
        });
        gate.Release.TrySetResult(); await operation.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Null(TranslationField(editor, "_continuityReport")); Assert.Null(TranslationField(editor, "_clientAiRequest"));
        await AssertCancellationWritingUnchanged(f);
    }

    [Fact]
    public async Task ClientAiCancellationDoesNotInterruptTheSaveAlreadyInProgress()
    {
        await using var f = new TranslationFixture(); await f.Start(); using var provider = new CheckedProvider(f); using var editor = CheckedEditor(f, provider);
        using var transport = new CancellationTransport(provider);
        var page = (await f.Http.GetFromJsonAsync<PageDto[]>($"api/sections/{f.Sections[0]}/pages"))![0];
        await ConfigureCancellationEditor(f, editor, transport, page);
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken saveToken = default;
        string writing = page.Content + "<p>Unsaved authored draft 🧭.</p>";
        await using var coordinator = new EditorSaveCoordinator(new RecoveryDraftService(new CancellationEditorJs(writing)), NullLogger<EditorSaveCoordinator>.Instance);
        coordinator.StartSession(RecoveryDraftKey.ForPage(page.Id), page.Content, async (html, ct) =>
        {
            saveToken = ct; ready.TrySetResult(); await release.Task;
            using var response = await f.Http.PutAsJsonAsync($"api/pages/{page.Id}", new { content = html }, ct);
            response.EnsureSuccessStatusCode(); return new EditorSaveResult(true, html, DateTimeOffset.UtcNow);
        });
        var pageEditor = CancellationPage(editor, page with { Content = writing }, coordinator);
        typeof(PageEditor).GetField("_currentPage", TranslationPrivate)!.SetValue(pageEditor, page);
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider(); await using var renderer = new TranslationRenderer(services, services.GetRequiredService<ILoggerFactory>()); await renderer.Attach(editor);
        var operation = CancellationFlow(renderer, editor, "writing"); await ready.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await CancelRequest(renderer, editor);
        Assert.False(saveToken.IsCancellationRequested); Assert.False(saveToken.CanBeCanceled);
        Assert.Null(TranslationField(editor, "_clientAiRequest")); Assert.False(editor.AiCommandStatusService.Current.IsInProgress);
        release.TrySetResult(); await operation.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(0, provider.Calls); Assert.Null(TranslationField(editor, "_pendingAiProposal"));
        Assert.Equal(writing, pageEditor.GetContent()); Assert.False(coordinator.IsDirty);
        await using var db = new AppDbContext(f.Options); Assert.Equal(writing, (await db.Pages.FindAsync(page.Id))!.Content);
    }

    [Fact]
    public async Task ClientAiCancellationOrdinaryErrorsRemainVisibleAndASecondRequestWorks()
    {
        await using var f = new TranslationFixture(); await f.Start(); using var provider = new CheckedProvider(f) { Failure = "capability" }; using var editor = CheckedEditor(f, provider);
        using var transport = new CancellationTransport(provider);
        var page = (await f.Http.GetFromJsonAsync<PageDto[]>($"api/sections/{f.Sections[0]}/pages"))![0];
        await ConfigureCancellationEditor(f, editor, transport, page);
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider(); await using var renderer = new TranslationRenderer(services, services.GetRequiredService<ILoggerFactory>()); await renderer.Attach(editor);
        await CancellationFlow(renderer, editor, "writing");
        var error = TranslationField(editor, "_pendingAiProposal")!;
        Assert.NotNull(error.GetType().GetProperty("ErrorMessage")!.GetValue(error));
        Assert.Null(TranslationField(editor, "_clientAiRequestMessage")); Assert.Null(TranslationField(editor, "_clientAiRequest"));
        provider.Failure = null; await CancellationFlow(renderer, editor, "writing");
        var result = TranslationField(editor, "_pendingAiProposal")!;
        Assert.Null(result.GetType().GetProperty("ErrorMessage")!.GetValue(result)); Assert.NotNull(result.GetType().GetProperty("ProposedText")!.GetValue(result));
        await AssertCancellationWritingUnchanged(f);
    }

    [Fact]
    public async Task ClientAiCancellationOwnQuotaRefreshRetainsAStillAuthorizedCompletedProposal()
    {
        await using var f = new TranslationFixture(); await f.Start(); using var provider = new CheckedProvider(f); using var editor = CheckedEditor(f, provider);
        using var transport = new CancellationTransport(provider);
        var page = (await f.Http.GetFromJsonAsync<PageDto[]>($"api/sections/{f.Sections[0]}/pages"))![0];
        await ConfigureCancellationEditor(f, editor, transport, page);
        var auth = new AuthStateService(editor.Http, NullLogger<AuthStateService>.Instance, new DeletedAccountStateService(), new DuplicateAccountStateService());
        await auth.GetAsync();
        Assert.Equal("ordinary-author", auth.UserId);
        editor.CheckedAi = new WebCheckedAi(editor.Http, auth);
        var changed = (Action)Delegate.CreateDelegate(typeof(Action), editor, typeof(DocumentEditor).GetMethod("OnAuthMeStateChanged", TranslationPrivate)!);
        editor.AuthMeStateService.Changed += changed;
        int generation = (int)TranslationField(editor, "_webTranslationGeneration")!;
        provider.After = () => { provider.TokensUsed++; return Task.CompletedTask; };
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider(); await using var renderer = new TranslationRenderer(services, services.GetRequiredService<ILoggerFactory>()); await renderer.Attach(editor);
        await CancellationFlow(renderer, editor, "writing");
        Assert.Equal(1, editor.AuthMeStateService.AiTokensUsedThisPeriod);
        Assert.NotNull(TranslationField(editor, "_pendingAiProposal"));
        Assert.Equal(generation, TranslationField(editor, "_webTranslationGeneration"));
        Assert.NotEmpty((System.Collections.IDictionary)TranslationField(editor, "_checkedProposals")!);
        editor.AuthMeStateService.Changed -= changed;
        await AssertCancellationWritingUnchanged(f);
    }

    [Fact]
    public async Task ClientAiCancellationLegacyTightenRetryHelperSharesTheOwnerToken()
    {
        await using var f = new TranslationFixture(); await f.Start(); using var provider = new CheckedProvider(f); using var editor = CheckedEditor(f, provider);
        var gate = new RequestGate("/execute", 2); using var transport = new CancellationTransport(provider, gate);
        var page = (await f.Http.GetFromJsonAsync<PageDto[]>($"api/sections/{f.Sections[0]}/pages"))![0];
        await ConfigureCancellationEditor(f, editor, transport, page);
        await using var coordinator = new EditorSaveCoordinator(new RecoveryDraftService(new CancellationEditorJs(page.Content)), NullLogger<EditorSaveCoordinator>.Instance);
        CancellationPage(editor, page, coordinator);
        provider.OutputForCall = (_, _) => PlainTextMapper.ToPlainText(page.Content);
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider(); await using var renderer = new TranslationRenderer(services, services.GetRequiredService<ILoggerFactory>()); await renderer.Attach(editor);
        // Checked outline writing intentionally skips this legacy retry so a proposal
        // continues to name its reviewed result. Cover the remaining helper boundary.
        string plain = PlainTextMapper.ToPlainText(page.Content);
        var action = Activator.CreateInstance(typeof(DocumentEditor).GetNestedType("AiActionOption", BindingFlags.NonPublic)!,
            ["tighten.selection", "Shorten", "Tighten", true, new Dictionary<string, object?>(), null, true, false, null])!;
        var request = new AiActionExecuteRequestDto(f.Document, f.Sections[0], page.Id, 0, plain.Length, plain, plain, null, new());
        Func<CancellationToken, Task> work = async ct =>
        {
            using var first = await CheckedPost(editor, "tighten.selection", request, ct);
            await (Task<string?>)typeof(DocumentEditor).GetMethod("EnsureMeaningfulTightenAsync", TranslationPrivate)!.Invoke(editor, [action, request, plain, plain, ct])!;
        };
        var kind = typeof(DocumentEditor).GetNestedType("ClientAiRequestKind", BindingFlags.NonPublic)!;
        var operation = TranslationEvent(renderer, editor, "RunClientAiRequest", Enum.Parse(kind, "Writing"), "Shorten", work);
        await gate.Ready.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(2, provider.Calls); Assert.True(gate.Token.CanBeCanceled);
        await CancelRequest(renderer, editor); Assert.True(gate.Token.IsCancellationRequested);
        gate.Release.TrySetResult(); await operation.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Null(TranslationField(editor, "_pendingAiProposal")); Assert.Empty((System.Collections.IDictionary)TranslationField(editor, "_checkedProposals")!);
        await AssertCancellationWritingUnchanged(f);
    }

    [Fact]
    public async Task ClientAiCancellationIgnoresLateEditorPreparationAndCanStartAgain()
    {
        await using var f = new TranslationFixture(); await f.Start(); using var provider = new CheckedProvider(f); using var editor = CheckedEditor(f, provider);
        using var transport = new CancellationTransport(provider);
        var page = (await f.Http.GetFromJsonAsync<PageDto[]>($"api/sections/{f.Sections[0]}/pages"))![0];
        await ConfigureCancellationEditor(f, editor, transport, page);
        await using var coordinator = new EditorSaveCoordinator(new RecoveryDraftService(new CancellationEditorJs(page.Content)), NullLogger<EditorSaveCoordinator>.Instance);
        var pageEditor = CancellationPage(editor, page, coordinator);
        var js = (CancellationEditorJs)typeof(PageEditor).GetProperty("JSRuntime", TranslationPrivate)!.GetValue(pageEditor)!;
        var gate = new RequestGate("read"); js.ReadGate = gate;
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider(); await using var renderer = new TranslationRenderer(services, services.GetRequiredService<ILoggerFactory>()); await renderer.Attach(editor);
        var operation = CancellationFlow(renderer, editor, "writing"); await gate.Ready.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await CancelRequest(renderer, editor); await operation.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(gate.Token.IsCancellationRequested); Assert.Equal(0, provider.Calls);
        await CancellationFlow(renderer, editor, "writing");
        var pending = TranslationField(editor, "_pendingAiProposal"); Assert.NotNull(pending);
        gate.Release.TrySetResult();
        Assert.Same(pending, TranslationField(editor, "_pendingAiProposal")); Assert.Equal(page.Content, pageEditor.GetContent());
        await AssertCancellationWritingUnchanged(f);
    }
}
