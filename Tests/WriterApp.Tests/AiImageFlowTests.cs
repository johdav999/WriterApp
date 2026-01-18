using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WriterApp.AI.Abstractions;
using WriterApp.AI.Actions;
using WriterApp.AI.Core;
using WriterApp.Application.Commands;
using WriterApp.Application.State;
using WriterApp.Application.Subscriptions;
using WriterApp.Application.Usage;
using WriterApp.Data;
using WriterApp.Data.Subscriptions;
using WriterApp.Data.Usage;
using WriterApp.Domain.Documents;
using Xunit;

namespace WriterApp.Tests
{
    public sealed class AiImageFlowTests
    {
        [Fact]
        public async Task CoverImageEntitlement_IsEnforced()
        {
            await using SqliteConnection connection = new("DataSource=:memory:");
            await connection.OpenAsync();
            AppDbContext dbContext = BuildDbContext(connection);

            Plan? standardPlan = await dbContext.Plans.FirstOrDefaultAsync(plan => plan.Key == "standard");
            Assert.NotNull(standardPlan);

            dbContext.UserPlanAssignments.Add(new UserPlanAssignment
            {
                UserId = "user-standard",
                PlanId = standardPlan!.PlanId,
                AssignedUtc = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                AssignedBy = "seed"
            });
            await dbContext.SaveChangesAsync();

            IEntitlementService entitlementService = BuildEntitlementService(dbContext);
            TestClock clock = new(new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc));
            IUsageMeter usageMeter = new UsageMeter(dbContext, clock);
            IAiUsagePolicy policy = BuildPolicy(entitlementService, usageMeter, clock, "user-standard");

            AiExecutionResult result = await BuildOrchestrator(policy, usageMeter, new TestImageProvider())
                .ExecuteActionAsync(GenerateCoverImageAction.ActionIdValue, BuildInput(), CancellationToken.None);

            Assert.False(result.Succeeded);
            Assert.Equal("ai.images.cover_disabled", result.ErrorCode);
        }

        [Fact]
        public async Task ImageProposal_AppliesAndRollsBack()
        {
            await using SqliteConnection connection = new("DataSource=:memory:");
            await connection.OpenAsync();
            AppDbContext dbContext = BuildDbContext(connection);

            Plan? professionalPlan = await dbContext.Plans.FirstOrDefaultAsync(plan => plan.Key == "professional");
            Assert.NotNull(professionalPlan);

            dbContext.UserPlanAssignments.Add(new UserPlanAssignment
            {
                UserId = "user-pro",
                PlanId = professionalPlan!.PlanId,
                AssignedUtc = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                AssignedBy = "seed"
            });
            await dbContext.SaveChangesAsync();

            IEntitlementService entitlementService = BuildEntitlementService(dbContext);
            TestClock clock = new(new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc));
            UsageMeter usageMeter = new(dbContext, clock);
            IAiUsagePolicy policy = BuildPolicy(entitlementService, usageMeter, clock, "user-pro");
            TestImageProvider provider = new();

            IArtifactStore artifactStore = new InMemoryArtifactStore();
            IAiOrchestrator orchestrator = BuildOrchestrator(policy, usageMeter, provider, artifactStore);

            AiExecutionResult result = await orchestrator.ExecuteActionAsync(
                GenerateCoverImageAction.ActionIdValue,
                BuildInput(),
                CancellationToken.None);

            Assert.True(result.Succeeded);
            Assert.NotNull(result.Proposal);
            Assert.Contains(result.Proposal!.Operations, op => op is AttachImageOperation);

            UsageSnapshot usageSnapshot = await usageMeter.GetCurrentPeriodAsync("user-pro", "ai.total");
            Assert.Equal(TestImageProvider.ImageTokenCost, usageSnapshot.TotalOutputTokens);

            Document document = DocumentFactory.CreateNewDocument();
            DocumentState state = new(document);
            CommandProcessor processor = new(state);
            IAiProposalApplier applier = new DefaultProposalApplier(artifactStore);

            applier.Apply(processor, result.Proposal);
            Assert.NotNull(state.Document.CoverImageId);

            Guid? groupId = state.Document.Chapters[0].Sections[0].AI?.AiEditGroups?.LastOrDefault()?.GroupId;
            Assert.True(groupId.HasValue);

            bool rolledBack = processor.RollbackAiEditGroup(state.Document.Chapters[0].Sections[0].SectionId, groupId.Value);
            Assert.True(rolledBack);
            Assert.Null(state.Document.CoverImageId);
        }

        private static IAiOrchestrator BuildOrchestrator(
            IAiUsagePolicy policy,
            IUsageMeter usageMeter,
            IAiProvider provider,
            IArtifactStore? artifactStore = null)
        {
            IArtifactStore store = artifactStore ?? new InMemoryArtifactStore();
            IAiProviderRegistry registry = new DefaultAiProviderRegistry(new[] { provider });
            WriterAiOptions options = new() { Enabled = true };
            IAiRouter router = new DefaultAiRouter(registry, Options.Create(options), NullLogger<DefaultAiRouter>.Instance);
            IAiActionExecutor executor = new AiActionExecutor(router, store);

            return new AiOrchestrator(
                executor,
                registry,
                router,
                policy,
                usageMeter,
                Options.Create(options),
                new IAiAction[] { new GenerateCoverImageAction() });
        }

        private static AiActionInput BuildInput()
        {
            Document document = DocumentFactory.CreateNewDocument();
            Guid sectionId = document.Chapters[0].Sections[0].SectionId;
            TextRange selection = new(0, 0);
            return new AiActionInput(document, sectionId, selection, string.Empty, "Generate cover image", null);
        }

        private static AppDbContext BuildDbContext(SqliteConnection connection)
        {
            DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;

            AppDbContext context = new(options);
            context.Database.EnsureCreated();
            return context;
        }

        private static IEntitlementService BuildEntitlementService(AppDbContext dbContext)
        {
            IMemoryCache cache = new MemoryCache(new MemoryCacheOptions());
            IPlanRepository planRepository = new PlanRepository(dbContext);
            return new EntitlementService(planRepository, cache);
        }

        private static IAiUsagePolicy BuildPolicy(
            IEntitlementService entitlementService,
            IUsageMeter usageMeter,
            IClock clock,
            string userId)
        {
            DefaultHttpContext httpContext = new();
            ClaimsIdentity identity = new(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId)
            }, "test");
            httpContext.User = new ClaimsPrincipal(identity);

            HttpContextAccessor accessor = new()
            {
                HttpContext = httpContext
            };

            WriterAiOptions options = new()
            {
                Enabled = true,
                RateLimiting = new WriterAiRateLimitOptions { RequestsPerMinute = 100 }
            };

            IMemoryCache cache = new MemoryCache(new MemoryCacheOptions());
            return new AiUsagePolicy(accessor, entitlementService, usageMeter, cache, clock, Options.Create(options));
        }

        private sealed class TestClock : IClock
        {
            public TestClock(DateTime utcNow)
            {
                UtcNow = utcNow;
            }

            public DateTime UtcNow { get; }
        }

        private sealed class TestImageProvider : IAiProvider, IAiBillingProvider
        {
            public const int ImageTokenCost = 1000;

            public string ProviderId => "test-image";
            public AiProviderCapabilities Capabilities => new(false, true);
            public bool RequiresEntitlement => true;
            public bool IsBillable => true;

            public Task<AiResult> ExecuteAsync(AiRequest request, CancellationToken ct)
            {
                byte[] bytes = new byte[] { 1, 2, 3 };
                AiArtifact artifact = new(
                    Guid.NewGuid(),
                    AiModality.Image,
                    "image/png",
                    null,
                    bytes,
                    new Dictionary<string, object>
                    {
                        ["dataUrl"] = $"data:image/png;base64,{Convert.ToBase64String(bytes)}"
                    });

                return Task.FromResult(new AiResult(
                    request.RequestId,
                    new List<AiArtifact> { artifact },
                    new AiUsage(0, ImageTokenCost, TimeSpan.Zero),
                    new Dictionary<string, object>
                    {
                        ["provider"] = ProviderId,
                        ["model"] = "test-image"
                    }));
            }
        }
    }
}
