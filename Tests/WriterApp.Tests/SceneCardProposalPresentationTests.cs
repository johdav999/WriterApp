using AngleSharp.Html.Parser;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WriterApp.Application.Documents;
using WriterApp.UI.Shared;
using Xunit;

namespace WriterApp.Tests;

public sealed class SceneCardProposalPresentationTests
{
    private const string Report = """
        {"narrativeRole":"Setup","narrativeIntent":"Introduce Elin's return to Vinterhamn and establish a tension-filled reunion with Jonas.","emotionalBeat":"A mix of nostalgia, unease, and subtle warmth.","keyEvents":"Elin arrives at Vinterhamn station after eleven years.","openQuestions":"Why did Elin leave Vinterhamn?","povCharacterId":"Elin","placeId":"Vinterhamn Station","timelineEventId":"arrival_at_vinterhamn","tags":["unspoken_tension"],"references":[{"kind":"character","targetId":"Jonas"}],"explanation":"This scene establishes location, time, and character dynamics."}
        """;

    [Theory]
    [InlineData("scene.suggest")]
    [InlineData("scene.refine")]
    public async Task ReportedSceneSuggestionShowsFieldUpdatesInsteadOfJson(string action)
    {
        var html = await Render(new() { ["ActionKey"] = action, ["Original"] = "null", ["Proposed"] = Report });
        var markup = new HtmlParser().ParseDocument(html);
        Assert.Equal(7, markup.QuerySelectorAll("tbody tr").Length);
        Assert.Contains("No scene details saved yet", markup.Body!.TextContent);
        Assert.Contains("Narrative role", markup.Body.TextContent);
        Assert.Contains("Introduce Elin's return", markup.Body.TextContent);
        Assert.Contains("Why did Elin leave", markup.Body.TextContent);
        Assert.Contains("This scene establishes", markup.Body.TextContent);
        Assert.Contains("Unchanged", markup.Body.TextContent); // Missing summary is preserved.
        Assert.DoesNotContain("narrativeRole", html);
        Assert.DoesNotContain("arrival_at_vinterhamn", html);
        Assert.DoesNotContain("null", markup.Body.TextContent);
        Assert.DoesNotContain("role=\"alert\"", html);
    }

    [Fact]
    public async Task OriginalAuthoredFieldsAndTypedOnlyProposalsAreReadableAndInert()
    {
        string html = await Render(new() { ["ActionKey"] = "scene.suggest", ["Original"] = "{\"Summary\":\"Original <summary>\",\"NarrativeIntent\":\"Keep original_intent\"}",
            ["Proposed"] = "Provider description", ["SceneCard"] = new SectionSceneCardProposalDto(null, null, null, null, Summary: "<script>alert(1)</script>") });
        Assert.Contains("Original &lt;summary&gt;", html);
        Assert.Contains("Keep original_intent", html);
        Assert.Contains("&lt;script&gt;", html);
        Assert.DoesNotContain("<script>", html);
        Assert.DoesNotContain("Provider description", html);
    }

    [Theory]
    [InlineData("{}")] [InlineData("null")] [InlineData("[]")] [InlineData("broken")]
    [InlineData("{\"explanation\":\"No fields\"}")]
    public async Task UnusableResponseShowsErrorInsteadOfRawJsonOrAnEmptyUpdate(string response)
    {
        Assert.False(SceneCardAiProposalParser.TryParse(response, out _, out _));
        string html = await Render(new() { ["ActionKey"] = "scene.suggest", ["Proposed"] = response });
        Assert.Contains("role=\"alert\"", html);
        Assert.Contains("could not be read", html);
        Assert.DoesNotContain("<table", html);
    }

    [Fact]
    public void PartialNestedAndFencedResponsesPreserveOmittedFields()
    {
        Assert.True(SceneCardAiProposalParser.TryParse("```json\n{\"sceneCard\":{\"summary\":\"A reunion.\"},\"explanation\":\"Why\"}\n```", out var card, out var explanation));
        Assert.Equal("A reunion.", card!.Summary);
        Assert.Null(card.EmotionalBeat); Assert.Null(card.KeyEvents); Assert.Null(card.OpenQuestions);
        Assert.Null(card.NarrativePurpose); Assert.Null(card.NarrativeRole); Assert.Null(card.NarrativeIntent);
        Assert.Null(card.Status); Assert.Equal("Why", explanation);
    }

    private static async Task<string> Render(Dictionary<string, object?> parameters)
    {
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () => (await renderer.RenderComponentAsync<AiResultPreview>(ParameterView.FromDictionary(parameters))).ToHtmlString());
    }
}
