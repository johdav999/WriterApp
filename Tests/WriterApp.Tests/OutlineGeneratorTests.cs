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
using WriterApp.Application.State;
using WriterApp.Application.Synopsis;
using WriterApp.Application.Usage;
using WriterApp.Data.Usage;
using WriterApp.Domain.Documents;
using Xunit;

namespace WriterApp.Tests
{
    public sealed class OutlineGeneratorTests
    {
        [Fact]
        public void GenerateOutlineFromSynopsisAction_BuildsRequest()
        {
            Document document = DocumentFactory.CreateNewDocument();
            document.Synopsis.Premise = "A healer discovers a sealed gate beneath the village.";
            IAiAction action = new GenerateOutlineFromSynopsisAction();

            AiRequest request = action.BuildRequest(new AiActionInput(
                document,
                document.Chapters[0].Sections[0].SectionId,
                new TextRange(0, 0),
                string.Empty,
                "Generate outline",
                new Dictionary<string, object?> { ["mode"] = "chapters", ["desired_count"] = 8 }));

            Assert.Equal(GenerateOutlineFromSynopsisAction.ActionIdValue, request.ActionId);
            Assert.True(request.Inputs.ContainsKey("synopsis_context"));
            Assert.Equal("chapters", request.Inputs["mode"]?.ToString());
        }

        [Fact]
        public async Task OutlineProposal_UsesReplaceSynopsisFieldOperation()
        {
            Document document = DocumentFactory.CreateNewDocument();
            IAiOrchestrator orchestrator = BuildOrchestrator(new GenerateOutlineFromSynopsisAction());

            AiExecutionResult result = await orchestrator.ExecuteActionAsync(
                GenerateOutlineFromSynopsisAction.ActionIdValue,
                new AiActionInput(
                    document,
                    document.Chapters[0].Sections[0].SectionId,
                    new TextRange(0, 0),
                    string.Empty,
                    "Generate outline",
                    new Dictionary<string, object?> { ["mode"] = "chapters", ["desired_count"] = 6 }),
                CancellationToken.None);

            Assert.True(result.Succeeded);
            ReplaceSynopsisFieldOperation operation = Assert.IsType<ReplaceSynopsisFieldOperation>(Assert.Single(result.Proposal!.Operations));
            Assert.Equal("outline_draft", operation.FieldKey);
            Assert.True(OutlineDraftParser.TryParse(operation.NewText, out OutlineDraft? parsed));
            Assert.NotNull(parsed);
        }

        [Fact]
        public async Task OutlineProposal_ApplyAndUndo_Works()
        {
            Document document = DocumentFactory.CreateNewDocument();
            Guid sectionId = document.Chapters[0].Sections[0].SectionId;
            IAiOrchestrator orchestrator = BuildOrchestrator(new GenerateOutlineFromSynopsisAction());
            AiExecutionResult result = await orchestrator.ExecuteActionAsync(
                GenerateOutlineFromSynopsisAction.ActionIdValue,
                new AiActionInput(document, sectionId, new TextRange(0, 0), string.Empty, "Generate outline", null),
                CancellationToken.None);

            Assert.True(result.Succeeded);
            DocumentState state = new(document);
            CommandProcessor processor = new(state);
            IAiProposalApplier applier = new DefaultProposalApplier(new InMemoryArtifactStore());

            string before = state.Document.Synopsis.OutlineDraft;
            applier.Apply(processor, result.Proposal!);

            Assert.NotEqual(before, state.Document.Synopsis.OutlineDraft);
            processor.Undo();
            Assert.Equal(before, state.Document.Synopsis.OutlineDraft);
        }

        [Fact]
        public void CreateSectionsFromOutlineCommand_CreatesSectionsAndUndo()
        {
            Document document = DocumentFactory.CreateNewDocument();
            Guid sectionId = document.Chapters[0].Sections[0].SectionId;
            int beforeCount = document.Chapters[0].Sections.Count;

            OutlineItemDraft item = new(
                1,
                "Arrival",
                "The protagonist reaches the capital.",
                "Ari",
                "Capital City",
                new[] { "Arrival at gate", "First warning" },
                "Setup",
                "Keep tension high.");

            DocumentState state = new(document);
            CommandProcessor processor = new(state);
            processor.Execute(new CreateSectionsFromOutlineCommand(sectionId, new[] { item }));

            Assert.Equal(beforeCount + 1, state.Document.Chapters[0].Sections.Count);
            Assert.Equal("Arrival", state.Document.Chapters[0].Sections[^1].Title);
            processor.Undo();
            Assert.Equal(beforeCount, state.Document.Chapters[0].Sections.Count);
        }

        private static IAiOrchestrator BuildOrchestrator(params IAiAction[] actions)
        {
            IAiProvider provider = new OutlineTestProvider();
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

        private sealed class OutlineTestProvider : IAiProvider
        {
            public string ProviderId => "outline-test";

            public AiProviderCapabilities Capabilities => new(true, false);

            public Task<AiResult> ExecuteAsync(AiRequest request, CancellationToken ct)
            {
                string json = "{\"schemaVersion\":\"1.0\",\"mode\":\"chapters\",\"items\":[{\"index\":1,\"title\":\"Act I\",\"summary\":\"Setup\",\"pov\":\"\",\"setting\":\"\",\"beats\":[\"Hook\"],\"storyRole\":\"setup\",\"notes\":\"\"}]}";
                AiArtifact artifact = new(
                    Guid.NewGuid(),
                    AiModality.Text,
                    "application/json",
                    json,
                    null,
                    null);

                return Task.FromResult(new AiResult(
                    request.RequestId,
                    new List<AiArtifact> { artifact },
                    new AiUsage(0, 0, TimeSpan.Zero),
                    new Dictionary<string, object>()));
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
