using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using WriterApp.Application.Documents;
using WriterApp.Client.Components.Editor;
using WriterApp.Client.Pages;
using WriterApp.Client.Services;
using WriterApp.Data;
using WriterApp.Shared;
using Xunit;

namespace WriterApp.Tests;

public sealed partial class DesktopAiWebPersistenceTests
{
    private sealed class TargetedTransport(HttpMessageHandler inner) : DelegatingHandler(inner)
    {
        public string? Failure; public int Calls; public bool SecondOnly;
        public readonly List<string> Instructions = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("rewrite.selection/execute"))
            {
                Calls++; var body = (await request.Content!.ReadFromJsonAsync<WriterApp.Application.AI.AiActionExecuteRequestDto>(ct))!;
                Instructions.Add(body.Parameters!["instruction"]!.ToString()!);
                if (!SecondOnly || Calls == 2)
                {
                    if (Failure == "network") throw new HttpRequestException("Synthetic network error");
                    if (Failure is "401" or "429" or "503") return new((HttpStatusCode)int.Parse(Failure));
                }
            }
            return await base.SendAsync(request, ct);
        }
    }
    [Theory][InlineData("style.repeated_words")][InlineData("style.passive_voice")][InlineData("readability.sentence_length")]
    public async Task ActualClientValidFirstResultDoesNotUseOptedInExtraGeneration(string rule)
    {
        var (before, valid) = TargetedQualityRetryTests.Text(rule);
        await using var f = new TranslationFixture { Configure = s => s.AddScoped<IQualityCheckService, QualityCheckService>() }; f.Html[0] = "<p>" + before + "</p>"; await f.Start();
        using var provider = new CheckedProvider(f) { OutputForCall = (_, _) => valid }; using var editor = CheckedEditor(f, provider);
        using var transport = new CancellationTransport(provider); var page = (await f.Http.GetFromJsonAsync<PageDto[]>($"api/sections/{f.Sections[0]}/pages"))![0]; await ConfigureCancellationEditor(f, editor, transport, page);
        await using var coordinator = new EditorSaveCoordinator(new RecoveryDraftService(new CancellationEditorJs(page.Content)), NullLogger<EditorSaveCoordinator>.Instance);
        CancellationPage(editor, page, coordinator); using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new TranslationRenderer(services, services.GetRequiredService<ILoggerFactory>()); await renderer.Attach(editor);
        await renderer.Dispatcher.InvokeAsync(() => typeof(DocumentEditor).GetMethod("TargetedRetryChanged", TranslationPrivate)!.Invoke(editor, [true]));
        Assert.True((bool)TranslationField(editor, "_targetedStrictRetry")!); await CancellationFlow(renderer, editor, "quality");
        var issue = ((List<PageQualityIssueDto>)TranslationField(editor, "_qualityIssues")!).First(i => i.RuleId == rule);
        await TranslationEvent(renderer, editor, "OpenQualityProposalAsync", issue);
        Assert.Equal(1, provider.Calls); Assert.True((bool)TranslationField(editor, "_isQualityProposalOpen")!); await AssertCancellationWritingUnchanged(f);
    }
    [Theory]
    [InlineData("style.repeated_words", false, false)][InlineData("style.repeated_words", true, false)][InlineData("style.repeated_words", true, true)]
    [InlineData("style.passive_voice", false, false)][InlineData("style.passive_voice", true, false)][InlineData("style.passive_voice", true, true)]
    [InlineData("readability.sentence_length", false, false)][InlineData("readability.sentence_length", true, false)][InlineData("readability.sentence_length", true, true)]
    public async Task ActualClientTargetedAdapterHonorsOptInAndExactlyOneSemanticRetry(string rule, bool optIn, bool secondValid)
    {
        var (before, valid) = TargetedQualityRetryTests.Text(rule);
        await using var f = new TranslationFixture { Configure = s => s.AddScoped<IQualityCheckService, QualityCheckService>() };
        f.Html[0] = "<p>" + before + "</p>"; await f.Start();
        using var provider = new CheckedProvider(f) { OutputForCall = (call, _) => call == 2 && secondValid ? valid : before };
        using var editor = CheckedEditor(f, provider); using var transport = new CancellationTransport(provider);
        var page = (await f.Http.GetFromJsonAsync<PageDto[]>($"api/sections/{f.Sections[0]}/pages"))![0];
        await ConfigureCancellationEditor(f, editor, transport, page);
        await using var coordinator = new EditorSaveCoordinator(new RecoveryDraftService(new CancellationEditorJs(page.Content)), NullLogger<EditorSaveCoordinator>.Instance);
        CancellationPage(editor, page, coordinator);
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new TranslationRenderer(services, services.GetRequiredService<ILoggerFactory>()); await renderer.Attach(editor);
        Assert.False((bool)TranslationField(editor, "_targetedStrictRetry")!); TranslationSet(editor, "_targetedStrictRetry", optIn);
        await CancellationFlow(renderer, editor, "quality");
        var issue = ((List<PageQualityIssueDto>)TranslationField(editor, "_qualityIssues")!).First(i => i.RuleId == rule);
        await TranslationEvent(renderer, editor, "OpenQualityProposalAsync", issue);
        Assert.Equal(optIn ? 2 : 1, provider.Calls);
        Assert.Equal(optIn && secondValid, (bool)TranslationField(editor, "_isQualityProposalOpen")!);
        if (optIn) Assert.Contains(TargetedQualityRetry.StrictInstruction, provider.Request!.Parameters!["instruction"]!.ToString());
        if (!secondValid || !optIn) Assert.Contains("retry manually", TranslationField(editor, "_qualityError")!.ToString());
        if (optIn && secondValid)
        {
            var reviewed = (PageQualityIssueDto)TranslationField(editor, "_proposalIssue")!;
            Assert.Equal(valid, reviewed.Fix!.Text);
            Assert.Equal(provider.Proposal, ((Dictionary<string, Guid>)TranslationField(editor, "_qualityHistoryProposals")!)[issue.IssueKey]);
        }
        await AssertCancellationWritingUnchanged(f);
        await using var db = new AppDbContext(f.Options); Assert.Empty(await db.WebAiHistoryOperations.ToListAsync());
    }
    [Theory][InlineData("valid")][InlineData("network")][InlineData("401")][InlineData("429")][InlineData("503")]
    [InlineData("stale")][InlineData("metadata")][InlineData("anchor")][InlineData("embedded")][InlineData("account")][InlineData("429-between")]
    public async Task ActualClientValidOrNonSemanticFailuresNeverCauseUnboundedOrIneligibleRetry(string failure)
    {
        const string rule = "style.passive_voice"; var (before, valid) = TargetedQualityRetryTests.Text(rule);
        await using var f = new TranslationFixture { Configure = s => s.AddScoped<IQualityCheckService, QualityCheckService>() };
        f.Html[0] = "<p>" + before + "</p>"; await f.Start();
        using var provider = new CheckedProvider(f) { OutputForCall = (_, _) => failure == "valid" ? valid : failure == "metadata" ? "{\"revisedText\":\"Anna opened the door.\"}" : before };
        using var editor = CheckedEditor(f, provider); using var failing = new TargetedTransport(provider) { Failure = failure == "429-between" ? "429" : failure, SecondOnly = failure == "429-between" };
        using var transport = new CancellationTransport(failing);
        var page = (await f.Http.GetFromJsonAsync<PageDto[]>($"api/sections/{f.Sections[0]}/pages"))![0]; await ConfigureCancellationEditor(f, editor, transport, page);
        await using var coordinator = new EditorSaveCoordinator(new RecoveryDraftService(new CancellationEditorJs(page.Content)), NullLogger<EditorSaveCoordinator>.Instance);
        var pageEditor = CancellationPage(editor, page, coordinator);
        if (failure == "embedded") ((CancellationEditorJs)typeof(PageEditor).GetProperty("JSRuntime", TranslationPrivate)!.GetValue(pageEditor)!).TargetedRangeValid = false;
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new TranslationRenderer(services, services.GetRequiredService<ILoggerFactory>()); await renderer.Attach(editor);
        TranslationSet(editor, "_targetedStrictRetry", true); await CancellationFlow(renderer, editor, "quality");
        var issue = ((List<PageQualityIssueDto>)TranslationField(editor, "_qualityIssues")!).First(i => i.RuleId == rule);
        if (failure == "anchor") issue = issue with { AnchorText = "another anchor" };
        if (failure == "stale") provider.After = async () => { await f.Http.PutAsJsonAsync($"api/pages/{page.Id}", new { content = "Changed synthetic writing" }); };
        if (failure == "account") provider.After = () => { TranslationSet(editor, "_webTranslationGeneration", 1); return Task.CompletedTask; };
        await TranslationEvent(renderer, editor, "OpenQualityProposalAsync", issue);
        Assert.Equal(failure is "anchor" or "embedded" ? 0 : failure == "429-between" ? 2 : 1, failing.Calls);
        Assert.Equal(failure == "valid", (bool)TranslationField(editor, "_isQualityProposalOpen")!);
        if (failure != "stale") await AssertCancellationWritingUnchanged(f);
        await using var db = new AppDbContext(f.Options); Assert.Empty(await db.WebAiHistoryOperations.ToListAsync());
    }
    [Fact]
    public async Task ActualClientCancelledFirstInvalidResponseCannotScheduleOptedInRetry()
    {
        await using var f = new TranslationFixture { Configure = s => s.AddScoped<IQualityCheckService, QualityCheckService>() };
        f.Html[0] = "<p>" + TargetedQualityRetryTests.Passive + "</p>"; await f.Start();
        using var provider = new CheckedProvider(f) { OutputForCall = (_, _) => TargetedQualityRetryTests.Passive };
        using var editor = CheckedEditor(f, provider); var gate = new RequestGate("/execute"); using var transport = new CancellationTransport(provider, gate);
        var page = (await f.Http.GetFromJsonAsync<PageDto[]>($"api/sections/{f.Sections[0]}/pages"))![0]; await ConfigureCancellationEditor(f, editor, transport, page);
        await using var coordinator = new EditorSaveCoordinator(new RecoveryDraftService(new CancellationEditorJs(page.Content)), NullLogger<EditorSaveCoordinator>.Instance);
        CancellationPage(editor, page, coordinator); using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new TranslationRenderer(services, services.GetRequiredService<ILoggerFactory>()); await renderer.Attach(editor);
        TranslationSet(editor, "_targetedStrictRetry", true); await CancellationFlow(renderer, editor, "quality");
        var issue = ((List<PageQualityIssueDto>)TranslationField(editor, "_qualityIssues")!).First(i => i.RuleId == "style.passive_voice");
        var run = TranslationEvent(renderer, editor, "OpenQualityProposalAsync", issue);
        await gate.Ready.Task.WaitAsync(TimeSpan.FromSeconds(10)); await CancelRequest(renderer, editor); gate.Release.TrySetResult(); await run;
        Assert.Equal(1, provider.Calls); Assert.False((bool)TranslationField(editor, "_isQualityProposalOpen")!);
        await AssertCancellationWritingUnchanged(f);
    }
}
