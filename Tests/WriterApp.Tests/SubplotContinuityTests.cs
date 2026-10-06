using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WriterApp.Device.Shared.Services;
using WriterApp.Shared;
using WriterApp.Shared.Sync;
using WriterApp.UI.Shared;
using Xunit;

namespace WriterApp.Tests;

public sealed class SubplotContinuityTests
{
    private static SyncSceneCard Card(string summary, string tags) => new(null,null,null,null,"She keeps the letter",null,summary,"Draft",null,null,null,null,null,tags,null);
    private static string Context()
    {
        var chapter = Guid.NewGuid();
        return StoryboardAnalysis.Build("The return", [new(chapter,null,0,"chapter","Homecoming"),
            new(Guid.NewGuid(),chapter,0,"scene","The letter",Card("Mara finds her father's letter.","[\"Family secret\"]")),
            new(Guid.NewGuid(),chapter,1,"scene","The reunion",Card("Mara reunites with Jonas without discussing the letter.","[\"Family secret\"]"))]);
    }

    [Fact]
    public async Task DesktopRequestDecodesTagsOrdersHierarchyAndExcludesDeletedSubtrees()
    {
        using var fixture = new TranslationTestFixture();
        var doc = await fixture.Create(); var chapter = doc.Project!.Nodes.Single(n => n.NodeType == "chapter");
        var scene = doc.Project.Nodes.Single(n => n.NodeType == "scene");
        var firstPart = Guid.NewGuid(); var secondPart = Guid.NewGuid(); var otherChapter = Guid.NewGuid();
        var deadChapter = Guid.NewGuid();
        doc = doc with { Project = doc.Project with { Nodes = [
            new() { NodeId=secondPart,NodeType="part",Title="Part two",OrderIndex=1 },
            new() { NodeId=otherChapter,ParentId=secondPart,NodeType="chapter",Title="Second chapter",OrderIndex=0 },
            scene with { ParentId=chapter.NodeId,Card=Card("Current summary","[\" Secret \",\"secret\"]") },
            chapter with { ParentId=firstPart,Title="First chapter",OrderIndex=9 },
            new() { NodeId=firstPart,NodeType="part",Title="Part one",OrderIndex=0 },
            new() { NodeId=deadChapter,NodeType="chapter",Title="Deleted chapter",DeletionId=Guid.NewGuid() },
            new() { NodeId=Guid.NewGuid(),ParentId=deadChapter,NodeType="scene",Title="Hidden scene" }
        ]}};
        var prepared = AdvancedAiRequests.Build(doc,doc.Sections[0].SectionId,AdvancedAiAction.Storyboard);
        string context = prepared.Request.Request.Parameters!["storyboard_context"]!.ToString()!;
        using var json = JsonDocument.Parse(context); var chapters = json.RootElement.GetProperty("chapters");
        Assert.Equal("First chapter",chapters[0].GetProperty("title").GetString());
        Assert.Equal("Second chapter",chapters[1].GetProperty("title").GetString());
        Assert.Equal(2,chapters.GetArrayLength());
        var current = chapters[0].GetProperty("scenes")[0];
        Assert.Equal("Current summary",current.GetProperty("summary").GetString());
        Assert.Equal("Secret",Assert.Single(current.GetProperty("subplotTags").EnumerateArray()).GetString());
        Assert.DoesNotContain("Hidden scene",context); Assert.DoesNotContain("SubplotTagsJson",context);
    }

    [Theory]
    [InlineData("{\"findings\":[]}","No findings returned")]
    [InlineData("{\"assessment\":\"no_issues_found\",\"summary\":\"Both threads are supported by the plan.\",\"findings\":[]}","No continuity issues identified")]
    [InlineData("{\"assessment\":\"needs_context\",\"summary\":\"Add scene summaries.\",\"findings\":[]}","More story context needed")]
    [InlineData("not json","Unable to read this report")]
    [InlineData("{\"findings\":[{}]}","Unable to read this report")]
    public async Task SharedPreviewDistinguishesEmptyInsufficientAndInvalidReports(string response,string expected)
    {
        string html = await Render(response,Context());
        Assert.Contains(expected,html); Assert.DoesNotContain("ai-preview-columns",html);
        Assert.DoesNotContain("ORIGINAL",html); Assert.Contains("Analysis coverage",html);
        Assert.Contains("Storyboard included in this check",html);
    }

    [Fact]
    public async Task SparseBoardCannotPresentEmptyResponseAsSuccessfulContinuityCheck()
    {
        var chapter = Guid.NewGuid();
        string context = StoryboardAnalysis.Build("Empty",[new(chapter,null,0,"chapter","Chapter"),new(Guid.NewGuid(),chapter,0,"scene","Scene")]);
        string html = await Render("{\"findings\":[]}",context);
        Assert.Contains("More story context needed",html); Assert.Contains("Titles alone cannot establish continuity",html);
        await Evidence("needs-context",html);
    }

    [Fact]
    public async Task FindingsRenderReadableIssueNamesSceneReferencesAndEscapedRecommendations()
    {
        string response = JsonSerializer.Serialize(new { assessment="issues_found",summary="One thread needs attention.",findings=new[]{new {
            subplotName="Family secret",issueType="introduced_not_developed",explanation="The letter is introduced but never revisited in the scene plan.",
            affectedScenes=new[]{"Homecoming / The letter","Homecoming / The reunion"},recommendation="Let Mara decide whether to share the letter. <script>alert(1)</script>" }} });
        string html = await Render(response,Context());
        Assert.Contains("Needs development",html); Assert.Contains("Suggested next step",html); Assert.Contains("The reunion",html);
        Assert.DoesNotContain("<script>",html); Assert.Contains("&lt;script&gt;",html); Assert.DoesNotContain("introduced_not_developed",html);
        await Evidence("findings",html);
    }

    private static async Task<string> Render(string response,string context)
    {
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () => (await renderer.RenderComponentAsync<AiResultPreview>(ParameterView.FromDictionary(new Dictionary<string,object?> {
            [nameof(AiResultPreview.ActionKey)]="storyboard.check-subplot-continuity",[nameof(AiResultPreview.Proposed)]=response,
            [nameof(AiResultPreview.Original)]="Unrelated scene writing",[nameof(AiResultPreview.StoryboardContext)]=context
        }))).ToHtmlString());
    }
    private static async Task Evidence(string name,string html)
    {
        if (Environment.GetEnvironmentVariable("WRITERAPP_SUBPLOT_EVIDENCE") is { } root)
            await File.WriteAllTextAsync(Path.Combine(root,name+".html"),html);
    }
}
