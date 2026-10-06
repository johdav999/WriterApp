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
    public async Task ReviewOffersAnActionPerSuggestionAndMarksApprovedSuggestions()
    {
        string html = await Render<AiResultPreview>(new() {
            ["ActionKey"] = "continuity.check_section", ["Proposed"] = Report,
            ["ReviewConsistencySuggestion"] = EventCallback.Factory.Create<int>(new object(), _ => Task.CompletedTask),
            ["AppliedConsistencySuggestions"] = new[] { 1 },
            ["ConsistencySuggestionReview"] = (RenderFragment<int>)(index => builder => {
                if (index == 2) { builder.OpenElement(0, "section"); builder.AddAttribute(1, "aria-label", "Approve consistency suggestion"); builder.AddContent(2, "Approval preview"); builder.CloseElement(); }
            })
        });
        var buttons = new HtmlParser().ParseDocument(html).QuerySelectorAll(".consistency-finding button");
        Assert.Equal(3, buttons.Length);
        Assert.Equal("Review & apply suggestion", buttons[0].TextContent);
        Assert.False(buttons[0].HasAttribute("disabled"));
        Assert.Equal("Applied", buttons[1].TextContent);
        Assert.True(buttons[1].HasAttribute("disabled"));
        var findings = new HtmlParser().ParseDocument(html).QuerySelectorAll(".consistency-finding");
        Assert.Null(findings[0].QuerySelector("[aria-label='Approve consistency suggestion']"));
        Assert.NotNull(findings[2].QuerySelector("[aria-label='Approve consistency suggestion']"));
        string busy = await Render<AiResultPreview>(new() {
            ["ActionKey"] = "continuity.check_section", ["Proposed"] = Report, ["Busy"] = true,
            ["ReviewConsistencySuggestion"] = EventCallback.Factory.Create<int>(new object(), _ => Task.CompletedTask)
        });
        Assert.All(new HtmlParser().ParseDocument(busy).QuerySelectorAll("button"), b => Assert.True(b.HasAttribute("disabled")));
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
    [Fact]
    public async Task TwoPassagesCoverageAndIntentionalActionAreReadableAndKeepReviewControlsSafe()
    {
        const string json = """
        {"schemaVersion":"1.0","coverage":{"comparedSections":2,"omittedSections":1},"issues":[{"severity":"high","type":"character","message":"Eye color differs.","evidence":{"sectionId":"8272ab13-2a97-49bf-8713-b8f76935c438","quote":"Blue eyes."},"comparisonEvidence":{"sectionId":"8272ab13-2a97-49bf-8713-b8f76935c439","quote":"Brown eyes."},"suggestedFix":"Brown eyes."}]}
        """;
        string html = await Render<ConsistencyReport>(new() { ["ResponseJson"] = json,
            ["FindingLocation"] = (Func<int,string>)(_ => "Chapter 1"), ["ComparisonLocation"] = (Func<int,string>)(_ => "Chapter 3"),
            ["JumpComparison"] = EventCallback.Factory.Create<int>(new object(), _ => Task.CompletedTask),
            ["MarkIntentional"] = EventCallback.Factory.Create<int>(new object(), _ => Task.CompletedTask), ["IntentionalSuggestions"] = new[] { 0 },
            ["ReviewSuggestion"] = EventCallback.Factory.Create<int>(new object(), _ => Task.CompletedTask) });
        var markup = new HtmlParser().ParseDocument(html);
        Assert.Equal(new[] { "Blue eyes.", "Brown eyes." }, markup.QuerySelectorAll("blockquote").Select(e => e.TextContent));
        Assert.Contains("Chapter 1", html); Assert.Contains("Chapter 3", html); Assert.Contains("outside the comparison limit", html);
        Assert.Contains("View conflicting passage", html); Assert.Contains("Check this again", html);
        Assert.DoesNotContain("Review &amp; apply suggestion", html); Assert.DoesNotContain("schemaVersion", html);
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
    public async Task CanonShowsReadableFactsQuotesAndSceneNamesWithoutInternalIds()
    {
        const string id = "8272ab13-2a97-49bf-8713-b8f76935c438";
        var content = WriterApp.Shared.Canon.CanonContent.Parse(WriterApp.Shared.Canon.CanonKind.Character,
            "{\"schemaVersion\":\"1.0\",\"characters\":[{\"name\":\"Elin\",\"facts\":[{\"fact\":\"Elin arrives after sunset.\",\"evidence\":{\"sectionId\":\"" + id + "\",\"quote\":\"Elin stepped down.\"}}],\"traits\":[\"independent\"]}]}");
        string html = await Render<StoryCanonView>(new() { ["Kind"] = content.Kind, ["Content"] = content, ["Status"] = "Current",
            ["SceneNames"] = new Dictionary<string, string> { [id] = "Arrival at the station" } });
        string? evidence = Environment.GetEnvironmentVariable("WRITERAPP_P03_EVIDENCE");
        if (evidence is not null) await File.WriteAllTextAsync(Path.Combine(evidence, "canon-reference.html"), html);
        var markup = new HtmlParser().ParseDocument(html);
        Assert.Contains("Elin arrives after sunset.", markup.Body!.TextContent);
        Assert.Equal("Elin stepped down.", markup.QuerySelector("blockquote")!.TextContent);
        Assert.Contains("From your manuscript", markup.Body.TextContent);
        Assert.Contains("Facts", markup.Body.TextContent);
        Assert.DoesNotContain("sectionId", html);
        Assert.Contains("From scene: Arrival at the station", markup.Body.TextContent);
        Assert.DoesNotContain(id, html);
    }

    [Fact]
    public async Task TimelineUsesEnglishLabelsAndNamesIncludingHonestMissingLinkFallbacks()
    {
        var kind = WriterApp.Shared.Canon.CanonKind.Timeline;
        var timeline = WriterApp.Shared.Canon.CanonContent.Parse(kind, """
            {"schemaVersion":"1.0","events":[{"id":"evt_arrival","title":"Elin meets Jonas at the station","timeRef":"just after sunset","order":2,"locationId":"loc_vinterhamn_station","participants":["chr_elin","chr_jonas","chr_unknown_person"],"evidence":[{"sectionId":"8272ab13-2a97-49bf-8713-b8f76935c438","quote":"Elin met Jonas."}]}]}
            """);
        var contents = new Dictionary<WriterApp.Shared.Canon.CanonKind, WriterApp.Shared.Canon.CanonContent> {
            [kind] = timeline,
            [WriterApp.Shared.Canon.CanonKind.Character] = WriterApp.Shared.Canon.CanonContent.Parse(WriterApp.Shared.Canon.CanonKind.Character,
                """{"schemaVersion":"1.0","characters":[{"id":"chr_elin","name":"Elin"},{"id":"chr_jonas","name":"Jonas"}]}"""),
            [WriterApp.Shared.Canon.CanonKind.Place] = WriterApp.Shared.Canon.CanonContent.Parse(WriterApp.Shared.Canon.CanonKind.Place,
                """{"schemaVersion":"1.0","places":[{"id":"loc_vinterhamn_station","name":"Vinterhamn station"}]}""")
        };
        string html = await Render<StoryCanonView>(new() { ["Kind"] = kind, ["Content"] = timeline, ["Contents"] = contents });
        var markup = new HtmlParser().ParseDocument(html);
        Assert.Equal(new[] { "When", "Event number", "Where", "Who is there", "Evidence" }, markup.QuerySelectorAll(".canon-fields > dt").Select(e => e.TextContent));
        var people = markup.QuerySelectorAll(".canon-values > li span").Select(e => e.TextContent).ToArray();
        Assert.Contains("Elin", people); Assert.Contains("Jonas", people); Assert.Contains("Unknown Person (not linked)", people);
        Assert.Contains("Vinterhamn station", markup.Body!.TextContent);
        Assert.DoesNotContain("chr_", html); Assert.DoesNotContain("loc_", html); Assert.DoesNotContain("evt_", html);
        Assert.DoesNotContain("8272ab13", html);
        string? evidence = Environment.GetEnvironmentVariable("WRITERAPP_P03_EVIDENCE");
        if (evidence is not null) await File.WriteAllTextAsync(Path.Combine(evidence, "timeline-plain-english.html"), html);
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
