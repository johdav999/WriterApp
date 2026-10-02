using AngleSharp.Html.Parser;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WriterApp.UI.Shared;
using Xunit;

namespace WriterApp.Tests;

public sealed class ConsistencyReportPresentationTests
{
    private const string Report = """
        {"schemaVersion":"1.0","issues":[{"severity":"low","type":"timeline","message":"The train\u0027s arrival shortly after sunset conflicts with the remaining orange sky reflected in the sea.","evidence":{"sectionId":"8272ab13-2a97-49bf-8713-b8f76935c438","quote":"The train reached Vinterhamn shortly after sunset."},"suggestedFix":"The train reached Vinterhamn as the sun dipped below the horizon, leaving the sky faintly tinged with orange reflected by the dark sea.","anchor":{"plainTextStart":0,"plainTextLength":43}},{"severity":"low","type":"character","message":"Elin\u0027s luggage feels heavier than expected but no explanation is given, potentially causing inconsistency.","evidence":{"sectionId":"8272ab13-2a97-49bf-8713-b8f76935c438","quote":"The train reached Vinterhamn shortly after sunset.  Elin stepped onto the platform carrying a suitcase that felt heavier than she remembered packing."},"suggestedFix":"Elin stepped onto the platform carrying a suitcase that felt surprisingly heavier than she recalled when packing it.","anchor":{"plainTextStart":44,"plainTextLength":78}},{"severity":"low","type":"character","message":"Elin\u0027s hesitation to take the later train is unexplained, leaving a gap in her motivation.","evidence":{"sectionId":"8272ab13-2a97-49bf-8713-b8f76935c438","quote":"At the far end of the platform stood the old station clock. It still ran three minutes slow.  Some things, apparently, refused to change."},"suggestedFix":"\u201CI thought you might take the later train,\u201D he said. \u201CI nearly did, but something held me back.\u201D Neither of them spoke of the reason.","anchor":{"plainTextStart":385,"plainTextLength":56}}]}
        """;

    [Fact]
    public async Task UserReportedResponseShowsEveryFindingEvidenceAndSuggestedRevision()
    {
        var html = await Render<AiResultPreview>(new() { ["ActionKey"] = "continuity.check_section", ["Proposed"] = Report, ["Original"] = "Original writing" });
        var markup = new HtmlParser().ParseDocument(html);
        Assert.Equal(3, markup.QuerySelectorAll(".consistency-finding").Length);
        Assert.Equal(3, markup.QuerySelectorAll("blockquote").Length);
        Assert.Equal(3, markup.QuerySelectorAll(".consistency-revision").Length);
        Assert.Contains("The train's arrival", markup.Body!.TextContent);
        Assert.Contains("Elin's luggage", markup.Body.TextContent);
        Assert.Contains("Elin's hesitation", markup.Body.TextContent);
        Assert.Contains("“I thought you might take the later train,”", markup.Body.TextContent);
        Assert.Contains("Low severity", markup.Body.TextContent);
        Assert.Contains("Timeline", markup.Body.TextContent);
        Assert.Contains("Character", markup.Body.TextContent);
        Assert.DoesNotContain("schemaVersion", html);
        Assert.DoesNotContain("plainTextStart", html);
        Assert.DoesNotContain("\\u0027", html);
        Assert.Empty(markup.QuerySelectorAll("button"));
    }

    [Fact]
    public async Task ExistingHistoryRecordsUseReadableReportWithoutRewritingStoredJson()
    {
        var entry = new AiHistoryItem(Guid.NewGuid(), "continuity.check_section", "Analysis", "Reviewed", DateTimeOffset.UtcNow, "Analyzed writing", Report);
        string html = await Render<AiHistoryPanel>(new() { ["Entries"] = new[] { entry } });
        Assert.Equal(3, new HtmlParser().ParseDocument(html).QuerySelectorAll(".consistency-finding").Length);
        Assert.DoesNotContain("schemaVersion", html);
        Assert.Equal(Report, entry.Proposed);
    }

    [Fact]
    public async Task EmptyReportShowsConfirmedNoIssues()
    {
        string html = await Render<ConsistencyReport>(new() { ["ResponseJson"] = "{\"schemaVersion\":\"1.0\",\"issues\":[]}" });
        Assert.Contains("No consistency issues found", html);
        Assert.DoesNotContain("role=\"alert\"", html);
    }

    [Theory]
    [InlineData("broken")]
    [InlineData("{}")]
    [InlineData("{\"schemaVersion\":\"1.0\",\"issues\":null}")]
    [InlineData("{\"schemaVersion\":\"1.0\",\"issues\":[null]}")]
    [InlineData("{\"schemaVersion\":\"1.0\",\"issues\":[{}]}")]
    [InlineData("{\"schemaVersion\":\"2.0\",\"issues\":[]}")]
    public async Task InvalidReportShowsReadableErrorInsteadOfCodeOrFalseSuccess(string json)
    {
        string html = await Render<ConsistencyReport>(new() { ["ResponseJson"] = json });
        Assert.Contains("role=\"alert\"", html);
        Assert.Contains("response could not be read", html);
        Assert.DoesNotContain("No consistency issues", html);
        Assert.DoesNotContain("schemaVersion", html);
    }

    [Fact]
    public async Task ModelTextIsRenderedAsInertText()
    {
        string html = await Render<ConsistencyReport>(new() { ["ResponseJson"] = Report.Replace("The train reached Vinterhamn shortly after sunset.", "<script>alert(1)</script>") });
        Assert.Contains("&lt;script&gt;", html);
        Assert.DoesNotContain("<script>", html);
    }

    [Fact]
    public async Task PlainTextAnalysisKeepsOriginalAndProposedText()
    {
        string html = await Render<AiResultPreview>(new() { ["ActionKey"] = "custom_transform", ["Original"] = "Source passage", ["Proposed"] = "Readable style suggestions" });
        Assert.Contains("Source passage", html);
        Assert.Contains("Readable style suggestions", html);
        Assert.DoesNotContain("Consistency findings", html);
    }

    private static async Task<string> Render<T>(Dictionary<string, object?> parameters) where T : IComponent
    {
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<T>(ParameterView.FromDictionary(parameters));
            return output.ToHtmlString();
        });
    }
}
