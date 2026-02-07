using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WriterApp.AI.Abstractions;
using WriterApp.AI.Actions;
using WriterApp.AI.Core;
using WriterApp.Application.Commands;
using WriterApp.Application.Continuity;
using WriterApp.Application.State;
using WriterApp.Application.Usage;
using WriterApp.Data.Usage;
using WriterApp.Domain.Documents;
using Xunit;

namespace WriterApp.Tests
{
    public sealed class ContinuityCoachTests
    {
        [Fact]
        public void ContinuityJson_ParsesAndNormalizesModels()
        {
            const string characterJson = "{\"schemaVersion\":\"1.0\",\"characters\":[{\"name\":\"Mira\",\"facts\":[{\"fact\":\"Fears deep water\",\"evidence\":{\"sectionId\":\"00000000-0000-0000-0000-000000000123\",\"quote\":\"Mira avoided the river.\"}}],\"traits\":[\"cautious\"]}]}";
            const string placeJson = "{\"schemaVersion\":\"1.0\",\"places\":[{\"name\":\"Ashmere\",\"facts\":[{\"fact\":\"Singing well\",\"evidence\":{\"sectionId\":\"00000000-0000-0000-0000-000000000123\",\"quote\":\"The well sang.\"}}]}]}";
            const string reportJson = "{\"schemaVersion\":\"1.0\",\"issues\":[{\"severity\":\"high\",\"type\":\"character\",\"message\":\"Eye color changed.\",\"evidence\":{\"sectionId\":\"00000000-0000-0000-0000-000000000123\",\"quote\":\"Blue eyes became brown.\"},\"suggestedFix\":\"Align eye color.\",\"anchor\":{\"plainTextStart\":20,\"plainTextLength\":999}}]}";

            Assert.True(ContinuityJson.TryParseCharacterBible(characterJson, out CharacterBible? characterBible));
            Assert.NotNull(characterBible);
            Assert.Single(characterBible!.Characters);

            Assert.True(ContinuityJson.TryParsePlaceBible(placeJson, out PlaceBible? placeBible));
            Assert.NotNull(placeBible);
            Assert.Single(placeBible!.Places);

            Assert.True(ContinuityJson.TryParseContinuityReport(reportJson, out ContinuityReport? report));
            Assert.NotNull(report);
            Assert.Single(report!.Issues);

            ContinuityAnchor normalized = ContinuityJson.NormalizeAnchor(report.Issues[0].Anchor, 64);
            Assert.Equal(20, normalized.PlainTextStart);
            Assert.Equal(44, normalized.PlainTextLength);
        }

        [Fact]
        public async Task ContinuityCheckAction_ProducesJsonIssues_ForContradictorySection()
        {
            Document document = CreateContradictoryDocument();
            Guid sectionId = document.Chapters[0].Sections[0].SectionId;
            string plainText = PlainTextMapper.ToPlainText(document.Chapters[0].Sections[0].Content.Value);
            IAiOrchestrator orchestrator = BuildOrchestrator(
                new ContinuityCheckAction(),
                new ExtractCharacterBibleAction(),
                new ExtractPlaceBibleAction());

            AiExecutionResult result = await orchestrator.ExecuteActionAsync(
                ContinuityCheckAction.ActionIdValue,
                new AiActionInput(
                    document,
                    sectionId,
                    new TextRange(0, plainText.Length),
                    plainText,
                    "Check continuity",
                    new Dictionary<string, object?>()),
                CancellationToken.None);

            Assert.True(result.Succeeded);
            Assert.NotNull(result.Proposal);
            Assert.False(string.IsNullOrWhiteSpace(result.Proposal!.ProposedText));
            Assert.True(ContinuityJson.TryParseContinuityReport(result.Proposal.ProposedText, out ContinuityReport? report));
            Assert.NotNull(report);
            Assert.NotEmpty(report!.Issues);
        }

        [Fact]
        public async Task ApplyContinuityFixAction_ReplacesOnlyAnchoredSpan_AndIsUndoable()
        {
            Document document = DocumentFactory.CreateNewDocument();
            Section section = document.Chapters[0].Sections[0];
            string sourceText = "Mira had blue eyes in this scene.";
            document.Chapters[0].Sections[0] = section with
            {
                Content = section.Content with
                {
                    Value = $"<p>{sourceText}</p>"
                }
            };

            Guid sectionId = document.Chapters[0].Sections[0].SectionId;
            int start = sourceText.IndexOf("blue", StringComparison.Ordinal);
            int length = "blue".Length;
            string selected = sourceText.Substring(start, length);

            IAiOrchestrator orchestrator = BuildOrchestrator(new ApplyContinuityFixAction());
            AiExecutionResult result = await orchestrator.ExecuteActionAsync(
                ApplyContinuityFixAction.ActionIdValue,
                new AiActionInput(
                    document,
                    sectionId,
                    new TextRange(start, length),
                    selected,
                    "Apply continuity fix",
                    new Dictionary<string, object?>
                    {
                        ["section_id"] = sectionId,
                        ["anchor_start"] = start,
                        ["anchor_length"] = length,
                        ["suggested_fix"] = "green"
                    }),
                CancellationToken.None);

            Assert.True(result.Succeeded);
            Assert.NotNull(result.Proposal);
            ReplaceTextRangeOperation replace = Assert.Single(result.Proposal!.Operations) as ReplaceTextRangeOperation
                ?? throw new Xunit.Sdk.XunitException("Expected ReplaceTextRangeOperation.");
            Assert.Equal(start, replace.Range.Start);
            Assert.Equal(length, replace.Range.Length);
            Assert.Equal("green", replace.NewText);

            CommandProcessor processor = new(new DocumentState(document));
            DefaultProposalApplier applier = new(new InMemoryArtifactStore());
            applier.Apply(processor, result.Proposal);

            string updatedHtml = document.Chapters[0].Sections[0].Content.Value ?? string.Empty;
            string updatedText = PlainTextMapper.ToPlainText(updatedHtml);
            Assert.Contains("green eyes", updatedText, StringComparison.Ordinal);
            Assert.DoesNotContain("blue eyes", updatedText, StringComparison.Ordinal);

            processor.Undo();
            string revertedHtml = document.Chapters[0].Sections[0].Content.Value ?? string.Empty;
            string revertedText = PlainTextMapper.ToPlainText(revertedHtml);
            Assert.Contains("blue eyes", revertedText, StringComparison.Ordinal);
        }

        private static IAiOrchestrator BuildOrchestrator(params IAiAction[] actions)
        {
            IAiProvider provider = new ContinuityTestProvider();
            IAiProviderRegistry registry = new DefaultAiProviderRegistry(new[] { provider });
            WriterAiOptions options = new() { Enabled = true };
            IAiRouter router = new DefaultAiRouter(registry, Options.Create(options), NullLogger<DefaultAiRouter>.Instance);
            IAiActionExecutor executor = new AiActionExecutor(
                router,
                new InMemoryArtifactStore(),
                new LoggerFactory().CreateLogger<AiActionExecutor>());

            return new AiOrchestrator(
                executor,
                registry,
                router,
                new AllowAllUsagePolicy(),
                new NoOpUsageMeter(),
                Options.Create(options),
                actions);
        }

        private static Document CreateContradictoryDocument()
        {
            Document document = DocumentFactory.CreateNewDocument();
            Section section = document.Chapters[0].Sections[0];
            document.Chapters[0].Sections[0] = section with
            {
                Content = section.Content with
                {
                    Value = "<p>Mira has blue eyes in the market scene.</p><p>Later, Mira's brown eyes reflected the torchlight.</p>"
                }
            };
            return document;
        }

        private sealed class ContinuityTestProvider : IAiProvider
        {
            public string ProviderId => "continuity-test";

            public AiProviderCapabilities Capabilities => new(true, false);

            public Task<AiResult> ExecuteAsync(AiRequest request, CancellationToken ct)
            {
                string actionId = request.ActionId ?? string.Empty;
                string output = actionId switch
                {
                    "continuity.extract_character_bible" => "{\"schemaVersion\":\"1.0\",\"characters\":[{\"name\":\"Mira\",\"facts\":[{\"fact\":\"Blue eyes\",\"evidence\":{\"sectionId\":\"" + request.Context.SectionId + "\",\"quote\":\"Mira has blue eyes.\"}}],\"traits\":[\"observant\"]}]}",
                    "continuity.extract_place_bible" => "{\"schemaVersion\":\"1.0\",\"places\":[{\"name\":\"Ashmere\",\"facts\":[{\"fact\":\"Market square\",\"evidence\":{\"sectionId\":\"" + request.Context.SectionId + "\",\"quote\":\"In the market scene.\"}}]}]}",
                    "continuity.check_section" => BuildContinuityReport(request),
                    _ => "{\"schemaVersion\":\"1.0\",\"issues\":[]}"
                };

                AiArtifact artifact = new(
                    Guid.NewGuid(),
                    AiModality.Text,
                    "application/json",
                    output,
                    null,
                    null);

                return Task.FromResult(new AiResult(
                    request.RequestId,
                    new List<AiArtifact> { artifact },
                    new AiUsage(0, 0, TimeSpan.Zero),
                    new Dictionary<string, object>()));
            }

            private static string BuildContinuityReport(AiRequest request)
            {
                string section = request.Inputs.TryGetValue("section_text", out object? value)
                    ? value?.ToString() ?? string.Empty
                    : string.Empty;
                bool contradiction = section.Contains("blue eyes", StringComparison.OrdinalIgnoreCase)
                    && section.Contains("brown eyes", StringComparison.OrdinalIgnoreCase);

                if (!contradiction)
                {
                    return "{\"schemaVersion\":\"1.0\",\"issues\":[]}";
                }

                return "{\"schemaVersion\":\"1.0\",\"issues\":[{\"severity\":\"high\",\"type\":\"character\",\"message\":\"Mira eye color is inconsistent.\",\"evidence\":{\"sectionId\":\""
                    + request.Context.SectionId
                    + "\",\"quote\":\"blue eyes ... brown eyes\"},\"suggestedFix\":\"Keep one consistent eye color.\",\"anchor\":{\"plainTextStart\":0,\"plainTextLength\":48}}]}";
            }
        }

        private sealed class AllowAllUsagePolicy : IAiUsagePolicy
        {
            public Task<AiUsageDecision> EvaluateAsync(IAiProvider provider, string actionId)
            {
                return Task.FromResult(new AiUsageDecision(true, "test-user", null, null));
            }
        }

        private sealed class NoOpUsageMeter : IUsageMeter
        {
            public Task RecordAsync(UsageEvent usageEvent) => Task.CompletedTask;

            public Task<UsageSnapshot> GetCurrentPeriodAsync(string userId, string kind)
            {
                DateTime now = DateTime.UtcNow;
                return Task.FromResult(new UsageSnapshot(userId, now, now, kind, 0, 0, 0, now));
            }

            public Task<UsageSnapshot> GetRangeAsync(string userId, string kind, DateTime startUtc, DateTime endUtc)
            {
                return Task.FromResult(new UsageSnapshot(userId, startUtc, endUtc, kind, 0, 0, 0, endUtc));
            }
        }
    }
}
