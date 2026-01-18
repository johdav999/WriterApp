using System;
using System.Threading.Tasks;
using WriterApp.AI.Abstractions;
using WriterApp.AI.Actions;
using WriterApp.AI.Core;
using WriterApp.AI.Providers.Mock;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WriterApp.Application.Usage;
using WriterApp.Data.Usage;
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
            IAiActionExecutor executor = new AiActionExecutor(router, store);
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
            IAiActionExecutor executor = new AiActionExecutor(router, store);
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
