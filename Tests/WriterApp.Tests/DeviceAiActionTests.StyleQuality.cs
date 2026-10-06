using System.Text.Json;
using WriterApp.Device.Shared.Services;
using WriterApp.Shared;
using Xunit;

namespace WriterApp.Tests;
public sealed partial class DeviceAiActionTests
{
    [Theory]
    [InlineData("polish")][InlineData("concise")][InlineData("vivid")]
    public async Task DeviceStyleRequestReturnsValidatedExplainedChangesRatherThanJsonWriting(string goal)
    {
        var (document, section, page) = Source();
        var prepared = DeviceAiRequests.Build(document, section, page, Selected, DeviceAiAction.StyleQuality, styleGoal: goal);
        Assert.Equal(goal, prepared.Request.Parameters![StyleQualityReview.Parameter]);
        var report = new StyleQualityReport([new("beta", "gamma", "word_choice", "preference", "Use a more precise word.", "Changes emphasis.")]);
        var api = new FakeApi { OnExecute = _ => Task.FromResult(Response(prepared.Key) with { ProposedText = JsonSerializer.Serialize(report), ChangesSummary = "Run custom prompt" }) };
        var proposal = await new DeviceAiService(api, await SignedInAsync(), new()).ProposeAsync(prepared, default);
        Assert.Equal("gamma", proposal.ProposedText);
        Assert.Equal("beta", proposal.SourceText);
        Assert.NotNull(proposal.StyleReview);
        Assert.Equal("Changes emphasis.", proposal.StyleReview!.Edits[0].Tradeoff);
        Assert.Null(proposal.Summary);
        Assert.Equal("<p>Alpha beta</p>", page.Content);
    }
    [Theory]
    [InlineData("plain rewrite")][InlineData("""{"edits":[{"original":"unknown","replacement":"changed","criterion":"clarity","kind":"preference","reason":"Clearer","tradeoff":"None"}]}""")]
    public async Task DeviceRejectsUnsupportedOrUnanchoredStyleOutputBeforeReview(string output)
    {
        var (document, section, page) = Source();
        var prepared = DeviceAiRequests.Build(document, section, page, Selected, DeviceAiAction.StyleQuality);
        var api = new FakeApi { OnExecute = _ => Task.FromResult(Response(prepared.Key) with { ProposedText = output }) };
        var account = await SignedInAsync();
        await Assert.ThrowsAsync<InvalidDataException>(() => new DeviceAiService(api, account, new()).ProposeAsync(prepared, default));
    }
    [Fact]
    public async Task OldBackendCannotChargeForAnUnsupportedStyleReview()
    {
        var (document, section, page) = Source();
        var prepared = DeviceAiRequests.Build(document, section, page, Selected, DeviceAiAction.StyleQuality);
        var api = new FakeApi { Usage = new() { AiEnabled = true, UiEnabled = true, SupportsDocumentVersionChecks = true, QuotaRemaining = 100 } };
        var account = await SignedInAsync();
        var error = await Assert.ThrowsAsync<DeviceAiException>(() => new DeviceAiService(api, account, new()).ProposeAsync(prepared, default));
        Assert.Contains("explained style reviews", error.Message); Assert.Equal(0, api.ExecuteCount);
    }
    [Fact]
    public void SelectionAtBeginningOfPageIsNotLabeledAsTheWholePage()
    {
        var (document, section, page) = Source();
        var capture = Selected with { SelectedText = "Alpha", SelectionStart = 0, SelectionEnd = 5, From = 1, To = 6 };
        Assert.Equal(DeviceAiApplyTarget.ManuscriptSelection, DeviceAiRequests.Build(document, section, page, capture, DeviceAiAction.StyleQuality).Target);
    }
}
