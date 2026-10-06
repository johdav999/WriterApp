using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WriterApp.AI.Abstractions;
using WriterApp.AI.Actions;
using WriterApp.AI.Core;
using WriterApp.Application.Commands;
using WriterApp.Application.State;
using WriterApp.Domain.Documents;
using WriterApp.Shared;
using Xunit;

namespace WriterApp.Tests;
public sealed class StyleQualityExecutorTests
{
    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task ServerValidatesStyleFindingsAndNeverCreatesJsonReplacementOperations(bool invalid)
    {
        var document = DocumentFactory.CreateNewDocument(); var section = document.Chapters[0].Sections[0].SectionId;
        const string source = "Elin walked slowly.";
        var input = new AiActionInput(document, section, new TextRange(0, source.Length), source, "Review style", new() {
            ["template"] = "Polish style", ["scope"] = "selection", ["section_text_override"] = source, [StyleQualityReview.Parameter] = "concise"
        });
        var action = new CustomTransformAction();
        var request = action.BuildRequest(input);
        Assert.True((bool)request.Inputs["style_quality_review"]);
        string instruction = (string)request.Inputs["instruction"];
        Assert.Contains("Reduce unnecessary wording", instruction);
        Assert.Contains("grammar and punctuation", instruction);
        Assert.DoesNotContain("Return only revised text", instruction);
        var report = new StyleQualityReport([new(invalid ? "Wrong source" : source, "Elin walked.", "word_choice", "preference", "Remove an adverb.", "Less emphasis on pace.")]);
        var registry = new DefaultAiProviderRegistry([new Provider(JsonSerializer.Serialize(report))]);
        var router = new DefaultAiRouter(registry, Options.Create(new WriterAiOptions { Enabled = true }), NullLogger<DefaultAiRouter>.Instance);
        var result = await new AiActionExecutor(router, new InMemoryArtifactStore(), NullLogger<AiActionExecutor>.Instance).ExecuteAsync(action, input, default);
        if (invalid) { Assert.Null(result.Proposal); Assert.Equal("ai.style_review_rejected", result.ErrorCode); }
        else {
            Assert.NotNull(result.Proposal);
            Assert.Empty(result.Proposal!.Operations);
            Assert.Contains("More concise", result.Proposal.UserSummary);
            _ = StyleQualityReview.Parse(result.Proposal.ProposedText!, source);
        }
    }
    private sealed class Provider(string output) : IAiProvider
    {
        public string ProviderId => "style-review-test";
        public AiProviderCapabilities Capabilities => new(true, false);
        public Task<AiResult> ExecuteAsync(AiRequest request, CancellationToken ct) => Task.FromResult(new AiResult(
            request.RequestId, [new(Guid.NewGuid(), AiModality.Text, "application/json", output, null, null)], new(0, 0, TimeSpan.Zero), new()));
    }
}
