using System.Text.Json;
using WriterApp.Shared;
using WriterApp.Device.Shared.Storage;
using WriterApp.AI.Actions;
using WriterApp.AI.Abstractions;
using WriterApp.Application.Commands;
using WriterApp.Application.State;
using WriterApp.Domain.Documents;
using Xunit;
using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WriterApp.AI.Providers.OpenAI;

namespace WriterApp.Tests;
public sealed class ConsistencyChecksTests
{
    private readonly Guid _first = Guid.NewGuid(), _second = Guid.NewGuid();
    private string Report(Guid reference, string quote) => JsonSerializer.Serialize(new { schemaVersion = "1.0", issues = new[] {
        new { severity = "high", type = "character", message = "Eye color conflicts", evidence = new { sectionId = _first, quote = "Her eyes were blue." },
            comparisonEvidence = new { sectionId = reference, quote }, suggestedFix = "Her eyes were brown.", anchor = new { plainTextStart = 0, plainTextLength = 19 } }
    }});
    [Fact]
    public void ContradictorySourcePassagesSurvivePreparationAndQuotesMustMatchActualOwnedSections()
    {
        var sources = ConsistencyChecks.Sources([new(_first, "Arrival", "Her eyes were blue."), new(_second, "Later", "Her eyes were brown.")], _first);
        string json = ConsistencyChecks.WithVerifiedCoverage(Report(_second, "Her eyes were brown."), sources, _first);
        Assert.Equal("Her eyes were brown.", ConsistencyChecks.Comparison(json, 0)!.Quote);
        Assert.Throws<InvalidDataException>(() => ConsistencyChecks.WithVerifiedCoverage(Report(Guid.NewGuid(), "Her eyes were brown."), sources, _first));
        Assert.Throws<InvalidDataException>(() => ConsistencyChecks.WithVerifiedCoverage(Report(_second, "Invented quote"), sources, _first));
        Assert.Throws<InvalidDataException>(() => ConsistencyChecks.WithVerifiedCoverage(Report(_second, "Her eyes were brown.").Replace("Her eyes were blue.", "Invented primary"), sources, _first));
        Assert.Throws<InvalidDataException>(() => ConsistencyChecks.WithVerifiedCoverage("{\"schemaVersion\":\"1.0\",\"issues\":[{\"message\":\"Missing motivation\"}]}", sources, _first));
    }
    [Fact]
    public void OversizedComparisonSectionsAreDisclosedAndTargetIsNeverSilentlySkipped()
    {
        var sources = ConsistencyChecks.Sources([new(_second, "Long", new('a', 200_001)), new(_first, "Target", "Her eyes were blue.")], _first);
        Assert.Single(sources.Sections); Assert.Equal(_first, sources.Sections[0].SectionId); Assert.Equal(1, sources.OmittedSections);
        string json = ConsistencyChecks.WithVerifiedCoverage("{\"schemaVersion\":\"1.0\",\"issues\":[]}", sources, _first);
        Assert.Contains("\"omittedSections\":1", json);
        Assert.Throws<InvalidDataException>(() => ConsistencyChecks.Sources([new(_first, "Target", new('a', 200_001))], _first));
    }
    [Fact]
    public void ActionUsesOriginalPassagesEvenWhenExtractedFactsHaveNormalizedTheContradictionAway()
    {
        var document = DocumentFactory.CreateNewDocument(); var first = document.Chapters[0].Sections[0];
        document.Chapters[0].Sections[0] = first with { Content = first.Content with { Value = "<p>Her eyes were blue.</p>" } };
        document.Chapters[0].Sections.Add(first with { SectionId = _second, Order = 1, Content = first.Content with { Value = "<p>Her eyes were brown.</p>" } });
        var request = new ContinuityCheckAction().BuildRequest(new AiActionInput(document, first.SectionId, new TextRange(0, 0), "", "", new() {
            ["character_bible_json"] = "{\"schemaVersion\":\"1.0\",\"characters\":[{\"name\":\"Elin\",\"facts\":[{\"fact\":\"Brown eyes\"}]}]}"
        }));
        string passages = request.Inputs["manuscript_passages"].ToString()!;
        Assert.Contains("Her eyes were blue.", passages); Assert.Contains("Her eyes were brown.", passages);
        Assert.Contains("not contradictions", request.Inputs["instruction"].ToString());
    }
    [Fact]
    public async Task OpenAiWireRequestIncludesOriginalConflictingPassagesAndTheTwoPassageContract()
    {
        var document = DocumentFactory.CreateNewDocument(); var first = document.Chapters[0].Sections[0];
        document.Chapters[0].Sections[0] = first with { Content = first.Content with { Value = "<p>Her eyes were blue.</p>" } };
        document.Chapters[0].Sections.Add(first with { SectionId = _second, Order = 1, Content = first.Content with { Value = "<p>Her eyes were brown.</p>" } });
        var request = new ContinuityCheckAction().BuildRequest(new AiActionInput(document, first.SectionId, new TextRange(0, 0), "", "", new()));
        var key = (OpenAiKeyProvider)Activator.CreateInstance(typeof(OpenAiKeyProvider), BindingFlags.NonPublic | BindingFlags.Instance, null, ["synthetic-test-key"], null)!;
        var provider = new OpenAiProvider(new NoNetworkFactory(), Options.Create(new WriterAiOptions()), key, NullLogger<OpenAiProvider>.Instance);
        using var message = (HttpRequestMessage)typeof(OpenAiProvider).GetMethod("BuildContinuityCheckRequest", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(provider, [request, "synthetic-test-key"])!;
        using var payload = JsonDocument.Parse(await message.Content!.ReadAsStringAsync());
        string prompt = payload.RootElement.GetProperty("input")[1].GetProperty("content")[0].GetProperty("text").GetString()!;
        Assert.Contains("Her eyes were blue.", prompt); Assert.Contains("Her eyes were brown.", prompt);
        Assert.Contains(_second.ToString(), prompt); Assert.Contains("comparisonEvidence", prompt);
        Assert.Contains("Never invent a source or quote", prompt); Assert.Contains("Missing links", prompt);
    }
    private sealed class NoNetworkFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => throw new InvalidOperationException("This verification must not contact a provider.");
    }
    [Fact]
    public async Task IntentionalDecisionsSurviveReopeningAreAccountScopedAndChangedEvidenceResurfaces()
    {
        string root = Path.Combine(Path.GetTempPath(), "ConsistencyDecisions", Guid.NewGuid().ToString("N"));
        try {
            var document = Guid.NewGuid(); string scope = new('A', 64), other = new('B', 64);
            string key = ConsistencyChecks.DecisionKey(_first, "Blue eyes", new(_second, "Brown eyes"), "character");
            await new LocalAiStore(root).SetConsistencyIntentionalAsync(document, scope, key, true);
            var reopened = new LocalAiStore(root);
            Assert.True(await reopened.IsConsistencyIntentionalAsync(document, scope, key));
            Assert.False(await reopened.IsConsistencyIntentionalAsync(document, other, key));
            Assert.False(await reopened.IsConsistencyIntentionalAsync(Guid.NewGuid(), scope, key));
            Assert.False(await reopened.IsConsistencyIntentionalAsync(document, scope, ConsistencyChecks.DecisionKey(_first, "Green eyes", new(_second, "Brown eyes"), "character")));
            await reopened.SetConsistencyIntentionalAsync(document, scope, key, false);
            Assert.False(await new LocalAiStore(root).IsConsistencyIntentionalAsync(document, scope, key));
        } finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
