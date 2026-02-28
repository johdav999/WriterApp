using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using WriterApp.AI.Abstractions;
using WriterApp.Application.Commands;
using WriterApp.Application.Continuity;
using WriterApp.Application.Subscriptions;
using WriterApp.Domain.Documents;
using Xunit;

namespace WriterApp.Tests
{
    public sealed class BibleRefreshServiceTests
    {
        [Fact]
        public async Task RefreshAsync_TimelineMalformedPatch_UsesFallbackAndSucceeds()
        {
            Guid documentId = Guid.NewGuid();
            Guid sectionId = Guid.NewGuid();

            Document document = new()
            {
                DocumentId = documentId,
                Chapters = new List<Chapter>
                {
                    new()
                    {
                        Order = 0,
                        Title = "Draft",
                        Sections = new List<Section>
                        {
                            new()
                            {
                                SectionId = sectionId,
                                Order = 0,
                                Title = "New Scene",
                                Content = new SectionContent
                                {
                                    Format = "html",
                                    Value = "<p>Test quote \"inside\" content.</p>"
                                }
                            }
                        }
                    }
                }
            };

            // Intentionally malformed JSON (unescaped inner quotes in content value).
            string malformedPatch = """
            {
              "bibleType":"Timeline",
              "schemaVersion":1,
              "ops":[
                {
                  "op":"upsertTimelineEvent",
                  "id":"evt_1",
                  "data":{"title":"New Scene","order":0,"content":"Test "inside" content"}
                }
              ]
            }
            """;

            FakeAiOrchestrator orchestrator = new(malformedPatch);
            InMemoryBibleStore store = new();
            BibleRefreshService service = new(
                orchestrator,
                new StubEntitlementService(aiEnabled: true, planKey: "professional"),
                store,
                new BiblePatchApplier(),
                NullLogger<BibleRefreshService>.Instance);

            BibleSnapshotState snapshot = await service.RefreshAsync(
                document,
                "user-1",
                sectionId,
                BibleType.Timeline,
                fullRebuild: false,
                CancellationToken.None);

            using JsonDocument json = JsonDocument.Parse(snapshot.ContentJson);
            JsonElement events = json.RootElement.GetProperty("events");
            Assert.True(events.GetArrayLength() >= 1);
            Assert.Equal(sectionId.ToString(), events[0].GetProperty("id").GetString());
        }

        [Fact]
        public async Task RefreshAsync_ThrowsEntitlementDenied_WhenAiDisabled()
        {
            Guid documentId = Guid.NewGuid();
            Guid sectionId = Guid.NewGuid();
            Document document = new()
            {
                DocumentId = documentId,
                Chapters = new List<Chapter>
                {
                    new()
                    {
                        Order = 0,
                        Title = "Draft",
                        Sections = new List<Section>
                        {
                            new()
                            {
                                SectionId = sectionId,
                                Order = 0,
                                Title = "Scene",
                                Content = new SectionContent
                                {
                                    Format = "html",
                                    Value = "<p>Sample</p>"
                                }
                            }
                        }
                    }
                }
            };

            CountingAiOrchestrator orchestrator = new();
            BibleRefreshService service = new(
                orchestrator,
                new StubEntitlementService(aiEnabled: false, planKey: "free"),
                new InMemoryBibleStore(),
                new BiblePatchApplier(),
                NullLogger<BibleRefreshService>.Instance);

            EntitlementDeniedException ex = await Assert.ThrowsAsync<EntitlementDeniedException>(() => service.RefreshAsync(
                document,
                "user-free",
                sectionId,
                BibleType.Character,
                fullRebuild: false,
                CancellationToken.None));

            Assert.Equal("ai.bibles.refresh", ex.FeatureKey);
            Assert.Equal("free", ex.PlanKey);
            Assert.Equal(0, orchestrator.CallCount);
        }

        private sealed class FakeAiOrchestrator : IAiOrchestrator
        {
            private readonly string _payload;

            public FakeAiOrchestrator(string payload)
            {
                _payload = payload;
            }

            public IReadOnlyList<IAiAction> Actions => Array.Empty<IAiAction>();

            public IAiAction? GetAction(string actionId) => null;

            public bool CanRunAction(string actionId) => true;

            public AiStreamingCapabilities GetStreamingCapabilities(string actionId) => new(true, false);

            public Task<AiExecutionResult> ExecuteActionAsync(string actionId, AiActionInput input, CancellationToken ct)
            {
                AiProposal proposal = new(
                    Guid.NewGuid(),
                    input.ActiveSectionId,
                    "Refresh timeline bible",
                    actionId,
                    "test",
                    Guid.NewGuid(),
                    DateTime.UtcNow,
                    null,
                    new List<ProposedOperation>(),
                    new List<Guid>(),
                    "Refresh timeline bible",
                    "Document",
                    "refresh",
                    null,
                    _payload);

                return Task.FromResult(AiExecutionResult.Success(proposal));
            }

            public AiStreamingSession StreamActionAsync(string actionId, AiActionInput input, CancellationToken ct)
            {
                throw new NotSupportedException();
            }
        }

        private sealed class InMemoryBibleStore : IBibleStore
        {
            private readonly Dictionary<(Guid, BibleType), BibleSnapshotState> _states = new();

            public Task<BibleSnapshotState?> GetSnapshotAsync(Guid documentId, BibleType bibleType, CancellationToken ct)
            {
                _states.TryGetValue((documentId, bibleType), out BibleSnapshotState? state);
                return Task.FromResult(state);
            }

            public Task<BibleSnapshotState> UpsertSnapshotAsync(
                Guid documentId,
                BibleType bibleType,
                string contentJson,
                string sourceHash,
                BibleRefreshCursor cursor,
                BibleRefreshStats stats,
                CancellationToken ct)
            {
                DateTimeOffset now = DateTimeOffset.UtcNow;
                BibleSnapshotState state = new(
                    Guid.NewGuid(),
                    documentId,
                    bibleType,
                    1,
                    contentJson,
                    now,
                    now,
                    now,
                    sourceHash,
                    stats,
                    cursor);

                _states[(documentId, bibleType)] = state;
                return Task.FromResult(state);
            }
        }

        private sealed class StubEntitlementService : IEntitlementService
        {
            private readonly bool _aiEnabled;
            private readonly string _planKey;

            public StubEntitlementService(bool aiEnabled, string planKey)
            {
                _aiEnabled = aiEnabled;
                _planKey = planKey;
            }

            public Task<UserEntitlements> GetEntitlementsAsync(string userId)
            {
                Dictionary<string, string> entitlements = new(StringComparer.OrdinalIgnoreCase)
                {
                    ["ai.enabled"] = _aiEnabled ? "true" : "false"
                };

                return Task.FromResult(new UserEntitlements(userId, _planKey, _planKey, entitlements));
            }

            public Task<bool> HasAsync(string userId, string entitlementKey)
            {
                bool result = string.Equals(entitlementKey, "ai.enabled", StringComparison.OrdinalIgnoreCase) && _aiEnabled;
                return Task.FromResult(result);
            }

            public Task<int?> GetIntAsync(string userId, string entitlementKey) => Task.FromResult<int?>(null);

            public void InvalidateForUser(string userId)
            {
            }
        }

        private sealed class CountingAiOrchestrator : IAiOrchestrator
        {
            public int CallCount { get; private set; }

            public IReadOnlyList<IAiAction> Actions => Array.Empty<IAiAction>();

            public IAiAction? GetAction(string actionId) => null;

            public bool CanRunAction(string actionId) => true;

            public AiStreamingCapabilities GetStreamingCapabilities(string actionId) => new(true, false);

            public Task<AiExecutionResult> ExecuteActionAsync(string actionId, AiActionInput input, CancellationToken ct)
            {
                CallCount++;
                return Task.FromResult(AiExecutionResult.Blocked("unexpected", "Should not be called"));
            }

            public AiStreamingSession StreamActionAsync(string actionId, AiActionInput input, CancellationToken ct)
            {
                throw new NotSupportedException();
            }
        }
    }
}
