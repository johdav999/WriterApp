using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using WriterApp.Application.Documents;
using WriterApp.Client.Components.Editor;
using WriterApp.Client.Pages;
using WriterApp.Client.Services;
using WriterApp.Data;
using WriterApp.Shared;
using Xunit;

namespace WriterApp.Tests;

#pragma warning disable BL0006 // Framework renderer seam exercises the production handlers.

public sealed partial class DesktopAiWebPersistenceTests
{
    private sealed class ConsistencyPageEditor : PageEditor
    {
        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder) { }
        protected override void OnInitialized() { }
        protected override Task OnInitializedAsync() => Task.CompletedTask;
        protected override Task OnParametersSetAsync() => Task.CompletedTask;
        protected override Task OnAfterRenderAsync(bool firstRender) => Task.CompletedTask;
    }
    private sealed class ConsistencyPageFixture : IAsyncDisposable
    {
        public readonly TranslationFixture Data = new();
        public CheckedProvider Provider = null!;
        public TranslationEditor Editor = null!;
        public TranslationRenderer Renderer = null!;
        private ServiceProvider _services = null!;
        private CancellationTransport _transport = null!;
        private readonly List<EditorSaveCoordinator> _coordinators = [];
        public readonly PageJs Js = new();
        public PageEditor PageEditor = null!;
        public readonly PageSaveTransport Saves;
        public object Issue => ((System.Collections.IEnumerable)TranslationField(Editor, "_continuityReport")!.GetType()
            .GetProperty("Issues")!.GetValue(TranslationField(Editor, "_continuityReport"))!).Cast<object>().Single();
        public PageDto Active => (PageDto)TranslationField(Editor, "_activePage")!;
        public string? Status => TranslationField(Editor, "_continuityStatus")?.ToString();
        private ConsistencyPageFixture() { Saves = new(); }
        public static async Task<ConsistencyPageFixture> Start(bool duplicate = false, RequestGate? gate = null, string? styleHtml = null)
        {
            var h = new ConsistencyPageFixture(); var f = h.Data;
            f.Html[0] = duplicate ? "<p>The clock stood beside the clock.</p>" : "<p>🧭 First page stays intact.</p>";
            if (styleHtml is not null) f.Html[0] = styleHtml;
            f.Html[1] = "<p>The <strong>clock</strong> stood beside the clock.</p>";
            await f.Start(); h.Provider = new(f); h.Editor = CheckedEditor(f, h.Provider);
            h.Saves.InnerHandler = h.Provider;
            var transport = new CancellationTransport(h.Saves, gate is null ? [] : [gate]); h._transport = transport;
            // Configure the same signed-in feature/usage seams as the cancellation regressions.
            var pages = (await f.Http.GetFromJsonAsync<PageDto[]>($"api/sections/{f.Sections[0]}/pages"))!;
            await ConfigureCancellationEditor(f, h.Editor, transport, pages[0]);
            var sections = (await f.Http.GetFromJsonAsync<SectionDto[]>($"api/documents/{f.Document}/sections"))!;
            foreach (var section in sections) ((ICollection<SectionDto>)TranslationField(h.Editor, "_sections")!).Add(section);
            var bySection = (Dictionary<Guid, List<PageDto>>)TranslationField(h.Editor, "_pagesBySection")!;
            bySection[f.Sections[0]] = pages.ToList();
            var tab = typeof(DocumentEditor).GetNestedType("ContextTab", BindingFlags.NonPublic)!;
            TranslationSet(h.Editor, "_activeContextTab", Enum.Parse(tab, "Continuity"));
            h.Editor.ConsistencyEditorReady = async id => {
                var page = (PageDto)TranslationField(h.Editor, "_activePage")!;
                Assert.Equal(id, page.Id); await h.SetEditor(page);
            };
            h._services = new ServiceCollection().AddLogging().BuildServiceProvider();
            h.Renderer = new(h._services, h._services.GetRequiredService<ILoggerFactory>()); await h.Renderer.Attach(h.Editor);
            await h.SetEditor(pages[0]);
            int offset = WriterApp.Client.State.PlainTextMapper.ToPlainText(f.Html[0]).Length + 2;
            h.Provider.ConsistencyOutput = JsonSerializer.Serialize(new { schemaVersion = "1.0", issues = new[] {
                new { severity = "high", type = "place", message = "The two clocks contradict the established room.",
                    evidence = new { sectionId = f.Sections[0], quote = "The clock stood beside the clock." },
                    comparisonEvidence = new { sectionId = f.Sections[1], quote = "Other section" },
                    fixKind = "replace", suggestedFix = "The clock stood beside the chime.", anchor = new { plainTextStart = offset, plainTextLength = 33 } }
            } });
            return h;
        }
        public Task Event(string name, params object?[] args) => TranslationEvent(Renderer, Editor, name, args);
        public Task Check() => Event("ExecuteContinuityActionAsync", "continuity.check_section", "Checked report", null);
        public async Task SetEditor(PageDto page)
        {
            var coordinator = new EditorSaveCoordinator(new RecoveryDraftService(Js), NullLogger<EditorSaveCoordinator>.Instance); _coordinators.Add(coordinator);
            var editor = new ConsistencyPageEditor(); PageEditor = editor; Js.Editor = editor; Js.Html = page.Content;
            typeof(PageEditor).GetProperty(nameof(PageEditor.Page))!.SetValue(editor, page);
            foreach (var (name, value) in new (string, object)[] { ("JSRuntime", Js), ("Http", Editor.Http), ("Logger", NullLogger<PageEditor>.Instance), ("SaveCoordinator", coordinator) })
                typeof(PageEditor).GetProperty(name, TranslationPrivate)!.SetValue(editor, value);
            typeof(PageEditor).GetField("_currentPage", TranslationPrivate)!.SetValue(editor, page);
            typeof(PageEditor).GetField("_content", TranslationPrivate)!.SetValue(editor, page.Content);
            typeof(PageEditor).GetField("_lastLoadedHash", TranslationPrivate)!.SetValue(editor,
                typeof(PageEditor).GetMethod("ComputeShortHash", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [page.Content]));
            typeof(PageEditor).GetField("_editorInstance", TranslationPrivate)!.SetValue(editor, Js);
            typeof(PageEditor).GetField("_commandsModule", TranslationPrivate)!.SetValue(editor, Js);
            TranslationSet(Editor, "_pageEditor", editor);
            coordinator.StartSession(RecoveryDraftKey.ForPage(page.Id), page.Content,
                (html, ct) => (Task<EditorSaveResult>)typeof(PageEditor).GetMethod("SaveContentAsync", TranslationPrivate)!.Invoke(editor, [html, ct])!);
            await Renderer.Attach(editor);
        }
        public async ValueTask DisposeAsync()
        {
            foreach (var coordinator in _coordinators) await coordinator.DisposeAsync();
            await Renderer.DisposeAsync(); await _services.DisposeAsync(); Editor.Dispose(); _transport.Dispose(); await Data.DisposeAsync();
        }
    }
    private sealed class PageSaveTransport : DelegatingHandler
    {
        public bool Fail; public Func<Task>? BeforeSave;
        public List<WebAiSource?> Sources = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (request.Method == HttpMethod.Put && request.RequestUri!.AbsolutePath.StartsWith("/api/pages/"))
            {
                Sources.Add(request.Headers.TryGetValues("X-WriterApp-AI-Source", out var values) ? JsonSerializer.Deserialize<WebAiSource>(values.Single()) : null);
                if (BeforeSave is not null) await BeforeSave();
                if (Fail) return new(HttpStatusCode.ServiceUnavailable);
            }
            return await base.SendAsync(request, ct);
        }
    }
    private sealed class PageJs : IJSRuntime, IJSObjectReference
    {
        public PageEditor Editor = null!; public string Html = ""; public bool RejectRichSpan;
        public int StyleFrom, StyleLength; public bool RejectStyle;
        public RequestGate? StylePreviewGate;
        public readonly Dictionary<string, string> Storage = [];
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public ValueTask<T> InvokeAsync<T>(string name, object?[]? args) => InvokeAsync<T>(name, default, args);
        public async ValueTask<T> InvokeAsync<T>(string name, CancellationToken ct, object?[]? args)
        {
            ct.ThrowIfCancellationRequested(); object? value = null;
            if (name == "localStorage.setItem") Storage[(string)args![0]!] = (string)args[1]!;
            if (name == "localStorage.removeItem") Storage.Remove((string)args![0]!);
            if (name == "localStorage.getItem") value = Storage.GetValueOrDefault((string)args![0]!);
            if (name == "tiptapEditor.getPlainText") value = WriterApp.Client.State.PlainTextMapper.ToPlainText(Html);
            if (name == "appendParagraph") { Html += "<p>" + System.Net.WebUtility.HtmlEncode((string)args![1]!) + "</p>"; typeof(PageEditor).GetField("_content", TranslationPrivate)!.SetValue(Editor, Html); }
            if (name == "tiptapEditor.captureStyleQuality") {
                string plain = WriterApp.Client.State.PlainTextMapper.ToPlainText(Html);
                bool page = (bool)args![1]!;
                if (!page && StyleLength == 0) throw new JSException("Select current writing before requesting a selection review.");
                value = new { html = Html, plain, from = page ? 0 : StyleFrom, text = page ? plain : plain.Substring(StyleFrom, StyleLength) };
            }
            if (name == "tiptapEditor.previewStyleQuality") {
                if (StylePreviewGate is { } gate && ++gate.Seen == 1) {
                    gate.Token = ct; gate.Ready.TrySetResult(); await gate.Release.Task; ct.ThrowIfCancellationRequested();
                }
                if (RejectStyle) throw new JSException("This changed wording spans mixed formatting. Review a smaller passage.");
                value = true;
            }
            if (name == "tiptapEditor.applyStyleQuality") {
                var edits = JsonSerializer.SerializeToElement(args![2], new JsonSerializerOptions(JsonSerializerDefaults.Web));
                foreach (var edit in edits.EnumerateArray()) {
                    string before = edit.GetProperty("original").GetString()!, after = edit.GetProperty("replacement").GetString()!;
                    int prefix = 0, suffix = 0;
                    while (prefix < before.Length && prefix < after.Length && before[prefix] == after[prefix]) prefix++;
                    while (suffix < before.Length - prefix && suffix < after.Length - prefix && before[^(suffix + 1)] == after[^(suffix + 1)]) suffix++;
                    string old = before.Substring(prefix, before.Length - prefix - suffix), next = after.Substring(prefix, after.Length - prefix - suffix);
                    int at = Html.LastIndexOf(old, StringComparison.Ordinal);
                    Html = Html.Remove(at, old.Length).Insert(at, next);
                }
                typeof(PageEditor).GetField("_content", TranslationPrivate)!.SetValue(Editor, Html); value = Html;
            }
            if (name == "tiptapEditor.resolvePlainRangeDetailed") {
                var input = JsonSerializer.SerializeToElement(args![1]); int from = input.GetProperty("from").GetInt32(), to = input.GetProperty("to").GetInt32();
                string plain = WriterApp.Client.State.PlainTextMapper.ToPlainText(Html);
                value = new { resolved = from >= 0 && to <= plain.Length && plain[from..to] == input.GetProperty("anchorText").GetString(), from, to, docFrom = from + 1, docTo = to + 1 };
            }
            if (name == "tiptapEditor.applyQualityIssueFixDetailed") {
                if (RejectRichSpan) value = new { applied = false, changed = false, reason = "mixed-changed-formatting", source = "range" };
                else {
                    Html = Html.Replace("beside the clock", "beside the chime", StringComparison.Ordinal);
                    typeof(PageEditor).GetField("_content", TranslationPrivate)!.SetValue(Editor, Html);
                    value = new { applied = true, changed = true, reason = "applied", source = "range" };
                }
            }
            return value is null ? default(T)! : JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        }
    }
    [Fact]
    public async Task ClientConsistencyPageTwoCheckJumpReviewCheckedApplyAndRestartPreserveOtherPagesAndRichSource()
    {
        await using var h = await ConsistencyPageFixture.Start(); await h.Check();
        Assert.NotNull(TranslationField(h.Editor, "_continuityReport"));
        Assert.Equal(string.Join("\n\n", h.Data.Html.Take(3).Select(WriterApp.Client.State.PlainTextMapper.ToPlainText)), h.Provider.Request!.SurroundingText);
        Assert.Equal(h.Data.Pages[0], h.Active.Id); Assert.Empty((System.Collections.IEnumerable)typeof(PageEditor).GetField("_pendingAiDecorations", TranslationPrivate)!.GetValue(h.PageEditor)!);
        await h.Event("OnJumpToContinuityIssueAsync", h.Issue); Assert.Equal(h.Data.Pages[1], h.Active.Id);
        Assert.Equal("Checked report", h.Status);
        Assert.Single(((System.Collections.IEnumerable)typeof(PageEditor).GetField("_pendingAiDecorations", TranslationPrivate)!.GetValue(h.PageEditor)!).Cast<object>());
        await h.Event("OpenContinuityProposalAsync", h.Issue); Assert.True((bool)TranslationField(h.Editor, "_isContinuityProposalOpen")!);
        Assert.Equal(1, h.Provider.Calls); Assert.Empty(h.Saves.Sources);
        await h.Event("ConfirmContinuityProposalApplyAsync");
        Assert.Equal("Continuity fix applied.", h.Status); Assert.Equal(h.Data.Pages[1], Assert.Single(h.Saves.Sources)!.PageId);
        h.Data.Restart(); await using var db = new AppDbContext(h.Data.Options);
        Assert.Equal(h.Data.Html[0], (await db.Pages.FindAsync(h.Data.Pages[0]))!.Content);
        Assert.Equal("<p>The <strong>clock</strong> stood beside the chime.</p>", (await db.Pages.FindAsync(h.Data.Pages[1]))!.Content);
        Assert.Equal(h.Data.Html[2], (await db.Pages.FindAsync(h.Data.Pages[2]))!.Content);
        Assert.NotEmpty(await db.PageVersions.Where(v => v.PageId == h.Data.Pages[1]).ToListAsync());
    }
    [Fact]
    public async Task ClientConsistencyCancelledPageNavigationCannotOpenLateResultAndCanImmediatelyRetry()
    {
        var gate = new RequestGate("/pages");
        await using var h = await ConsistencyPageFixture.Start(gate: gate); await h.Check(); var issue = h.Issue;
        var navigation = h.Event("OnJumpToContinuityIssueAsync", issue);
        await gate.Ready.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await CancelRequest(h.Renderer, h.Editor); Assert.True(gate.Token.IsCancellationRequested);
        await h.Event("OnJumpToContinuityIssueAsync", issue);
        Assert.Equal(h.Data.Pages[1], h.Active.Id); Assert.Null(TranslationField(h.Editor, "_clientAiRequest"));
        gate.Release.TrySetResult(); await navigation.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(h.Data.Pages[1], h.Active.Id); Assert.Empty(h.Saves.Sources);
        await h.Event("OpenContinuityProposalAsync", issue); Assert.True((bool)TranslationField(h.Editor, "_isContinuityProposalOpen")!);
    }
    [Fact]
    public async Task ClientConsistencySourceChangedDuringAnalysisPublishesNoApplicableReport()
    {
        await using var h = await ConsistencyPageFixture.Start();
        h.Provider.After = async () => {
            await using var db = new AppDbContext(h.Data.Options);
            (await db.Pages.FindAsync(h.Data.Pages[1]))!.Content += "<p>Concurrent analysis edit.</p>";
            await db.SaveChangesAsync();
        };
        await h.Check(); Assert.Null(TranslationField(h.Editor, "_continuityReport"));
        Assert.Null(TranslationField(h.Editor, "_continuityCheckedSource")); Assert.NotNull(h.Status); Assert.Empty(h.Saves.Sources);
    }
    [Fact]
    public async Task ClientConsistencyPageSwitchKeepsIntentionalDecisionsAndConflictingEvidence()
    {
        await using var h = await ConsistencyPageFixture.Start(); await h.Check();
        await h.Event("MarkContinuityIntentionalAsync", h.Issue);
        Assert.Single((IEnumerable<string>)TranslationField(h.Editor, "_intentionalContinuity")!);
        await h.Event("OnJumpToContinuityIssueAsync", h.Issue);
        Assert.Equal(h.Data.Pages[1], h.Active.Id);
        Assert.Single((IEnumerable<string>)TranslationField(h.Editor, "_intentionalContinuity")!);
        await h.Event("ViewContinuityComparisonAsync", new ConsistencyComparison(h.Data.Sections[1], "Other section"));
        Assert.NotNull(TranslationField(h.Editor, "_continuitySourcePreview"));
        await h.Event("SelectManuscriptPageAsync", new ChangeEventArgs { Value = h.Data.Pages[0].ToString() });
        await h.SetEditor(h.Active);
        Assert.Single((IEnumerable<string>)TranslationField(h.Editor, "_intentionalContinuity")!);
        await h.Event("OnJumpToContinuityIssueAsync", h.Issue);
        Assert.Equal(h.Data.Pages[1], h.Active.Id); Assert.Empty(h.Saves.Sources);
        await h.Event("MarkContinuityIntentionalAsync", h.Issue);
        Assert.Empty((IEnumerable<string>)TranslationField(h.Editor, "_intentionalContinuity")!);
    }
    [Fact]
    public async Task WebConsistencyAnnotatedReadRetainsAuthorizationAndTranslationMutationGuards()
    {
        await using var f = new TranslationFixture(); await f.Start(); var approval = await f.Approval("section", "replace");
        await using (var db = new AppDbContext(f.Options)) {
            db.PageAnnotations.Add(new() { Id = Guid.NewGuid(), DocumentId = f.Document, PageId = f.Pages[0],
                AnchorFrom = 1, AnchorTo = 4, AnchorText = "Åsa", Content = "Authored comment", AuthorUserId = "ordinary-author" });
            await db.SaveChangesAsync();
        }
        string url = $"{f.Endpoint}/source?scope=section&sectionId={f.Sections[0]}";
        var read = await f.Http.GetFromJsonAsync<WebTranslationSource>(url + "&purpose=consistency");
        Assert.Equal(f.Pages.Take(3), read!.Sections.Single().Pages.Select(p => p.Id));
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Http.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Http.GetAsync(url + "&purpose=unknown")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Http.PostAsJsonAsync(f.Endpoint + "/approve", approval)).StatusCode);
        f.Http.DefaultRequestHeaders.Remove("X-Test-Owner"); f.Http.DefaultRequestHeaders.Add("X-Test-Owner", "foreign-author");
        Assert.Equal(HttpStatusCode.NotFound, (await f.Http.GetAsync(url + "&purpose=consistency")).StatusCode);
        f.Http.DefaultRequestHeaders.Remove("X-Test-Owner");
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Http.GetAsync(url + "&purpose=consistency")).StatusCode);
        await using var verify = new AppDbContext(f.Options);
        Assert.Equal(f.Html[0], (await verify.Pages.FindAsync(f.Pages[0]))!.Content); Assert.Single(await verify.PageAnnotations.ToListAsync());
    }
    [Fact]
    public async Task ClientConsistencyIdenticalEvidenceAcrossPagesIsAmbiguousAndNeverNavigatesOrAppliesElsewhere()
    {
        await using var h = await ConsistencyPageFixture.Start(true); await h.Check();
        await h.Event("OnJumpToContinuityIssueAsync", h.Issue); Assert.Contains("ambiguous", h.Status);
        await h.Event("OpenContinuityProposalAsync", h.Issue); Assert.Contains("ambiguous", h.Status);
        Assert.Equal(h.Data.Pages[0], h.Active.Id); Assert.Empty(h.Saves.Sources); Assert.False((bool)TranslationField(h.Editor, "_isContinuityProposalOpen")!);
    }
    [Theory][InlineData("writing")][InlineData("reorder")][InlineData("delete")][InlineData("move")][InlineData("account")][InlineData("backend")]
    public async Task ClientConsistencySourceDriftRejectsNavigationBeforeOpeningAnotherPage(string change)
    {
        await using var h = await ConsistencyPageFixture.Start(); await h.Check();
        if (change == "account") { h.Editor.Http.DefaultRequestHeaders.Remove("X-Test-Owner"); h.Editor.Http.DefaultRequestHeaders.Add("X-Test-Owner", "foreign-author"); }
        else if (change == "backend") h.Editor.Http = new HttpClient(h.Saves, false) { BaseAddress = new("http://different.local/") };
        else { await using var db = new AppDbContext(h.Data.Options); var page = (await db.Pages.FindAsync(h.Data.Pages[1]))!;
            if (change == "writing") page.Content += "<p>Later edit.</p>";
            if (change == "reorder") page.OrderIndex = 7;
            if (change == "move") page.SectionId = h.Data.Sections[1];
            if (change == "delete") db.Pages.Remove(page);
            await db.SaveChangesAsync(); }
        await h.Event("OnJumpToContinuityIssueAsync", h.Issue);
        Assert.Equal(h.Data.Pages[0], h.Active.Id); Assert.Empty(h.Saves.Sources); Assert.NotNull(h.Status);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task ClientConsistencyUnsavedActivePageSavesBeforeNavigationOrRetainsItsDraftOnFailure(bool failure)
    {
        await using var h = await ConsistencyPageFixture.Start(); await h.Check(); h.Saves.Fail = failure;
        string writing = h.Js.Html + "<p>Unsaved active-page draft.</p>"; h.Js.Html = writing;
        typeof(PageEditor).GetField("_content", TranslationPrivate)!.SetValue(h.PageEditor, writing);
        await h.Event("OnJumpToContinuityIssueAsync", h.Issue);
        Assert.Equal(h.Data.Pages[0], h.Active.Id); Assert.NotNull(h.Status); Assert.Single(h.Saves.Sources);
        await using var db = new AppDbContext(h.Data.Options);
        Assert.Equal(failure ? h.Data.Html[0] : writing, (await db.Pages.FindAsync(h.Data.Pages[0]))!.Content);
        if (failure) Assert.Equal(writing, (await new RecoveryDraftService(h.Js).LoadAsync(RecoveryDraftKey.ForPage(h.Data.Pages[0])))!.Content);
        Assert.Equal(h.Data.Html[1], (await db.Pages.FindAsync(h.Data.Pages[1]))!.Content);
    }
    [Theory][InlineData("writing")][InlineData("page")][InlineData("server")][InlineData("unsupported")][InlineData("save")][InlineData("save-race")]
    public async Task ClientConsistencyReviewedPageCannotBeRedirectedAndFailedSaveRetainsRecovery(string change)
    {
        await using var h = await ConsistencyPageFixture.Start(); await h.Check(); await h.Event("OpenContinuityProposalAsync", h.Issue);
        Assert.True((bool)TranslationField(h.Editor, "_isContinuityProposalOpen")!);
        if (change == "page") { TranslationSet(h.Editor, "_activePage", (await h.Data.Http.GetFromJsonAsync<PageDto[]>($"api/sections/{h.Data.Sections[0]}/pages"))![0]); await h.SetEditor(h.Active); }
        if (change == "writing") { h.Js.Html += "<p>Later local writing.</p>"; typeof(PageEditor).GetField("_content", TranslationPrivate)!.SetValue(h.PageEditor, h.Js.Html); }
        if (change == "server") { await using var db = new AppDbContext(h.Data.Options); (await db.Pages.FindAsync(h.Data.Pages[1]))!.Content += "<p>Concurrent writing.</p>"; await db.SaveChangesAsync(); }
        if (change == "unsupported") h.Js.RejectRichSpan = true;
        if (change == "save") h.Saves.Fail = true;
        if (change == "save-race") h.Saves.BeforeSave = async () => {
            await using var db = new AppDbContext(h.Data.Options);
            (await db.Pages.FindAsync(h.Data.Pages[1]))!.Content += "<p>Concurrent writing before PUT.</p>";
            await db.SaveChangesAsync();
        };
        if (change is "writing" or "page" or "server") {
            await h.Event("RecomputeContinuityRangeAsync"); Assert.NotNull(TranslationField(h.Editor, "_continuityProposalError"));
        }
        await h.Event("ConfirmContinuityProposalApplyAsync"); Assert.NotEqual("Continuity fix applied.", h.Status);
        await using var check = new AppDbContext(h.Data.Options);
        Assert.Equal(h.Data.Html[0], (await check.Pages.FindAsync(h.Data.Pages[0]))!.Content);
        Assert.DoesNotContain("chime", (await check.Pages.FindAsync(h.Data.Pages[1]))!.Content);
        if (change is "save" or "save-race") {
            Assert.Contains("chime", h.PageEditor.GetContent());
            Assert.Contains("chime", (await new RecoveryDraftService(h.Js).LoadAsync(RecoveryDraftKey.ForPage(h.Data.Pages[1])))!.Content);
        }
    }
}
