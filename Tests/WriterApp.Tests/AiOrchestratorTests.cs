using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WriterApp.AI.Abstractions;
using WriterApp.AI.Actions;
using Microsoft.Extensions.Logging;
using WriterApp.AI.Core;
using WriterApp.AI.Providers.Mock;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WriterApp.Application.AI;
using WriterApp.Application.Commands;
using WriterApp.Application.Usage;
using WriterApp.Application.State;
using WriterApp.Data.Usage;
using WriterApp.Domain.Documents;
using Xunit;

namespace WriterApp.Tests
{
    public sealed class AiOrchestratorTests
    {
        [Fact]
        public void CanRunAction_HidesImageWhenOnlyTextProvider()
        {
            IAiProviderRegistry registry = new DefaultAiProviderRegistry(new IAiProvider[]
            {
                new MockTextProvider()
            });

            WriterAiOptions options = new() { Enabled = true };
            IAiRouter router = new DefaultAiRouter(
                registry,
                Options.Create(options),
                NullLogger<DefaultAiRouter>.Instance);
            IArtifactStore store = new InMemoryArtifactStore();
            IAiActionExecutor executor = new AiActionExecutor(
                router,
                store,
                new LoggerFactory().CreateLogger<AiActionExecutor>());
            IAiOrchestrator orchestrator = new AiOrchestrator(
                executor,
                registry,
                router,
                new AllowAllUsagePolicy(),
                new NoOpUsageMeter(),
                Options.Create(options),
                new IAiAction[] { new RewriteSelectionAction(), new GenerateCoverImageAction() });

            Assert.True(orchestrator.CanRunAction(RewriteSelectionAction.ActionIdValue));
            Assert.False(orchestrator.CanRunAction(GenerateCoverImageAction.ActionIdValue));
        }

        [Fact]
        public void CanRunAction_AllowsImageWhenImageProviderPresent()
        {
            IAiProviderRegistry registry = new DefaultAiProviderRegistry(new IAiProvider[]
            {
                new MockTextProvider(),
                new MockImageProvider()
            });

            WriterAiOptions options = new() { Enabled = true };
            IAiRouter router = new DefaultAiRouter(
                registry,
                Options.Create(options),
                NullLogger<DefaultAiRouter>.Instance);
            IArtifactStore store = new InMemoryArtifactStore();
            IAiActionExecutor executor = new AiActionExecutor(
                router,
                store,
                new LoggerFactory().CreateLogger<AiActionExecutor>());
            IAiOrchestrator orchestrator = new AiOrchestrator(
                executor,
                registry,
                router,
                new AllowAllUsagePolicy(),
                new NoOpUsageMeter(),
                Options.Create(options),
                new IAiAction[] { new RewriteSelectionAction(), new GenerateCoverImageAction() });

            Assert.True(orchestrator.CanRunAction(GenerateCoverImageAction.ActionIdValue));
        }

        [Fact]
        public async Task OnboardingDemoRequest_SkipsQuotaChecksAndCharges()
        {
            TrackingQuotaService quotaService = new();
            IAiProvider provider = new TestBillableTextProvider();
            IAiProviderRegistry registry = new DefaultAiProviderRegistry(new[] { provider });
            WriterAiOptions options = new() { Enabled = true };
            IAiRouter router = new DefaultAiRouter(
                registry,
                Options.Create(options),
                NullLogger<DefaultAiRouter>.Instance);
            IArtifactStore store = new InMemoryArtifactStore();
            IAiActionExecutor executor = new AiActionExecutor(
                router,
                store,
                new LoggerFactory().CreateLogger<AiActionExecutor>());

            IAiOrchestrator orchestrator = new AiOrchestrator(
                executor,
                registry,
                router,
                new AllowAllUsagePolicy(),
                quotaService,
                new NoOpUsageMeter(),
                Options.Create(options),
                new IAiAction[] { new TightenSectionAction() });

            Document document = DocumentFactory.CreateNewDocument();
            Guid sectionId = document.Chapters[0].Sections[0].SectionId;
            document.Chapters[0].Sections[0] = document.Chapters[0].Sections[0] with
            {
                Content = new SectionContent
                {
                    Format = "html",
                    Value = "<p>The woman was across the room.</p>"
                }
            };

            AiExecutionResult result = await orchestrator.ExecuteActionAsync(
                TightenSectionAction.ActionIdValue,
                new AiActionInput(
                    document,
                    sectionId,
                    new TextRange(0, 0),
                    string.Empty,
                    null,
                    new Dictionary<string, object?>
                    {
                        [OnboardingDemoAiUsage.RequestParameterKey] = true,
                        [OnboardingDemoAiUsage.InstructionParameterKey] = "tighten the character description"
                    }),
                CancellationToken.None);

            Assert.True(result.Succeeded);
            Assert.Equal(0, quotaService.EnsureCalls);
            Assert.Equal(0, quotaService.ChargeCalls);
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

        private sealed class TrackingQuotaService : IAiQuotaService
        {
            public int EnsureCalls { get; private set; }

            public int ChargeCalls { get; private set; }

            public Task<AiQuotaDecision> EnsureAiAllowedAsync(string userId, int estimatedTokens, CancellationToken ct)
            {
                EnsureCalls++;
                AiQuotaSnapshot snapshot = new("Free", 0, 0, DateTimeOffset.UtcNow);
                return Task.FromResult(new AiQuotaDecision(true, null, null, snapshot, null));
            }

            public Task<AiQuotaChargeResult> ChargeActualUsageAsync(string userId, AiRequest request, AiResult result, CancellationToken ct)
            {
                ChargeCalls++;
                AiQuotaSnapshot snapshot = new("Free", 0, 0, DateTimeOffset.UtcNow);
                return Task.FromResult(new AiQuotaChargeResult(true, 0, snapshot, null, null, null));
            }
        }

        private sealed class TestBillableTextProvider : IAiProvider, IAiBillingProvider
        {
            public string ProviderId => "test-text";

            public AiProviderCapabilities Capabilities => new(true, false);

            public bool RequiresEntitlement => true;

            public bool IsBillable => true;

            public Task<AiResult> ExecuteAsync(AiRequest request, CancellationToken ct)
            {
                AiArtifact artifact = new(
                    Guid.NewGuid(),
                    AiModality.Text,
                    "text/plain",
                    "The woman sat near the bookshelf, coffee untouched, scanning the room between glances at her phone.",
                    null,
                    null);

                return Task.FromResult(new AiResult(
                    request.RequestId,
                    new List<AiArtifact> { artifact },
                    new AiUsage(25, 40, TimeSpan.Zero),
                    new Dictionary<string, object>()));
            }
        }
    }
}
