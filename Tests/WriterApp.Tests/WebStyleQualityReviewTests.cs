using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WriterApp.Application.Documents;
using WriterApp.Application.Usage;
using WriterApp.Client.Components.Editor;
using WriterApp.Client.Services;
using WriterApp.Data;
using WriterApp.Shared;
using Xunit;

namespace WriterApp.Tests;

public sealed partial class DesktopAiWebPersistenceTests
{
    private const string StyleHtml = "<p>The <strong>clock</strong> stood beside the clock.</p><p>She was very tired.</p>";
    private static StyleQualityReport StyleReport => new([
        new("The clock stood beside the clock.", "The clock stood beside the chime.", "repetition", "preference", "Vary the repeated noun.", "A different sound is emphasized."),
        new("She was very tired.", "She was tired.", "word_choice", "preference", "Remove the intensifier.", "Less emphasis on exhaustion.")]);
    private static async Task<ConsistencyPageFixture> StyleFixture(RequestGate? gate = null) {
        var h = await ConsistencyPageFixture.Start(gate: gate, styleHtml: StyleHtml);
        ((ICollection<string>)TranslationField(h.Editor, "_availableActionKeys")!).Add("custom_transform");
        TranslationSet(h.Editor, "_aiUsageStatus", new AiUsageStatusDto { UiEnabled = true, AiEnabled = true, QuotaRemaining = 1000, SupportsStyleQualityReview = true });
        h.Provider.OutputForCall = (_, _) => JsonSerializer.Serialize(StyleReport, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        h.PageEditor.AiHistoryOutbox = new(h.Editor.Http, h.Editor.JSRuntime, new WebCheckedAi(h.Editor.Http));
        return h;
    }
    private static async Task SelectStyle(ConsistencyPageFixture h, params int[] indexes) => await h.Event("StyleEditsChanged", (object)indexes);
    private static string? StyleError(ConsistencyPageFixture h) => TranslationField(h.Editor, "_styleReviewError")?.ToString();
    [Theory][InlineData("one")][InlineData("subset")][InlineData("all")]
    public async Task ClientStyleReviewPageApprovesSelectedEditsInOneCheckedSaveAndUndoRestoresRichSource(string selection)
    {
        await using var h = await StyleFixture(); await h.Event("RunStyleReviewAsync");
        Assert.NotNull(TranslationField(h.Editor, "_styleReview")); Assert.Null(StyleError(h)); Assert.Empty(h.Saves.Sources);
        Assert.Equal(h.Data.Pages[0], h.Provider.Request!.PageId); Assert.Equal(0, h.Provider.Request.SelectionStart);
        Assert.Equal(WriterApp.Client.State.PlainTextMapper.ToPlainText(StyleHtml), h.Provider.Request.OriginalText);
        Assert.Equal("selection", h.Provider.Request.Parameters!["scope"]!.ToString());
        int[] approved = selection == "all" ? [0, 1] : selection == "one" ? [0] : [1];
        await SelectStyle(h, approved); await h.Event("ApplyStyleReviewAsync"); Assert.Null(StyleError(h));
        Assert.Single(h.Saves.Sources); Assert.Equal(1, h.Provider.Calls); Assert.Null(TranslationField(h.Editor, "_styleReview"));
        await using (var db = new AppDbContext(h.Data.Options)) {
            string html = (await db.Pages.FindAsync(h.Data.Pages[0]))!.Content;
            Assert.Contains("<strong>clock</strong>", html);
            Assert.Equal(approved.Contains(0), html.Contains("chime")); Assert.Equal(!approved.Contains(1), html.Contains("very"));
            Assert.Equal(h.Data.Html[1], (await db.Pages.FindAsync(h.Data.Pages[1]))!.Content);
            var operation = Assert.Single(await db.WebAiHistoryOperations.ToListAsync()); Assert.NotNull(operation.CommittedAt);
        }
        TranslationSet(h.Editor, "_hasAiUndoHistory", true); await h.Event("OnAiUndoRequested");
        h.Data.Restart(); await using var verify = new AppDbContext(h.Data.Options);
        Assert.Equal(StyleHtml, (await verify.Pages.FindAsync(h.Data.Pages[0]))!.Content);
        Assert.Equal(2, await verify.WebAiHistoryOperations.CountAsync());
    }
    [Fact]
    public async Task ClientStyleReviewCurrentSelectionUsesItsExactPageOffsetAndNoCollapsedSelectionFallback()
    {
        await using var h = await StyleFixture();
        await h.Event("StyleReviewScopeChanged", "selection");
        h.Js.StyleFrom = 35; h.Js.StyleLength = 19;
        h.Provider.OutputForCall = (_, _) => JsonSerializer.Serialize(new StyleQualityReport([StyleReport.Edits[1]]), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        await h.Event("RunStyleReviewAsync"); Assert.Null(StyleError(h));
        Assert.Equal(35, h.Provider.Request!.SelectionStart); Assert.Equal("She was very tired.", h.Provider.Request.OriginalText);
        await h.Event("ApplyStyleReviewAsync"); Assert.Null(StyleError(h)); Assert.Single(h.Saves.Sources);
        await using var db = new AppDbContext(h.Data.Options);
        Assert.Equal("<p>The <strong>clock</strong> stood beside the clock.</p><p>She was tired.</p>", (await db.Pages.FindAsync(h.Data.Pages[0]))!.Content);
        h.Js.StyleLength = 0; await h.Event("RunStyleReviewAsync");
        Assert.Contains("Select current writing", StyleError(h)); Assert.Equal(1, h.Provider.Calls); Assert.Null(TranslationField(h.Editor, "_styleReview"));
    }
    [Theory][InlineData("empty")][InlineData("none")][InlineData("dismiss")]
    public async Task ClientStyleReviewNoApprovedChangeOrDismissNeverSaves(string state)
    {
        await using var h = await StyleFixture();
        if (state == "empty") h.Provider.OutputForCall = (_, _) => "{\"edits\":[]}";
        await h.Event("RunStyleReviewAsync"); Assert.Null(StyleError(h));
        if (state == "none") await SelectStyle(h);
        if (state == "dismiss") await h.Renderer.Dispatcher.InvokeAsync(() => typeof(WriterApp.Client.Pages.DocumentEditor).GetMethod("DismissStyleReview", TranslationPrivate)!.Invoke(h.Editor, null));
        await h.Event("ApplyStyleReviewAsync"); Assert.Empty(h.Saves.Sources);
        await using var db = new AppDbContext(h.Data.Options); Assert.Equal(StyleHtml, (await db.Pages.FindAsync(h.Data.Pages[0]))!.Content);
    }
    [Theory][InlineData("malformed")][InlineData("overlap")][InlineData("ambiguous")][InlineData("rich")][InlineData("unchecked")][InlineData("capability")]
    public async Task ClientStyleReviewInvalidResultsAndUnavailableCapabilityNeverBecomeApplicable(string failure)
    {
        await using var h = await StyleFixture();
        if (failure == "malformed") h.Provider.OutputForCall = (_, _) => "{\"edits\":[{}]}";
        if (failure == "overlap") h.Provider.OutputForCall = (_, _) => JsonSerializer.Serialize(new StyleQualityReport([StyleReport.Edits[0], StyleReport.Edits[0] with { Original = "stood beside", Replacement = "waited near" }]), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        if (failure == "ambiguous") h.Provider.OutputForCall = (_, _) => JsonSerializer.Serialize(new StyleQualityReport([StyleReport.Edits[0] with { Original = "clock", Replacement = "chime" }]), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        if (failure == "rich") h.Js.RejectStyle = true;
        if (failure == "unchecked") h.Provider.Failure = "unchecked";
        if (failure == "capability") TranslationSet(h.Editor, "_aiUsageStatus", new AiUsageStatusDto { UiEnabled = true, AiEnabled = true, QuotaRemaining = 1000 });
        await h.Event("RunStyleReviewAsync"); Assert.Null(TranslationField(h.Editor, "_styleReview")); Assert.NotNull(StyleError(h));
        await h.Event("ApplyStyleReviewAsync"); Assert.Empty(h.Saves.Sources);
        await using var db = new AppDbContext(h.Data.Options); Assert.Equal(StyleHtml, (await db.Pages.FindAsync(h.Data.Pages[0]))!.Content);
    }
    [Theory][InlineData("local")][InlineData("server")][InlineData("page")][InlineData("backend")][InlineData("account")][InlineData("save")][InlineData("save-race")]
    public async Task ClientStyleReviewDriftCannotRedirectApplyAndSaveFailureRetainsApprovedRichRecovery(string change)
    {
        await using var h = await StyleFixture(); await h.Event("RunStyleReviewAsync"); Assert.Null(StyleError(h));
        if (change == "local") { h.Js.Html += "<p>Unsaved writing.</p>"; typeof(PageEditor).GetField("_content", TranslationPrivate)!.SetValue(h.PageEditor, h.Js.Html); }
        if (change == "server") { await using var db = new AppDbContext(h.Data.Options); (await db.Pages.FindAsync(h.Data.Pages[0]))!.Content += "<p>Concurrent writing.</p>"; await db.SaveChangesAsync(); }
        if (change == "page") { TranslationSet(h.Editor, "_activePage", (await h.Data.Http.GetFromJsonAsync<PageDto[]>($"api/sections/{h.Data.Sections[0]}/pages"))![1]); await h.SetEditor(h.Active); }
        if (change == "backend") h.Editor.Http = new HttpClient(h.Saves, false) { BaseAddress = new("http://other.local/") };
        if (change == "account") { h.Editor.Http.DefaultRequestHeaders.Remove("X-Test-Owner"); h.Editor.Http.DefaultRequestHeaders.Add("X-Test-Owner", "foreign-author"); }
        if (change == "save") h.Saves.Fail = true;
        if (change == "save-race") h.Saves.BeforeSave = async () => { await using var db = new AppDbContext(h.Data.Options); (await db.Pages.FindAsync(h.Data.Pages[0]))!.Content += "<p>Race before PUT.</p>"; await db.SaveChangesAsync(); };
        await h.Event("ApplyStyleReviewAsync"); Assert.NotNull(StyleError(h));
        await using var verify = new AppDbContext(h.Data.Options);
        Assert.DoesNotContain("chime", (await verify.Pages.FindAsync(h.Data.Pages[0]))!.Content);
        Assert.Equal(h.Data.Html[1], (await verify.Pages.FindAsync(h.Data.Pages[1]))!.Content);
        if (change is "save" or "save-race") {
            var draft = (await new RecoveryDraftService(h.Js).LoadAsync(RecoveryDraftKey.ForPage(h.Data.Pages[0])))!.Content;
            Assert.Contains("chime", draft); Assert.Contains("<strong>clock</strong>", draft); Assert.DoesNotContain("very", draft);
        }
    }
    [Theory][InlineData("preparation")][InlineData("generation")][InlineData("preview")]
    public async Task ClientStyleReviewCancellationRejectsLateOutputAndImmediatelyAcceptsFreshRequest(string stage)
    {
        var gate = new RequestGate(stage == "preparation" ? "/" : "/execute");
        // The source path contains the fixture's generated document ID; gate an exact captured request below.
        await using var h = await StyleFixture(stage == "generation" ? gate : null);
        if (stage == "preparation") {
            gate = new RequestGate("/web-source/" + h.Data.Document);
            var transport = new CancellationTransport(h.Saves, gate);
            h.Editor.Http = new HttpClient(transport, false) { BaseAddress = new("http://localhost/") };
            h.Editor.Http.DefaultRequestHeaders.Add("X-Test-Owner", "ordinary-author");
        }
        if (stage == "preview") h.Js.StylePreviewGate = gate;
        var pending = h.Event("RunStyleReviewAsync"); await gate.Ready.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await CancelRequest(h.Renderer, h.Editor); Assert.True(gate.Token.IsCancellationRequested);
        await h.Event("RunStyleReviewAsync"); Assert.NotNull(TranslationField(h.Editor, "_styleReview"));
        gate.Release.TrySetResult(); await pending.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.NotNull(TranslationField(h.Editor, "_styleReview")); Assert.Equal(stage == "preparation" ? 1 : 2, h.Provider.Calls); Assert.Empty(h.Saves.Sources);
    }
}
