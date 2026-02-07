using System;
using System.Collections.Generic;
using System.Linq;
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
    public sealed class ReviseActionsTests
    {
        [Theory]
        [MemberData(nameof(ReviseActionCases))]
        public async Task ReviseAction_ProducesTextAndReplaceOperation(
            IAiAction action,
            bool requiresSelection,
            Dictionary<string, object?>? options)
        {
            Document document = CreateDocumentWithContent();
            Guid sectionId = document.Chapters[0].Sections[0].SectionId;
            string plainText = WriterApp.Application.State.PlainTextMapper.ToPlainText(document.Chapters[0].Sections[0].Content.Value);
            TextRange range = requiresSelection ? new TextRange(0, Math.Min(12, plainText.Length)) : new TextRange(0, plainText.Length);
            string selected = requiresSelection ? plainText.Substring(0, Math.Min(12, plainText.Length)) : string.Empty;

            IAiOrchestrator orchestrator = BuildOrchestrator(action);
            AiExecutionResult result = await orchestrator.ExecuteActionAsync(
                action.ActionId,
                new AiActionInput(document, sectionId, range, selected, action.DisplayName, options),
                CancellationToken.None);

            Assert.True(result.Succeeded);
            Assert.NotNull(result.Proposal);
            Assert.False(string.IsNullOrWhiteSpace(result.Proposal!.ProposedText));
            ReplaceTextRangeOperation operation = Assert.IsType<ReplaceTextRangeOperation>(Assert.Single(result.Proposal.Operations));
            Assert.Equal(sectionId, operation.SectionId);
            Assert.True(operation.NewText.Length > 0);

            if (requiresSelection)
            {
                Assert.Equal(range.Start, operation.Range.Start);
                Assert.Equal(range.Length, operation.Range.Length);
            }
            else
            {
                Assert.Equal(0, operation.Range.Start);
                Assert.Equal(plainText.Length, operation.Range.Length);
            }
        }

        [Fact]
        public async Task ReviseProposal_ApplyUndo_TracksAiProvenance()
        {
            Document document = CreateDocumentWithContent();
            Guid sectionId = document.Chapters[0].Sections[0].SectionId;
            string plainText = WriterApp.Application.State.PlainTextMapper.ToPlainText(document.Chapters[0].Sections[0].Content.Value);
            IAiOrchestrator orchestrator = BuildOrchestrator(new TightenSectionAction());

            AiExecutionResult result = await orchestrator.ExecuteActionAsync(
                TightenSectionAction.ActionIdValue,
                new AiActionInput(document, sectionId, new TextRange(0, plainText.Length), string.Empty, "Tighten section", null),
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
            Assert.True(state.Document.Chapters[0].Sections[0].AI.LastModifiedByAi);
            Assert.True(state.Document.Chapters[0].Sections[0].AI.AiEditGroups.Count > 0);

            processor.Undo();
            Assert.Equal(before, state.Document.Chapters[0].Sections[0].Content.Value);
        }

        [Fact]
        public async Task ReviseProposal_RollbackGroup_RestoresContent()
        {
            Document document = CreateDocumentWithContent();
            Guid sectionId = document.Chapters[0].Sections[0].SectionId;
            string plainText = WriterApp.Application.State.PlainTextMapper.ToPlainText(document.Chapters[0].Sections[0].Content.Value);
            IAiOrchestrator orchestrator = BuildOrchestrator(new ExpandSectionAction());

            AiExecutionResult result = await orchestrator.ExecuteActionAsync(
                ExpandSectionAction.ActionIdValue,
                new AiActionInput(document, sectionId, new TextRange(0, plainText.Length), string.Empty, "Expand section", null),
                CancellationToken.None);

            Assert.True(result.Succeeded);
            Assert.NotNull(result.Proposal);

            DocumentState state = new(document);
            CommandProcessor processor = new(state);
            IAiProposalApplier applier = new DefaultProposalApplier(new InMemoryArtifactStore());
            string before = state.Document.Chapters[0].Sections[0].Content.Value;
            applier.Apply(processor, result.Proposal!);

            Guid? groupId = state.Document.Chapters[0].Sections[0].AI.AiEditGroups.LastOrDefault()?.GroupId;
            Assert.True(groupId.HasValue);

            bool rolledBack = processor.RollbackAiEditGroup(sectionId, groupId.Value);
            Assert.True(rolledBack);
            Assert.Equal(before, state.Document.Chapters[0].Sections[0].Content.Value);
        }

        public static IEnumerable<object[]> ReviseActionCases()
        {
            yield return new object[] { new TightenSelectionAction(), true, null! };
            yield return new object[] { new TightenSectionAction(), false, null! };
            yield return new object[] { new ExpandSelectionAction(), true, null! };
            yield return new object[] { new ExpandSectionAction(), false, null! };
            yield return new object[] { new ChangeToneSelectionAction(), true, new Dictionary<string, object?> { ["tone"] = "Formal" } };
            yield return new object[] { new ChangeToneSectionAction(), false, new Dictionary<string, object?> { ["tone"] = "Friendly" } };
            yield return new object[] { new ShowDontTellSelectionAction(), true, null! };
            yield return new object[] { new ShowDontTellSectionAction(), false, null! };
        }

        private static Document CreateDocumentWithContent()
        {
            Document document = DocumentFactory.CreateNewDocument();
            Section section = document.Chapters[0].Sections[0];
            document.Chapters[0].Sections[0] = section with
            {
                Content = section.Content with
                {
                    Value = "<p>She was angry and the room felt tense.</p><p>He walked to the door quietly.</p>"
                }
            };
            return document;
        }

        private static IAiOrchestrator BuildOrchestrator(params IAiAction[] actions)
        {
            IAiProvider provider = new ReviseTestProvider();
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

        private sealed class ReviseTestProvider : IAiProvider
        {
            public string ProviderId => "revise-test";

            public AiProviderCapabilities Capabilities => new(true, false);

            public Task<AiResult> ExecuteAsync(AiRequest request, CancellationToken ct)
            {
                string source = request.Context.SelectionText ?? request.Context.OriginalText ?? string.Empty;
                string output = $"[{request.ActionId}] {source} (revised)";
                AiArtifact artifact = new(
                    Guid.NewGuid(),
                    AiModality.Text,
                    "text/plain",
                    output,
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
