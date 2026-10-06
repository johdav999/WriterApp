using WriterApp.Shared;
using Xunit;
namespace WriterApp.Tests;
public sealed class RecommendedWritingTests
{
    public static IEnumerable<object[]> Catalog => RecommendedWriting.Catalog.Select(t => new object[] { t.Id });
    [Theory][MemberData(nameof(Catalog))]
    public void CatalogUsesExactTemplatesAndTypedOutputSpecificTargets(string id) {
        var parameters = RecommendedWriting.Parameters(id); var run = RecommendedWriting.From(parameters)!;
        RecommendedWriting.ValidateParameters(parameters, run);
        Assert.Equal(RecommendedWriting.Tool(id).PromptTemplate.UserTemplate, parameters["template"]);
        Assert.Equal(RecommendedWriting.Tool(id).PromptTemplate.SystemTemplate, parameters["systemTemplate"]);
        Assert.False(parameters.ContainsKey(ReusablePrompts.Parameter)); Assert.NotEmpty(RecommendedWriting.Target(id));
        parameters["systemTemplate"] = "Forged template"; Assert.Throws<InvalidDataException>(() => RecommendedWriting.ValidateParameters(parameters, run));
    }
    [Theory][InlineData("{}")] [InlineData("{\"items\":[\"one\"]}")] [InlineData("{\"items\":[],\"extra\":1}")] [InlineData("{\"items\":[],\"items\":[]}")]
    public void CandidateListsRefuseMalformedIncompleteAndDuplicateOutput(string json) => Assert.ThrowsAny<Exception>(() => RecommendedWriting.TextResult(json, "blog.generate_headlines", "Source prose."));
    [Fact]
    public void RecommendedAppendRejectsLeadingSourceEchoInsteadOfAppendingIt() {
        const string source = "The narrator watched the old clock while the rain fell against the windows and the visitor waited at the door.";
        string json = System.Text.Json.JsonSerializer.Serialize(new { items = new[] { source + " She finally opened the letter." } });
        Assert.Throws<InvalidDataException>(() => RecommendedWriting.TextResult(json, "novel.continue_scene", source));
        Assert.Throws<InvalidDataException>(() => RecommendedWriting.TextResult(json, "other.expand_idea", source));
    }
    [Fact]
    public void HookCannotReviseLaterParagraphsAndListsCannotReplaceManuscript() {
        var original = new WritingStructure(1, Guid.NewGuid(), Guid.NewGuid(), [new(Guid.NewGuid(), [new("0.0", "Opening."), new("1.0", "Later.")])]);
        var changed = original with { Pages = [original.Pages[0] with { Runs = [new("0.0", "New opening."), new("1.0", "Changed later.")] }] };
        Assert.Throws<InvalidDataException>(() => RecommendedWriting.Revision(WritingActions.Serialize(changed), original, "blog.improve_hook"));
        Assert.Throws<InvalidDataException>(() => RecommendedWriting.Revision(WritingActions.Serialize(original), original, "blog.generate_headlines"));
        Assert.ThrowsAny<Exception>(() => RecommendedWriting.Parse("{\"version\":2,\"toolId\":\"novel.deepen_character\"}"));
    }
}
