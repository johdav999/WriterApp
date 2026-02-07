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
using WriterApp.Application.Usage;
using WriterApp.Data.Usage;
using WriterApp.Domain.Documents;
using Xunit;

namespace WriterApp.Tests
{
    public sealed class CustomTransformActionTests
    {
        [Fact]
        public void ExpandTemplate_ReplacesTokens()
        {
            Dictionary<string, object?> options = new()
            {
                ["tone"] = "dramatic",
                ["focus"] = "urgency"
            };

            string expanded = CustomTransformAction.ExpandTemplate(
                "Rewrite to a {tone} voice with {focus}.",
                options);

            Assert.Equal("Rewrite to a dramatic voice with urgency.", expanded);
        }

        [Fact]
        public async Task ProposalApplyUndo_Works_ThroughCommandProcessor()
        {
            Document document = DocumentFactory.CreateNewDocument();
            Section section = document.Chapters[0].Sections[0];
            document.Chapters[0].Sections[0] = section with
            {
                Content = section.Content with { Value = "<p>The street was quiet.</p>" }
            };
            Guid sectionId = document.Chapters[0].Sections[0].SectionId;
            string plainText = PlainTextMapper.ToPlainText(document.Chapters[0].Sections[0].Content.Value);

            IAiOrchestrator orchestrator = BuildOrchestrator(new CustomTransformAction());
            AiExecutionResult result = await orchestrator.ExecuteActionAsync(
                CustomTransformAction.ActionIdValue,
                new AiActionInput(
                    document,
                    sectionId,
                    new TextRange(0, plainText.Length),
                    plainText,
                    "Custom transform",
                    new Dictionary<string, object?>
                    {
                        ["template"] = "Make this more {tone}.",
                        ["tone"] = "cinematic",
                        ["scope"] = "section"
                    }),
                CancellationToken.None);

            Assert.True(result.Succeeded);
            Assert.NotNull(result.Proposal);

            DocumentState state = new(document);
            CommandProcessor processor = new(state);
            IAiProposalApplier applier = new DefaultProposalApplier(new InMemoryArtifactStore());
            string before = state.Document.Chapters[0].Sections[0].Content.Value;

            applier.Apply(processor, result.Proposal!);
            string after = state.Document.Chapters[0].Sections[0].Content.Value;

            Assert.NotEqual(before, after);
            processor.Undo();
            Assert.Equal(before, state.Document.Chapters[0].Sections[0].Content.Value);
        }

        private static IAiOrchestrator BuildOrchestrator(params IAiAction[] actions)
        {
            IAiProvider provider = new CustomTransformProvider();
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

        private sealed class CustomTransformProvider : IAiProvider
        {
            public string ProviderId => "custom-transform-test";

            public AiProviderCapabilities Capabilities => new(true, false);

            public Task<AiResult> ExecuteAsync(AiRequest request, CancellationToken ct)
            {
                string original = request.Context.SelectionText ?? request.Context.OriginalText ?? string.Empty;
                string rewritten = $"[custom] {original}";
                AiArtifact artifact = new(
                    Guid.NewGuid(),
                    AiModality.Text,
                    "text/plain",
                    rewritten,
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
