using System.Reflection;
using WriterApp.Application.Documents;
using WriterApp.Device.Shared.Components;
using WriterApp.Device.Shared.Services;
using WriterApp.Device.Shared.Storage;
using WriterApp.Shared;
using Xunit;

namespace WriterApp.Tests;

public sealed partial class LocalQualityPanelTests
{
    [Theory]
    [InlineData("style.repeated_words", false, false)][InlineData("style.repeated_words", true, false)][InlineData("style.repeated_words", true, true)]
    [InlineData("style.passive_voice", false, false)][InlineData("style.passive_voice", true, false)][InlineData("style.passive_voice", true, true)]
    [InlineData("readability.sentence_length", false, false)][InlineData("readability.sentence_length", true, false)][InlineData("readability.sentence_length", true, true)]
    public async Task ActualDesktopTargetedAdapterHonorsOptInAndExactlyOneSemanticRetry(string rule, bool optIn, bool secondValid)
    {
        var (before, valid) = TargetedQualityRetryTests.Text(rule); await _account.SignInAsync();
        var (services, renderer, root) = await Render(before); await using var ownedServices = services; await using var ownedRenderer = renderer;
        Assert.Contains(TargetedQualityRetry.Explanation, await renderer.Dispatcher.InvokeAsync(root.ToHtmlString));
        typeof(LocalQualityPanel).GetField("_strictRetry", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(_components.Panel, optIn);
        await Event(renderer, "Analyze"); var a = Analysis; var issue = a.Issues.First(i => i.RuleId == rule);
        _api.OutputForCall = (call, _) => call == 2 && secondValid ? valid : before;
        await Event(renderer, "Review", issue.IssueKey);
        var html = await renderer.Dispatcher.InvokeAsync(root.ToHtmlString);
        Assert.Equal(optIn ? 2 : 1, _api.Requests);
        if (optIn && secondValid) Assert.Contains("Approve &amp; apply quality fix", html);
        else { Assert.DoesNotContain("Approve &amp; apply quality fix", html); Assert.Contains("retry manually", html); }
        if (optIn) Assert.Contains(TargetedQualityRetry.StrictInstruction, _api.Inputs[1].Parameters!["instruction"]!.ToString());
        Assert.Equal(LocalDocumentCodec.Encode(a.Source), LocalDocumentCodec.Encode((await Repository.LoadAsync(a.Source.DocumentId))!));
        Assert.Empty(await History.HistoryAsync(a.Source.DocumentId));
        if (optIn && secondValid)
        {
            await Event(renderer, "Apply");
            var saved = (await Repository.LoadAsync(a.Source.DocumentId))!;
            Assert.Equal("<p>" + valid + "</p>", saved.Sections[0].Pages[0].Content);
            var entry = Assert.Single(await History.HistoryAsync(a.Source.DocumentId));
            Assert.Equal("Applied", entry.Status);
            Assert.Equal(valid, entry.Proposed);
            var undone = LocalAiHistoryActions.Change(saved, entry, false);
            Assert.Equal(a.Html, undone.Sections[0].Pages[0].Content);
            Assert.Equal(saved.Sections[0].Pages[0].Content,
                LocalAiHistoryActions.Change(undone, entry with { Status = "Undone" }, true).Sections[0].Pages[0].Content);
        }
    }
    [Theory][InlineData("style.repeated_words")][InlineData("style.passive_voice")][InlineData("readability.sentence_length")]
    public async Task ActualDesktopValidFirstResultNeverUsesExtraGeneration(string rule)
    {
        var (before, valid) = TargetedQualityRetryTests.Text(rule); await _account.SignInAsync(); var (services, renderer, root) = await Render(before);
        await using var ownedServices = services; await using var ownedRenderer = renderer;
        typeof(LocalQualityPanel).GetField("_strictRetry", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(_components.Panel, true);
        await Event(renderer, "Analyze"); _api.Output = valid;
        await Event(renderer, "Review", Analysis.Issues.First(i => i.RuleId == rule).IssueKey);
        Assert.Equal(1, _api.Requests); Assert.Contains("Approve &amp; apply", await renderer.Dispatcher.InvokeAsync(root.ToHtmlString));
    }
    [Theory][InlineData("network")][InlineData("quota")][InlineData("auth")][InlineData("stale")][InlineData("cancel")][InlineData("embedded")][InlineData("metadata")][InlineData("quota-between")]
    public async Task ActualDesktopNonSemanticFailuresCannotTriggerAutomaticGeneration(string failure)
    {
        if (failure != "auth") await _account.SignInAsync();
        var (services, renderer, root) = await Render(TargetedQualityRetryTests.Passive); await using var ownedServices = services; await using var ownedRenderer = renderer;
        typeof(LocalQualityPanel).GetField("_strictRetry", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(_components.Panel, true);
        await Event(renderer, "Analyze"); var a = Analysis; var issue = a.Issues.First(i => i.RuleId == "style.passive_voice");
        _api.Output = failure == "metadata" ? "{\"revisedText\":\"Anna opened the door.\"}" : TargetedQualityRetryTests.Passive;
        if (failure == "quota") _api.Quota = 0;
        if (failure == "embedded") { _js.FailureMethod = "validateTargetedQualityRange"; _js.Error = "Embedded content requires manual revision."; }
        _api.Wait = async ct => {
            if (failure == "network") throw new HttpRequestException("Synthetic network failure");
            if (failure == "quota-between") _api.Quota = 0;
            if (failure == "cancel") ((CancellationTokenSource)typeof(LocalQualityPanel).GetField("_cancel", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(_components.Panel)!).Cancel();
            if (failure == "stale") await Repository.SaveAsync(a.Source with { Sections = a.Source.Sections.Select(s => s with { Pages = s.Pages.Select(p => p with { Content = "<p>Changed writing</p>" }).ToArray() }).ToArray() });
        };
        await Event(renderer, "Review", issue.IssueKey);
        Assert.Equal(failure is "quota" or "auth" or "embedded" ? 0 : 1, _api.Requests);
        Assert.DoesNotContain("Approve &amp; apply", await renderer.Dispatcher.InvokeAsync(root.ToHtmlString));
        Assert.Empty(await History.HistoryAsync(a.Source.DocumentId));
    }
}
