using System;
using System.Collections.Generic;
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
        public async Task RefreshAsync_CharacterValidJson_Succeeds()
        {
            Document document = BuildDocument(out Guid sectionId);
            SequenceAiOrchestrator orchestrator = new("""
            {
              "schemaVersion":"1.0",
              "characters":[
                {
                  "name":"Mira",
                  "facts":[{"fact":"Carries a brass key","evidence":{"sectionId":"11111111-1111-1111-1111-111111111111","quote":"Mira carries a brass key."}}],
                  "traits":["observant"]
                }
              ]
            }
            """);

            BibleRefreshService service = BuildService(orchestrator);

            BibleSnapshotState snapshot = await service.RefreshAsync(
                document,
                "user-1",
                sectionId,
                BibleType.Character,
                fullRebuild: false,
                CancellationToken.None);

            using JsonDocument json = JsonDocument.Parse(snapshot.ContentJson);
            Assert.Equal("Mira", json.RootElement.GetProperty("characters")[0].GetProperty("name").GetString());
            Assert.Equal(1, orchestrator.CallCount);
        }

        [Fact]
        public async Task RefreshAsync_CharacterMarkdownFencedJson_SucceedsWithoutRetry()
        {
            Document document = BuildDocument(out Guid sectionId);
            SequenceAiOrchestrator orchestrator = new(
                """
                ```json
                {"schemaVersion":"1.0","characters":[{"name":"Mira","facts":[],"traits":["observant"]}]}
                ```
                """);

            BibleRefreshService service = BuildService(orchestrator);

            BibleSnapshotState snapshot = await service.RefreshAsync(
                document,
                "user-1",
                sectionId,
                BibleType.Character,
                fullRebuild: false,
                CancellationToken.None);

            using JsonDocument json = JsonDocument.Parse(snapshot.ContentJson);
            Assert.Equal("Mira", json.RootElement.GetProperty("characters")[0].GetProperty("name").GetString());
            Assert.Equal(1, orchestrator.CallCount);
        }

        [Fact]
        public async Task RefreshAsync_CharacterJsonWithExtraProse_SucceedsWithoutRetry()
        {
            Document document = BuildDocument(out Guid sectionId);
            SequenceAiOrchestrator orchestrator = new(
                """
                Here is the repaired payload:
                {"schemaVersion":"1.0","characters":[{"name":"Mira","facts":[],"traits":["observant"]}]}
                End of payload.
                """);

            BibleRefreshService service = BuildService(orchestrator);

            BibleSnapshotState snapshot = await service.RefreshAsync(
                document,
                "user-1",
                sectionId,
                BibleType.Character,
                fullRebuild: false,
                CancellationToken.None);

            using JsonDocument json = JsonDocument.Parse(snapshot.ContentJson);
            Assert.Equal("Mira", json.RootElement.GetProperty("characters")[0].GetProperty("name").GetString());
            Assert.Equal(1, orchestrator.CallCount);
        }

        [Fact]
        public async Task RefreshAsync_CharacterTruncatedJson_ThrowsAfterRepairFails()
        {
            Document document = BuildDocument(out Guid sectionId);
            SequenceAiOrchestrator orchestrator = new(
                "{\"schemaVersion\":\"1.0\",\"characters\":[{\"name\":\"Mira\"",
                "{\"schemaVersion\":\"1.0\",\"characters\":[{\"name\":\"Mira\"");

            BibleRefreshService service = BuildService(orchestrator);

            BibleRefreshInvalidPayloadException ex = await Assert.ThrowsAsync<BibleRefreshInvalidPayloadException>(() => service.RefreshAsync(
                document,
                "user-1",
                sectionId,
                BibleType.Character,
                fullRebuild: false,
                CancellationToken.None));

            Assert.True(ex.RepairAttempted);
            Assert.Contains("JSON could not be parsed into an object", ex.FailureReason);
            Assert.Equal(2, orchestrator.CallCount);
        }

        [Fact]
        public async Task RefreshAsync_CharacterRetrySuccessAfterParseFailure_Succeeds()
        {
            Document document = BuildDocument(out Guid sectionId);
            SequenceAiOrchestrator orchestrator = new(
                "{\"schemaVersion\":\"1.0\",\"characters\":[{\"name\":\"Mira\"",
                "{\"schemaVersion\":\"1.0\",\"characters\":[{\"name\":\"Mira\",\"facts\":[],\"traits\":[\"observant\"]}]}");

            BibleRefreshService service = BuildService(orchestrator);

            BibleSnapshotState snapshot = await service.RefreshAsync(
                document,
                "user-1",
                sectionId,
                BibleType.Character,
                fullRebuild: false,
                CancellationToken.None);

            using JsonDocument json = JsonDocument.Parse(snapshot.ContentJson);
            Assert.Equal("Mira", json.RootElement.GetProperty("characters")[0].GetProperty("name").GetString());
            Assert.Equal(2, orchestrator.CallCount);
            Assert.True(orchestrator.SawRepairAttempt);
        }

        [Fact]
        public async Task RefreshAsync_TimelineMalformedPatch_UsesFallbackAndSucceeds()
        {
            Document document = BuildDocument(out Guid sectionId, "<p>Test quote \"inside\" content.</p>");

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

            SequenceAiOrchestrator orchestrator = new(malformedPatch);
            BibleRefreshService service = BuildService(orchestrator);

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
            Document document = BuildDocument(out Guid sectionId);
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

        private static Document BuildDocument(out Guid sectionId, string content = "<p>Sample</p>")
        {
            Guid documentId = Guid.NewGuid();
            sectionId = Guid.NewGuid();
            return new Document
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
                                    Value = content
                                }
                            }
                        }
                    }
                }
            };
        }

        private static BibleRefreshService BuildService(IAiOrchestrator orchestrator)
        {
            return new BibleRefreshService(
                orchestrator,
                new StubEntitlementService(aiEnabled: true, planKey: "professional"),
                new InMemoryBibleStore(),
                new BiblePatchApplier(),
                NullLogger<BibleRefreshService>.Instance);
        }

        private sealed class SequenceAiOrchestrator : IAiOrchestrator
        {
            private readonly Queue<string> _payloads;

            public SequenceAiOrchestrator(params string[] payloads)
            {
                _payloads = new Queue<string>(payloads);
            }

            public int CallCount { get; private set; }

            public bool SawRepairAttempt { get; private set; }

            public IReadOnlyList<IAiAction> Actions => Array.Empty<IAiAction>();

            public IAiAction? GetAction(string actionId) => null;

            public bool CanRunAction(string actionId) => true;

            public AiStreamingCapabilities GetStreamingCapabilities(string actionId) => new(true, false);

            public Task<AiExecutionResult> ExecuteActionAsync(string actionId, AiActionInput input, CancellationToken ct)
            {
                CallCount++;
                SawRepairAttempt |= input.Options is not null
                    && input.Options.TryGetValue("repair_invalid_json", out object? value)
                    && string.Equals(value?.ToString(), bool.TrueString, StringComparison.OrdinalIgnoreCase);

                string payload = _payloads.Count > 0 ? _payloads.Dequeue() : "{}";
                AiProposal proposal = new(
                    Guid.NewGuid(),
                    input.ActiveSectionId,
                    "Refresh bible",
                    actionId,
                    "test",
                    Guid.NewGuid(),
                    DateTime.UtcNow,
                    null,
                    new List<ProposedOperation>(),
                    new List<Guid>(),
                    "Refresh bible",
                    "Document",
                    "refresh",
                    null,
                    payload);

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

            public PlanTier GetUserTier(UserEntitlements entitlements)
            {
                return _planKey.ToLowerInvariant() switch
                {
                    "professional" => PlanTier.Professional,
                    "standard" => PlanTier.Standard,
                    _ => PlanTier.Free
                };
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
