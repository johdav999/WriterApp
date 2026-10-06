using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using AngleSharp.Html.Parser;
using WriterApp.Device.Shared.Components;
using WriterApp.Device.Shared.Services;
using WriterApp.Device.Shared.Storage;
using WriterApp.Shared.Quality;
using Xunit;

namespace WriterApp.Tests;

public sealed partial class LocalQualityPanelTests
{
    private readonly GlossaryHandler _glossary = new();
    [Fact]
    public async Task ActualPanelApprovesCachedCasingFixOfflineAndHistoryReopensUndoesAndRedoesOnlyThatPassage()
    {
        await _account.SignInAsync(); var (services, renderer, root) = await Render("elin visited Rostok.");
        await using var ownedServices = services; await using var ownedRenderer = renderer;
        await Event(renderer, "Analyze"); _network.SetOnline(false); await Event(renderer, "Analyze");
        var original = Analysis.Source;
        var issue = Analysis.Issues.Single(i => i.RuleId == "terminology.glossary" && i.Fix is not null);
        await Event(renderer, "Review", issue.IssueKey);
        Assert.Equal(original.Sections[0].Pages[0].Content, (await Repository.LoadAsync(original.DocumentId))!.Sections[0].Pages[0].Content);
        Assert.Empty(await History.HistoryAsync(original.DocumentId));
        await Event(renderer, "Apply");
        var saved = (await Repository.LoadAsync(original.DocumentId))!;
        Assert.Equal("<p>Elin visited Rostok.</p>", saved.Sections[0].Pages[0].Content);
        var entry = Assert.Single(await History.HistoryAsync(saved.DocumentId)); Assert.Equal("Applied", entry.Status);
        var reopened = new LocalAiHistoryActions(Repository, History);
        await reopened.ChangeAsync(saved.DocumentId, entry.Id, false);
        Assert.Equal(original.Sections[0].Pages[0].Content, (await Repository.LoadAsync(saved.DocumentId))!.Sections[0].Pages[0].Content);
        await new LocalAiHistoryActions(Repository, History).ChangeAsync(saved.DocumentId, entry.Id, true);
        Assert.Equal(saved.Sections[0].Pages[0].Content, (await Repository.LoadAsync(saved.DocumentId))!.Sections[0].Pages[0].Content);
        Assert.Equal(1, _glossary.Calls); Assert.Equal(0, _api.Requests);
    }
    [Fact]
    public async Task ActualQualityPanelShowsFreshAndCachedGlossaryChecksAndAccountSwitchClearsPrivateFindings()
    {
        await _account.SignInAsync(); var (services, renderer, root) = await Render("🧭 elin visited Rostok.");
        await using var ownedServices = services; await using var ownedRenderer = renderer;
        Assert.Equal(0, _glossary.Calls);
        await Event(renderer, "Analyze");
        Assert.Equal(GlossaryAvailability.Verified, Analysis.Glossary!.Availability);
        Assert.Equal(2, Analysis.Issues.Count(i => i.RuleId == "terminology.glossary"));
        var original = LocalDocumentCodec.Encode(Analysis.Source);
        string html = await renderer.Dispatcher.InvokeAsync(root.ToHtmlString);
        Assert.Contains("Saved glossary loaded", html); Assert.Contains("Last successful glossary check", html); Assert.Contains("2 saved terms", html);
        await GlossaryEvidence("fresh", html);
        _network.SetOnline(false); await Event(renderer, "Analyze");
        Assert.Equal(GlossaryAvailability.CachedOffline, Analysis.Glossary!.Availability); Assert.Equal(1, _glossary.Calls);
        Assert.Contains("Using cached glossary offline", await renderer.Dispatcher.InvokeAsync(root.ToHtmlString));
        await GlossaryEvidence("offline", await renderer.Dispatcher.InvokeAsync(root.ToHtmlString));
        _network.SetOnline(true); _glossary.Fail = true; await Event(renderer, "Analyze");
        Assert.Equal(GlossaryAvailability.CachedRefreshFailed, Analysis.Glossary!.Availability);
        Assert.Contains("may be out of date", await renderer.Dispatcher.InvokeAsync(root.ToHtmlString));
        await GlossaryEvidence("failed", await renderer.Dispatcher.InvokeAsync(root.ToHtmlString));
        _glossary.Fail = false; _glossary.Terms = []; await Event(renderer, "Analyze");
        Assert.Equal(GlossaryAvailability.Verified, Analysis.Glossary!.Availability); Assert.DoesNotContain(Analysis.Issues, i => i.RuleId == "terminology.glossary");
        Assert.Contains("verified empty", await renderer.Dispatcher.InvokeAsync(root.ToHtmlString));
        await GlossaryEvidence("empty", await renderer.Dispatcher.InvokeAsync(root.ToHtmlString));
        _glossary.Terms = ["Elin", "Rostock"]; await Event(renderer, "Analyze");
        var doc = Analysis.Source; var casing = Analysis.Issues.Single(i => i.RuleId == "terminology.glossary" && i.Fix is not null);
        await Event(renderer, "Review", casing.IssueKey);
        Assert.Contains("Approve &amp; apply quality fix", await renderer.Dispatcher.InvokeAsync(root.ToHtmlString));
        await GlossaryEvidence("review", await renderer.Dispatcher.InvokeAsync(root.ToHtmlString));
        await renderer.Dispatcher.InvokeAsync(() => _account.SignOutAsync());
        html = await renderer.Dispatcher.InvokeAsync(root.ToHtmlString);
        Assert.Null(typeof(LocalQualityPanel).GetField("_analysis", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_components.Panel));
        Assert.Empty(_highlights); Assert.DoesNotContain("Approve &amp; apply", html); Assert.DoesNotContain("Rostock", html);
        Assert.Equal(original, LocalDocumentCodec.Encode((await Repository.LoadAsync(doc.DocumentId))!)); Assert.Empty(await History.HistoryAsync(doc.DocumentId));
        Assert.Equal(0, _api.Requests);
        await Event(renderer, "Analyze"); Assert.Equal(GlossaryAvailability.Unavailable, Analysis.Glossary!.Availability); Assert.Empty(Analysis.Glossary.Terms);
        await GlossaryEvidence("unavailable", await renderer.Dispatcher.InvokeAsync(root.ToHtmlString));
    }
    [Theory][InlineData("account")][InlineData("writing")][InlineData("document")][InlineData("page")]
    public async Task ActualQualityPanelRejectsLateGlossaryResultsAfterSourceOrAccountSwitch(string change)
    {
        await _account.SignInAsync(); var (services, renderer, root) = await Render("elin visited Rostok.");
        await using var ownedServices = services; await using var ownedRenderer = renderer;
        _glossary.BeforeReply = async () => {
            if (change == "account") await _account.SignOutAsync();
            if (change == "document") typeof(LocalQualityPanel).GetProperty(nameof(LocalQualityPanel.DocumentId))!.SetValue(_components.Panel, Guid.NewGuid());
            if (change == "page") typeof(LocalQualityPanel).GetProperty(nameof(LocalQualityPanel.PageId))!.SetValue(_components.Panel, Guid.NewGuid());
            if (change == "writing") {
                var doc = (await Repository.ListAsync()).Documents.Single();
                await Repository.SaveAsync(doc with { Sections = doc.Sections.Select(s => s with {
                    Pages = s.Pages.Select(p => p with { Content = "<p>New writing.</p>" }).ToArray() }).ToArray() });
            }
        };
        await Event(renderer, "Analyze"); Assert.Null(Analysis); Assert.Empty(_highlights);
        Assert.DoesNotContain("Saved glossary loaded", await renderer.Dispatcher.InvokeAsync(root.ToHtmlString)); Assert.Equal(0, _api.Requests);
    }
    private sealed class GlossaryHandler : HttpMessageHandler
    {
        public int Calls; public bool Fail; public string[] Terms = ["Elin", "Rostock"]; public Func<Task>? BeforeReply;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++; if (BeforeReply is not null) await BeforeReply();
            if (Fail) throw new HttpRequestException("Synthetic refresh failure");
            var id = Guid.Parse(request.RequestUri!.AbsolutePath.Split('/')[3]);
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(DeviceGlossarySnapshot.Create(id, Terms)) };
        }
    }
    private static async Task GlossaryEvidence(string state, string html)
    {
        string? root = Environment.GetEnvironmentVariable("WRITERAPP_PARITY_P04_EVIDENCE");
        if (root is null) return;
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, "quality-" + state + ".html"), html);
    }
}
