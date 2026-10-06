using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WriterApp.Application.Documents;
using WriterApp.Data;
using Xunit;

namespace WriterApp.Tests;
public sealed partial class DesktopAiWebPersistenceTests
{
    [Theory][InlineData("Ai")][InlineData("Annotations")]
    public async Task WebTabChangeClearsCurrentAndDeferredConsistencyHighlights(string nextTab)
    {
        await using var f = new TranslationFixture(); await f.Start();
        using var provider = new CheckedProvider(f); using var editor = CheckedEditor(f, provider);
        var pageEditor = new WriterApp.Client.Components.Editor.PageEditor();
        TranslationSet(editor, "_pageEditor", pageEditor);
        var tabType = typeof(WriterApp.Client.Pages.DocumentEditor).GetNestedType("ContextTab", System.Reflection.BindingFlags.NonPublic)!;
        TranslationSet(editor, "_activeContextTab", Enum.Parse(tabType, "Continuity"));
        TranslationSet(editor, "_selectedContinuityIssueKey", "selected");
        TranslationSet(editor, "_pendingContinuityHighlights", true);
        await pageEditor.SetAiDecorationsAsync([new(0, 4, "wa-consistency-passage", true)]);
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new TranslationRenderer(services, services.GetRequiredService<ILoggerFactory>()); await renderer.Attach(editor);
        await TranslationEvent(renderer, editor, "SetContextTabAsync", Enum.Parse(tabType, nextTab), false, false);
        // A check completing after navigation must not repaint the departed tab's markup.
        TranslationSet(editor, "_pendingContinuityHighlights", true);
        await TranslationEvent(renderer, editor, "ApplyContinuityHighlightsAsync");
        Assert.Null(TranslationField(editor, "_selectedContinuityIssueKey"));
        Assert.False((bool)TranslationField(editor, "_pendingContinuityHighlights")!);
        Assert.Empty((System.Collections.IEnumerable)typeof(WriterApp.Client.Components.Editor.PageEditor).GetField("_pendingAiDecorations", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(pageEditor)!);
        Assert.Equal(0, provider.Calls);
    }
    [Fact]
    public async Task WebIntentionalDecisionSurvivesNewChecksAndSourcePreviewKeepsTheReportOpen()
    {
        await using var f = new TranslationFixture(); await f.Start();
        using var provider = new CheckedProvider(f); using var editor = CheckedEditor(f, provider);
        var target = (await f.Http.GetFromJsonAsync<PageDto[]>($"api/sections/{f.Sections[0]}/pages"))![0];
        var reference = (await f.Http.GetFromJsonAsync<PageDto[]>($"api/sections/{f.Sections[1]}/pages"))![0];
        string original = WriterApp.Application.State.PlainTextMapper.ToPlainText(target.Content);
        string other = WriterApp.Application.State.PlainTextMapper.ToPlainText(reference.Content);
        provider.ConsistencyOutput = System.Text.Json.JsonSerializer.Serialize(new { schemaVersion = "1.0", issues = new[] {
            new { severity = "high", type = "character", message = "Test contradiction", evidence = new { sectionId = f.Sections[0], quote = original },
                comparisonEvidence = new { sectionId = f.Sections[1], quote = other }, suggestedFix = "", anchor = new { plainTextStart = 0, plainTextLength = original.Length } }
        }});
        TranslationSet(editor, "_activePage", target);
        ((ICollection<string>)TranslationField(editor, "_availableActionKeys")!).Add("continuity.check_section");
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new TranslationRenderer(services, services.GetRequiredService<ILoggerFactory>()); await renderer.Attach(editor);
        await TranslationEvent(renderer, editor, "ExecuteContinuityActionAsync", "continuity.check_section", "Checked report", null);
        Assert.NotNull(TranslationField(editor, "_continuityReport"));
        var report = TranslationField(editor, "_continuityReport")!;
        object issue = ((System.Collections.IEnumerable)report.GetType().GetProperty("Issues")!.GetValue(report)!).Cast<object>().Single();
        await TranslationEvent(renderer, editor, "MarkContinuityIntentionalAsync", issue);
        Assert.Single((IEnumerable<string>)TranslationField(editor, "_intentionalContinuity")!);
        await TranslationEvent(renderer, editor, "ExecuteContinuityActionAsync", "continuity.check_section", "Checked report", null);
        Assert.Single((IEnumerable<string>)TranslationField(editor, "_intentionalContinuity")!);
        await TranslationEvent(renderer, editor, "ViewContinuityComparisonAsync", new WriterApp.Shared.ConsistencyComparison(f.Sections[1], other));
        Assert.NotNull(TranslationField(editor, "_continuitySourcePreview"));
        Assert.NotNull(TranslationField(editor, "_continuityReport"));
        await TranslationEvent(renderer, editor, "MarkContinuityIntentionalAsync", issue);
        Assert.Empty((IEnumerable<string>)TranslationField(editor, "_intentionalContinuity")!);
        await using var db = new AppDbContext(f.Options);
        Assert.Equal(target.Content, (await db.Pages.FindAsync(target.Id))!.Content);
    }
    [Theory][InlineData(null, 3, true)][InlineData("unavailable", 0, true)][InlineData("stale", 0, false)]
    public async Task WebConsistencyPreparesReferencesAutomaticallyAndOnlyAllowsAvailabilityFallback(string? failure, int versions, bool succeeds)
    {
        await using var f = new TranslationFixture(); await f.Start();
        using var provider = new CheckedProvider(f) { ReferenceFailure = failure }; using var editor = CheckedEditor(f, provider);
        TranslationSet(editor, "_activePage", (await f.Http.GetFromJsonAsync<PageDto[]>($"api/sections/{f.Sections[0]}/pages"))![0]);
        ((ICollection<string>)TranslationField(editor, "_availableActionKeys")!).Add("continuity.check_section");
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new TranslationRenderer(services, services.GetRequiredService<ILoggerFactory>()); await renderer.Attach(editor);
        await TranslationEvent(renderer, editor, "ExecuteContinuityActionAsync", "continuity.check_section", "Checked report", null);
        Assert.Equal(succeeds ? 3 : 1, provider.ReferenceRefreshes);
        if (succeeds) {
            Assert.Equal(versions, provider.Request!.ExpectedCanonVersions!.Count);
            Assert.NotNull(TranslationField(editor, "_continuityReport"));
            if (failure is not null) Assert.Contains("Some references could not be prepared", TranslationField(editor, "_continuityCoverage")!.ToString());
            else {
                await TranslationEvent(renderer, editor, "ExecuteContinuityActionAsync", "continuity.check_section", "Checked report", null);
                Assert.Equal(3, provider.ReferenceRefreshes); // No AI extraction for a matching second check.
            }
        } else { Assert.Null(provider.Request); Assert.Null(TranslationField(editor, "_continuityReport")); }
        await using var db = new AppDbContext(f.Options);
        Assert.All(await db.Pages.ToArrayAsync(), p => Assert.DoesNotContain("Reviewed", p.Content));
    }
    [Theory][InlineData(false, false)][InlineData(true, false)][InlineData(true, true)]
    public async Task WebDiscardRemovesOnlyItsSuggestionAndNeverSavesWriting(bool pending, bool busy)
    {
        await using var f = new TranslationFixture(); await f.Start();
        using var provider = new CheckedProvider(f); using var editor = CheckedEditor(f, provider);
        var field = typeof(WriterApp.Client.Pages.DocumentEditor).GetField("_continuityReport", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        string json = System.Text.Json.JsonSerializer.Serialize(new { schemaVersion = "1.0", issues = new[] {
            new { severity = "low", type = "character", message = "First finding", evidence = new { sectionId = f.Sections[0], quote = "Original" }, suggestedFix = "Changed", anchor = new { plainTextStart = 0, plainTextLength = 8 } },
            new { severity = "low", type = "timeline", message = "Second finding", evidence = new { sectionId = f.Sections[0], quote = "Other" }, suggestedFix = "Later", anchor = new { plainTextStart = 12, plainTextLength = 5 } }
        }});
        var report = System.Text.Json.JsonSerializer.Deserialize(json, field.FieldType, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;
        TranslationSet(editor, "_continuityReport", report);
        var issue = ((System.Collections.IEnumerable)report.GetType().GetProperty("Issues")!.GetValue(report)!).Cast<object>().First();
        var pageEditor = new WriterApp.Client.Components.Editor.PageEditor();
        TranslationSet(editor, "_pageEditor", pageEditor);
        await pageEditor.SetAiDecorationsAsync([new(0, 8, "wa-consistency-passage", true)]);
        TranslationSet(editor, "_pendingContinuityHighlights", true);
        if (pending) { TranslationSet(editor, "_pendingContinuityIssue", issue); TranslationSet(editor, "_isContinuityProposalOpen", true); }
        TranslationSet(editor, "_continuityBusy", busy);
        await using var beforeDb = new AppDbContext(f.Options);
        var before = await beforeDb.Pages.OrderBy(p => p.Id).Select(p => p.Content).ToArrayAsync();
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new TranslationRenderer(services, services.GetRequiredService<ILoggerFactory>()); await renderer.Attach(editor);
        if (pending) await TranslationEvent(renderer, editor, "DiscardPendingContinuitySuggestionAsync");
        else await TranslationEvent(renderer, editor, "DiscardContinuitySuggestionAsync", issue);
        var afterReport = TranslationField(editor, "_continuityReport")!;
        var remaining = ((System.Collections.IEnumerable)afterReport.GetType().GetProperty("Issues")!.GetValue(afterReport)!).Cast<object>().ToArray();
        Assert.Equal(busy ? 2 : 1, remaining.Length);
        if (!busy) {
            Assert.Equal("Second finding", remaining[0].GetType().GetProperty("Message")!.GetValue(remaining[0]));
            Assert.Equal("Suggestion discarded. Your writing is unchanged.", TranslationField(editor, "_continuityStatus"));
            Assert.False((bool)TranslationField(editor, "_isContinuityProposalOpen")!);
            Assert.False((bool)TranslationField(editor, "_pendingContinuityHighlights")!);
            Assert.Empty((System.Collections.IEnumerable)typeof(WriterApp.Client.Components.Editor.PageEditor).GetField("_pendingAiDecorations", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(pageEditor)!);
        }
        await using var afterDb = new AppDbContext(f.Options);
        Assert.Equal(before, await afterDb.Pages.OrderBy(p => p.Id).Select(p => p.Content).ToArrayAsync());
        Assert.Empty(await afterDb.AiActionHistoryEntries.ToArrayAsync()); Assert.Equal(0, provider.Calls);
    }

    [Theory][InlineData(null)][InlineData("writing")][InlineData("canon")][InlineData("planning")][InlineData("unchecked")]
    public async Task ActualWebConsistencyReportBindsTypedCanonAndRejectsLateWritingPlanningAndCanon(string? failure)
    {
        await using var f=new TranslationFixture();await f.Start();using var provider=new CheckedProvider(f){Failure=failure};using var editor=CheckedEditor(f,provider);
        TranslationSet(editor,"_activePage",(await f.Http.GetFromJsonAsync<PageDto[]>($"api/sections/{f.Sections[0]}/pages"))!.Single(p=>p.Id==f.Pages[0]));
        ((ICollection<string>)TranslationField(editor,"_availableActionKeys")!).Add("continuity.check_section");
        provider.After=async ()=> {
            if(failure=="writing")await f.Http.PutAsJsonAsync($"api/pages/{f.Pages[0]}",new{content="Concurrent prose"});
            if(failure is "planning" or "canon") {await using var db=new AppDbContext(f.Options);
                if(failure=="planning")(await db.ProjectNodes.SingleAsync()).Title="Concurrent planning";
                else db.BibleSnapshots.Add(new(){Id=Guid.NewGuid(),DocumentId=f.Document,BibleType="character",ContentJson="{\"schemaVersion\":\"1.0\",\"characters\":[]}"});
                await db.SaveChangesAsync();
            }
        };
        using var services=new ServiceCollection().AddLogging().BuildServiceProvider();await using var renderer=new TranslationRenderer(services,services.GetRequiredService<ILoggerFactory>());await renderer.Attach(editor);
        await TranslationEvent(renderer,editor,"ExecuteContinuityActionAsync","continuity.check_section","Checked report",new Dictionary<string,object?>());
        Assert.NotNull(provider.Request!.ExpectedCanonVersions);
        if(failure is null) {Assert.NotNull(TranslationField(editor,"_continuityCheckedSource"));Assert.NotNull(TranslationField(editor,"_continuityReport"));}
        else {Assert.Null(TranslationField(editor,"_continuityCheckedSource"));Assert.Null(TranslationField(editor,"_continuityReport"));}
        await using var verify=new AppDbContext(f.Options);Assert.All(await verify.Pages.ToListAsync(),p=>Assert.DoesNotContain("Reviewed",p.Content));
    }
}
