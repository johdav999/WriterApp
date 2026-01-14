using System;
using WriterApp.AI.Abstractions;
using WriterApp.AI.Actions;
using WriterApp.AI.Core;
using WriterApp.AI.Providers.Mock;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
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
                Options.Create(options),
                NullLogger<AiOrchestrator>.Instance,
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
                Options.Create(options),
                NullLogger<AiOrchestrator>.Instance,
                new IAiAction[] { new RewriteSelectionAction(), new GenerateCoverImageAction() });

            Assert.True(orchestrator.CanRunAction(GenerateCoverImageAction.ActionIdValue));
        }
    }
}
