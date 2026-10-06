using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;
using WriterApp.Application.Documents;
using WriterApp.Application.Usage;
using WriterApp.Client.Pages;
using WriterApp.Client.Services;
using WriterApp.Data;
using WriterApp.Shared;
using Xunit;
namespace WriterApp.Tests;
public sealed partial class DesktopAiWebPersistenceTests
{
    private sealed class RecommendedJs(IJSRuntime inner, PageJs active) : IJSRuntime {
        public string? Copied;
        public ValueTask<T> InvokeAsync<T>(string name, object?[]? args) => InvokeAsync<T>(name, default, args);
        public ValueTask<T> InvokeAsync<T>(string name, CancellationToken ct, object?[]? args) {
            ct.ThrowIfCancellationRequested(); object? value;
            if (name == "navigator.clipboard.writeText") { Copied = (string)args![0]!; return ValueTask.FromResult(default(T)!); }
            if (name == "tiptapEditor.captureTranslation") value = new { runs = TranslationFixture.Runs((string)args![0]!) };
            else if (name == "tiptapEditor.previewTranslation") value = WebTranslationHtml.Map((string)args![0]!, new(Guid.Empty, TranslationFixture.Runs((string)args[0]!)), new(Guid.Empty, (IReadOnlyList<TranslationRun>)args[2]!));
            else if (name == "tiptapEditor.getPlainText") value = WriterApp.Client.State.PlainTextMapper.ToPlainText(active.Html);
            else return inner.InvokeAsync<T>(name, ct, args);
            return ValueTask.FromResult(JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value), new JsonSerializerOptions(JsonSerializerDefaults.Web))!);
        }
    }
    private static object RecommendedOption(string tool) => typeof(DocumentEditor).GetMethod("CreateRecommendedToolOption", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [RecommendedWriting.Tool(tool)])!;
    private static Task RunRecommended(ConsistencyPageFixture h, string tool) => h.Renderer.Dispatcher.InvokeAsync(() => (Task)typeof(DocumentEditor).GetMethods(TranslationPrivate).Single(m => m.Name == "OnAiActionSelected" && m.GetParameters().Length == 2).Invoke(h.Editor, [RecommendedOption(tool), false])!);
    private static async Task<ConsistencyPageFixture> RecommendedFixture(RequestGate? gate = null) {
        var h = await ConsistencyPageFixture.Start(gate: gate, styleHtml: StyleHtml);
        ((ICollection<string>)TranslationField(h.Editor, "_availableActionKeys")!).Add("custom_transform");
        TranslationSet(h.Editor, "_aiUsageStatus", new AiUsageStatusDto { UiEnabled = true, AiEnabled = true, QuotaRemaining = 1000, SupportsRecommendedWriting = true });
        h.Editor.JSRuntime = new RecommendedJs(new TranslationJs(), h.Js);
        h.PageEditor.AiHistoryOutbox = new(h.Editor.Http, h.Editor.JSRuntime, new WebCheckedAi(h.Editor.Http));
        h.Provider.OutputForCall = (_, normal) => {
            var run = RecommendedWriting.From(h.Provider.Request!.Parameters)!;
            if (!RecommendedWriting.Revises(run.ToolId)) return JsonSerializer.Serialize(new { items = Enumerable.Range(1, RecommendedWriting.ItemCount(run.ToolId)).Select(i => "A messenger arrived with a sealed letter " + i + ".").ToArray() });
            if (RecommendedWriting.Output(run.ToolId) == RecommendedOutput.OpeningRevision) {
                var source = WritingActions.Parse(h.Provider.Request.Parameters![WritingActions.Parameter]!.ToString()!);
                return WritingActions.Serialize(source with { Pages = source.Pages.Select((p, i) => i == 0 ? p with { Runs = p.Runs.Select(r => r.Id.Split('.')[0] == p.Runs[0].Id.Split('.')[0] ? r with { Text = string.Concat(r.Text.TakeWhile(char.IsWhiteSpace)) + "Reviewed " + r.Text.Trim() + string.Concat(r.Text.Reverse().TakeWhile(char.IsWhiteSpace).Reverse()) } : r).ToArray() } : p).ToArray() });
            }
            return normal;
        };
        return h;
    }
    [Theory][MemberData(nameof(RecommendedWritingTests.Catalog), MemberType = typeof(RecommendedWritingTests))]
    public async Task RecommendedClientRunsEveryCatalogActionWithSafeOutputSpecificReviewAndPersistence(string tool) {
        await using var h = await RecommendedFixture();
        bool append = RecommendedWriting.Output(tool) == RecommendedOutput.AppendParagraph;
        if (append) { var pages = (await h.Data.Http.GetFromJsonAsync<PageDto[]>($"api/sections/{h.Data.Sections[0]}/pages"))!; TranslationSet(h.Editor, "_activePage", pages.Last()); await h.SetEditor(pages.Last()); h.PageEditor.AiHistoryOutbox = new(h.Editor.Http, h.Editor.JSRuntime, new WebCheckedAi(h.Editor.Http)); }
        await RunRecommended(h, tool);
        Assert.Null(TranslationField(h.Editor, "_clientAiRequestMessage")); Assert.Equal(1, h.Provider.Calls);
        Assert.Equal(tool, RecommendedWriting.From(h.Provider.Request!.Parameters)!.ToolId); Assert.False(h.Provider.Request.Parameters!.ContainsKey(ReusablePrompts.Parameter));
        Assert.Empty(h.Saves.Sources);
        if (RecommendedWriting.CopyOnly(tool)) {
            Assert.Null(TranslationField(h.Editor, "_pendingAiProposal")); Assert.NotNull(TranslationField(h.Editor, "_recommendedCopy"));
            await h.Event("CopyRecommendedText", "A messenger arrived with a sealed letter 3.");
            Assert.Equal(RecommendedWriting.Output(tool) == RecommendedOutput.Headlines ? "A messenger arrived with a sealed letter 3." : null, ((RecommendedJs)h.Editor.JSRuntime).Copied);
            if (tool == "other.summarize_clearly") { await h.Event("CopyRecommendedText", "A messenger arrived with a sealed letter 1."); Assert.NotNull(((RecommendedJs)h.Editor.JSRuntime).Copied); }
            await h.Event("OnApplyPendingAiProposal"); Assert.Empty(h.Saves.Sources);
        } else {
            Assert.NotNull(TranslationField(h.Editor, "_pendingAiProposal")); await h.Event("OnApplyPendingAiProposal");
            Assert.Null(TranslationField(h.Editor, "_pendingAiProposal")); Assert.Null(TranslationField(h.Editor, "_webTranslationError"));
        }
        await using var db = new AppDbContext(h.Data.Options);
        string first = (await db.Pages.FindAsync(h.Data.Pages[0]))!.Content;
        if (RecommendedWriting.CopyOnly(tool) || append) Assert.Equal(StyleHtml, first);
        else { Assert.Contains("Reviewed", first); Assert.Contains("<strong>", first); }
        Assert.Equal(h.Data.Html[3], (await db.Pages.FindAsync(h.Data.Pages[3]))!.Content);
        if (append) { Assert.Contains("messenger", (await db.Pages.FindAsync(h.Data.Pages[2]))!.Content); Assert.Single(h.Saves.Sources); TranslationSet(h.Editor, "_hasAiUndoHistory", true); await h.Event("OnAiUndoRequested"); await using var verify = new AppDbContext(h.Data.Options); Assert.Equal(h.Data.Html[2], (await verify.Pages.FindAsync(h.Data.Pages[2]))!.Content); }
        else if (!RecommendedWriting.CopyOnly(tool)) {
            var operation = Assert.Single(await db.WebTranslationOperations.ToListAsync());
            var receipt = JsonSerializer.Deserialize<WebTranslationReceipt>(operation.ReceiptJson, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
            Assert.Equal("Committed", receipt.State);
            await h.Event("RecoverTranslationOriginalAsync", receipt);
            Assert.Null(TranslationField(h.Editor, "_webTranslationError"));
        }
        h.Data.Restart(); await using var reopened = new AppDbContext(h.Data.Options);
        Assert.Equal(h.Data.Html[3], (await reopened.Pages.FindAsync(h.Data.Pages[3]))!.Content);
    }
    [Theory][InlineData("malformed")][InlineData("capability")][InlineData("source")][InlineData("account")][InlineData("backend")][InlineData("wrong-append-page")]
    public async Task RecommendedClientRefusesInvalidStaleAndUnsupportedResultsWithoutAuthoredMutation(string failure) {
        await using var h = await RecommendedFixture();
        if (failure == "malformed") h.Provider.OutputForCall = (_, _) => "{\"items\":[\"one\"]}";
        if (failure == "capability") TranslationSet(h.Editor, "_aiUsageStatus", new AiUsageStatusDto { UiEnabled = true, AiEnabled = true, QuotaRemaining = 1000 });
        await RunRecommended(h, failure == "wrong-append-page" ? "novel.continue_scene" : "blog.generate_headlines");
        if (failure == "source") { await using var db = new AppDbContext(h.Data.Options); (await db.Pages.FindAsync(h.Data.Pages[0]))!.Content += "<p>Later writing.</p>"; await db.SaveChangesAsync(); }
        if (failure == "account") { h.Editor.Http.DefaultRequestHeaders.Remove("X-Test-Owner"); h.Editor.Http.DefaultRequestHeaders.Add("X-Test-Owner", "foreign-author"); }
        if (failure == "backend") h.Editor.Http = new HttpClient(h.Saves, false) { BaseAddress = new("http://other.local/") };
        await h.Event("CopyRecommendedText", "A messenger arrived with a sealed letter 1."); Assert.Null(((RecommendedJs)h.Editor.JSRuntime).Copied);
        Assert.Empty(h.Saves.Sources);
        var pending = TranslationField(h.Editor, "_pendingAiProposal");
        Assert.Null(pending?.GetType().GetProperty("ProposedText")?.GetValue(pending));
        if (failure is "wrong-append-page" or "capability") Assert.Equal(0, h.Provider.Calls);
    }
    [Fact]
    public async Task RecommendedClientCancellationDropsLateCandidateAndPermitsFreshRequest() {
        var gate = new RequestGate("/execute"); await using var h = await RecommendedFixture(gate);
        var pending = RunRecommended(h, "blog.generate_headlines"); await gate.Ready.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await CancelRequest(h.Renderer, h.Editor); Assert.True(gate.Token.IsCancellationRequested);
        await RunRecommended(h, "other.summarize_clearly"); var fresh = TranslationField(h.Editor, "_recommendedCopy"); Assert.NotNull(fresh);
        gate.Release.TrySetResult(); await pending.WaitAsync(TimeSpan.FromSeconds(10)); Assert.Same(fresh, TranslationField(h.Editor, "_recommendedCopy")); Assert.Empty(h.Saves.Sources);
    }
    private sealed class RecommendedCopyTransport(HttpMessageHandler inner, Action dismiss) : DelegatingHandler(inner) {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) {
            var response = await base.SendAsync(request, ct);
            if (request.RequestUri!.AbsolutePath.Contains("/writing-outline/")) dismiss();
            return response;
        }
    }
    [Fact]
    public async Task RecommendedCopyDismissedDuringOutlineCheckCannotCopyItsLateResult() {
        await using var h = await RecommendedFixture(); await RunRecommended(h, "blog.generate_headlines");
        using var client = new HttpClient(new RecommendedCopyTransport(h.Saves, () => TranslationSet(h.Editor, "_recommendedCopy", null)), false) { BaseAddress = h.Editor.Http.BaseAddress };
        foreach (var header in h.Editor.Http.DefaultRequestHeaders) client.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
        h.Editor.Http = client;
        await h.Event("CopyRecommendedText", "A messenger arrived with a sealed letter 1.");
        Assert.Null(((RecommendedJs)h.Editor.JSRuntime).Copied); Assert.Empty(h.Saves.Sources);
        Assert.Contains("changed", TranslationField(h.Editor, "_recommendedCopyMessage")!.ToString());
    }
}
