using System.Text.Json;
using WriterApp.Shared;
using Xunit;

namespace WriterApp.Tests;

public sealed class StyleQualityReviewTests
{
    private const string Source = "🧭 Elin walked very slowly.\n\nShe was really tired.";
    private static StyleQualityEdit Edit(string original = "Elin walked very slowly.", string replacement = "Elin walked slowly.") =>
        new(original, replacement, "word_choice", "preference", "Remove an unnecessary intensifier.", "Slightly less emphasis on her pace.");
    [Fact]
    public void PartialApprovalPreservesEveryOtherPassageAndParagraphBreak()
    {
        var report = StyleQualityReview.Parse(JsonSerializer.Serialize(new StyleQualityReport([Edit(), Edit("She was really tired.", "She was tired.")])), Source);
        Assert.Equal("🧭 Elin walked slowly.\n\nShe was really tired.", StyleQualityReview.Compose(report, Source, [0]));
        Assert.Equal("🧭 Elin walked very slowly.\n\nShe was tired.", StyleQualityReview.Compose(report, Source, [1]));
        Assert.Equal(Source, StyleQualityReview.Compose(report, Source, []));
        Assert.Throws<InvalidDataException>(() => StyleQualityReview.Compose(report, Source, [2]));
        Assert.Throws<InvalidDataException>(() => StyleQualityReview.Compose(report, "Changed writing", [0]));
    }
    [Theory]
    [InlineData("foreign")][InlineData("ambiguous")][InlineData("overlap")][InlineData("missing_reason")]
    [InlineData("unknown_criterion")][InlineData("unknown_kind")][InlineData("paragraphs")][InlineData("metadata")][InlineData("unchanged")]
    public void UnsafeOrUnexplainedFindingsAreRejected(string failure)
    {
        string source = Source; var edit = Edit(); IReadOnlyList<StyleQualityEdit> edits = [edit];
        switch (failure) {
            case "foreign": edits = [Edit("Another passage.")]; break;
            case "ambiguous": source += " " + edit.Original; break;
            case "overlap": edits = [edit, Edit("very slowly", "slowly")]; break;
            case "missing_reason": edits = [edit with { Reason = "" }]; break;
            case "unknown_criterion": edits = [edit with { Criterion = "overall_quality" }]; break;
            case "unknown_kind": edits = [edit with { Kind = "mandatory" }]; break;
            case "paragraphs": edits = [edit with { Replacement = "Elin walked.\n\nSlowly." }]; break;
            case "metadata": edits = [edit with { Replacement = "Here is the revised text: Elin walked slowly." }]; break;
            case "unchanged": edits = [edit with { Replacement = edit.Original }]; break;
        }
        Assert.Throws<InvalidDataException>(() => StyleQualityReview.Parse(JsonSerializer.Serialize(new StyleQualityReport(edits)), source));
    }
    [Theory]
    [InlineData("plain rewrite")][InlineData("""{"edits":[],"Edits":[]}""")][InlineData("""{"edits":[],"score":90}""")]
    [InlineData("""{"edits":null}""")][InlineData("""{"edits":[null]}""")][InlineData("""{}""")] 
    public void MalformedOrUnexpectedReportFieldsFailClosed(string json) =>
        Assert.Throws<InvalidDataException>(() => StyleQualityReview.Parse(json, Source));
    [Fact]
    public void EmptyFindingsAndEachExplicitGoalAreSupported()
    {
        var report = StyleQualityReview.Parse("""{"edits":[]}""", Source);
        Assert.Equal(Source, StyleQualityReview.Compose(report, Source, []));
        Assert.Contains("minimal edits", StyleQualityReview.Instruction("polish"));
        Assert.Contains("Reduce unnecessary wording", StyleQualityReview.Instruction("concise"));
        Assert.Contains("do not invent facts", StyleQualityReview.Instruction("vivid"));
        Assert.Throws<InvalidDataException>(() => StyleQualityReview.Instruction("unknown"));
    }
}
