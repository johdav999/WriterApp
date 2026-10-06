using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using WriterApp.Device.Shared.Components;
using WriterApp.Device.Shared.Services;
using WriterApp.Device.Shared.Storage;
using WriterApp.Shared.Quality;
using Xunit;

namespace WriterApp.Tests;

public sealed partial class LocalQualityPanelTests
{
    private sealed class DecisionHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }

    [Fact]
    public async Task ActualPanelDurablyDismissesOneOccurrenceAndRestoresWithoutEditingWriting()
    {
        _network.SetOnline(false);
        var (services, renderer, root) = await Render("Elin carried carried the bag. Anna lifted lifted the box.");
        await using var ownedServices = services; await using var ownedRenderer = renderer;
        await Event(renderer, "Analyze");
        var a = Analysis; var issues = a.Issues.Where(i => i.RuleId == "style.repeated_words").ToArray();
        Assert.Equal(2, issues.Length);
        await Event(renderer, "Dismiss", issues[0].IssueKey);
        await Event(renderer, "Analyze");
        Assert.DoesNotContain(_highlights, h => h.IssueKey == issues[0].IssueKey);
        Assert.Contains(_highlights, h => h.IssueKey == issues[1].IssueKey);
        var html = await renderer.Dispatcher.InvokeAsync(root.ToHtmlString);
        Assert.Contains("Review dismissed findings (1)", html); Assert.Contains("Restore finding", html);
        var view = (LocalQualityDecisionView)typeof(LocalQualityPanel).GetField("_decisions", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_components.Panel)!;
        var scope = LocalBibleStore.ScopeKey(new("https://test.invalid/"), "anonymous");
        var reopened = await new LocalQualityDismissalStore(Path.Combine(_root, "quality-decisions")).ReadAsync(scope, a.Source.DocumentId, a.PageId);
        Assert.Equal(Assert.Single(view.Items), Assert.Single(reopened.Items));
        await Evidence("dismissed", html);
        await Event(renderer, "Restore", view.Items[0].Id);
        Assert.Contains(_highlights, h => h.IssueKey == issues[0].IssueKey);
        await Evidence("restored", await renderer.Dispatcher.InvokeAsync(root.ToHtmlString));
        Assert.Equal(a.Html, (await Repository.LoadAsync(a.Source.DocumentId))!.Sections[0].Pages[0].Content);
        Assert.Empty(await History.HistoryAsync(a.Source.DocumentId)); Assert.Equal(0, _api.Requests);
    }

    [Fact]
    public async Task ActualPanelAccountSwitchDropsDismissedPrivatePassagesAndUsesSeparateJournal()
    {
        _network.SetOnline(false);
        var (services, renderer, root) = await Render();
        await using var ownedServices = services; await using var ownedRenderer = renderer;
        await Event(renderer, "Analyze"); var key = Analysis.Issues[0].IssueKey;
        await Event(renderer, "Dismiss", key);
        await renderer.Dispatcher.InvokeAsync(() => _account.SignInAsync());
        Assert.DoesNotContain("Restore finding", await renderer.Dispatcher.InvokeAsync(root.ToHtmlString));
        await Event(renderer, "Analyze"); Assert.Contains(_highlights, h => h.IssueKey == key);
        await Event(renderer, "Dismiss", key);
        await Evidence("unmapped", await renderer.Dispatcher.InvokeAsync(root.ToHtmlString));
        await renderer.Dispatcher.InvokeAsync(() => _account.SignOutAsync());
        await Event(renderer, "Analyze"); Assert.DoesNotContain(_highlights, h => h.IssueKey == key);
    }

    private static async Task Evidence(string state, string html)
    {
        if (Environment.GetEnvironmentVariable("WRITERAPP_P10_EVIDENCE") is { } path)
            await File.WriteAllTextAsync(Path.Combine(path, state + ".html"), html);
    }
}
