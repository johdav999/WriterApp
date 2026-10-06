using AngleSharp.Html.Parser;
using WriterApp.Device.Shared.Storage;
using WriterApp.Shared;
using Xunit;

namespace WriterApp.Tests;

public sealed partial class LocalWritingPanelTests
{
    [Fact]
    public async Task SelectionValidationShowsOnlyActionableMessageAndDoesNotCallProvider()
    {
        await using var h = new Harness(); await h.Start();
        const string message = "This passage spans different formatting, blocks or embedded content. Revise it manually to preserve that structure.";
        h.Interop.RangeError = message + "\nError: " + message + "\n    at Module.validateQualityRange (https://0.0.0.1/editor/device-editor.js:123:4)";
        await h.Event("PreviewAction");
        var html = new HtmlParser().ParseDocument(await h.Html());
        var alert = Assert.Single(html.QuerySelectorAll("[role='alert']"));
        Assert.Equal(message, alert.TextContent.Trim());
        Assert.DoesNotContain("device-editor.js", html.Body!.TextContent);
        Assert.DoesNotContain("Error:", html.Body.TextContent);
        Assert.Equal(0, h.Api.Calls);
        Assert.Equal(LocalDocumentCodec.Encode(h.Source), LocalDocumentCodec.Encode((await h.Fixture.Repository.LoadAsync(h.Source.DocumentId))!));
    }

    [Fact]
    public async Task RewritePreviewExplainsSelectionAndRemainsVisibleWhenProviderIsUnavailable()
    {
        await using var h = new Harness(); await h.Start();
        h.Api.Available = false; await h.Event("Refresh");
        var html = new HtmlParser().ParseDocument(await h.Html());
        var button = Assert.Single(html.QuerySelectorAll("button.writing-rewrite"));
        Assert.True(button.HasAttribute("disabled"));
        Assert.Contains("Select a passage, then choose an action.", html.Body!.TextContent);
        Assert.Contains("Refresh writing availability", html.Body.TextContent);
        await h.Event("PreviewAction"); Assert.Equal(0, h.Api.Calls);
        var evidence = Environment.GetEnvironmentVariable("WRITERAPP_REWRITE_EVIDENCE");
        if (evidence is not null) await File.WriteAllTextAsync(Path.Combine(evidence, "rewrite-unavailable.html"), await h.Html());
    }

    [Theory]
    [InlineData(WritingScope.Section)]
    [InlineData(WritingScope.Continuation)]
    public async Task ChangingTargetSelectsASupportedActionAndKeepsOnePreview(WritingScope scope)
    {
        await using var h = new Harness(); await h.Start();
        h.Set("_scope", scope); await h.Event("ScopeChanged");
        var html = new HtmlParser().ParseDocument(await h.Html());
        Assert.DoesNotContain(html.QuerySelectorAll(".writing-operations button"), b => b.TextContent == "Rewrite");
        if (scope == WritingScope.Section) {
            var preview = Assert.Single(html.QuerySelectorAll("button.writing-preview"));
            Assert.Equal("Preview expansion", preview.TextContent);
            Assert.Equal(0, h.Api.Calls);
            await h.Event("PreviewAction"); Assert.Equal("expand.section", h.Api.LastKey);
        } else {
            Assert.Empty(html.QuerySelectorAll("button.writing-preview"));
            Assert.Single(html.QuerySelectorAll("button"), b => b.TextContent == "Propose next paragraph");
            await h.Event("PreviewAction"); Assert.Equal(0, h.Api.Calls);
        }
    }
}
