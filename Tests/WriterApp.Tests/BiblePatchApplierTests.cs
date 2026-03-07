using System.Text.Json;
using WriterApp.Application.Continuity;
using Xunit;

namespace WriterApp.Tests
{
    public sealed class BiblePatchApplierTests
    {
        [Fact]
        public void TryApply_CharacterFullPayload_AcceptsAsFullPayload()
        {
            BiblePatchApplier applier = new();
            string existing = BibleJson.EmptyBibleContent(BibleType.Character);
            string payload = """
            {
              "schemaVersion":"1.0",
              "characters":[
                {
                  "name":"Anna",
                  "facts":[
                    {
                      "fact":"Carries a brass key",
                      "evidence":{"sectionId":"11111111-1111-1111-1111-111111111111","quote":"Anna pressed the brass key into her palm."}
                    }
                  ],
                  "traits":["guarded"]
                }
              ]
            }
            """;

            bool ok = applier.TryApply(BibleType.Character, existing, payload, out BiblePatchApplyResult result, out string failureReason);

            Assert.True(ok);
            Assert.True(string.IsNullOrWhiteSpace(failureReason));
            using JsonDocument doc = JsonDocument.Parse(result.ContentJson);
            JsonElement characters = doc.RootElement.GetProperty("characters");
            Assert.Equal(1, characters.GetArrayLength());
            Assert.Equal("Anna", characters[0].GetProperty("name").GetString());
        }

        [Fact]
        public void TryApply_MalformedJson_ReturnsUsefulFailureReason()
        {
            BiblePatchApplier applier = new();
            string existing = BibleJson.EmptyBibleContent(BibleType.Character);
            string payload = """
            {
              "schemaVersion":"1.0",
              "characters":[{"name":"Anna","facts":[{"fact":"Broken "quote""}]}]
            }
            """;

            bool ok = applier.TryApply(BibleType.Character, existing, payload, out BiblePatchApplyResult _, out string failureReason);

            Assert.False(ok);
            Assert.Contains("JSON could not be parsed into an object", failureReason);
            Assert.Contains("BytePositionInLine", failureReason);
        }

        [Fact]
        public void TryApply_TimelinePatchWithOperationsAlias_AppliesSuccessfully()
        {
            BiblePatchApplier applier = new();
            string existing = BibleJson.EmptyBibleContent(BibleType.Timeline);
            string payload = """
            {
              "bibleType":"Timeline",
              "schemaVersion":1,
              "operations":[
                {
                  "op":"upsertTimelineEvent",
                  "id":"evt_1",
                  "data":{
                    "id":"evt_1",
                    "title":"Dock arrival",
                    "timeRef":"Day 1",
                    "order":1,
                    "summary":"Arrival in harbor",
                    "participants":["chr_1"],
                    "evidence":[{"sectionId":null,"quote":"She arrived at dusk."}],
                    "constraints":[]
                  }
                }
              ]
            }
            """;

            bool ok = applier.TryApply(BibleType.Timeline, existing, payload, out BiblePatchApplyResult result);

            Assert.True(ok);
            using JsonDocument doc = JsonDocument.Parse(result.ContentJson);
            JsonElement events = doc.RootElement.GetProperty("events");
            Assert.Equal(1, events.GetArrayLength());
            Assert.Equal("evt_1", events[0].GetProperty("id").GetString());
        }

        [Fact]
        public void TryApply_TimelinePayloadInsideMarkdownFence_AppliesSuccessfully()
        {
            BiblePatchApplier applier = new();
            string existing = BibleJson.EmptyBibleContent(BibleType.Timeline);
            string payload = """
            ```json
            {"bibleType":"Timeline","schemaVersion":1,"ops":[{"op":"upsertTimelineEvent","id":"evt_2","data":{"id":"evt_2","title":"Departure","timeRef":"Day 2","order":2,"summary":"Leaves harbor","participants":[],"evidence":[],"constraints":[]}}]}
            ```
            """;

            bool ok = applier.TryApply(BibleType.Timeline, existing, payload, out BiblePatchApplyResult result);

            Assert.True(ok);
            using JsonDocument doc = JsonDocument.Parse(result.ContentJson);
            JsonElement events = doc.RootElement.GetProperty("events");
            Assert.Equal(1, events.GetArrayLength());
            Assert.Equal("evt_2", events[0].GetProperty("id").GetString());
        }

        [Fact]
        public void TryApply_TimelineFullPayloadWithAliasKey_AcceptsAsFullPayload()
        {
            BiblePatchApplier applier = new();
            string existing = BibleJson.EmptyBibleContent(BibleType.Timeline);
            string payload = """
            {
              "schemaVersion":"1.0",
              "timelineEvents":[
                {
                  "id":"evt_3",
                  "title":"Festival",
                  "timeRef":"Week 1",
                  "order":3,
                  "summary":"Town festival",
                  "participants":[],
                  "evidence":[],
                  "constraints":[]
                }
              ]
            }
            """;

            bool ok = applier.TryApply(BibleType.Timeline, existing, payload, out BiblePatchApplyResult result);

            Assert.True(ok);
            using JsonDocument doc = JsonDocument.Parse(result.ContentJson);
            JsonElement events = doc.RootElement.GetProperty("events");
            Assert.Equal(1, events.GetArrayLength());
            Assert.Equal("evt_3", events[0].GetProperty("id").GetString());
        }

        [Fact]
        public void TryApply_TimelinePatchWithTrailingCommas_AppliesSuccessfully()
        {
            BiblePatchApplier applier = new();
            string existing = BibleJson.EmptyBibleContent(BibleType.Timeline);
            string payload = """
            {
              "bibleType":"Timeline",
              "schemaVersion":1,
              "ops":[
                {
                  "op":"upsertTimelineEvent",
                  "id":"evt_4",
                  "data":{
                    "id":"evt_4",
                    "title":"Signal flare",
                    "order":4,
                    "summary":"A flare is launched",
                  },
                },
              ],
            }
            """;

            bool ok = applier.TryApply(BibleType.Timeline, existing, payload, out BiblePatchApplyResult result);

            Assert.True(ok);
            using JsonDocument doc = JsonDocument.Parse(result.ContentJson);
            JsonElement events = doc.RootElement.GetProperty("events");
            Assert.Equal(1, events.GetArrayLength());
            Assert.Equal("evt_4", events[0].GetProperty("id").GetString());
        }

        [Fact]
        public void TryApply_TimelinePatchWithSmartQuotes_AppliesSuccessfully()
        {
            BiblePatchApplier applier = new();
            string existing = BibleJson.EmptyBibleContent(BibleType.Timeline);
            string payload = """
            {
              “bibleType”:“Timeline”,
              “schemaVersion”:1,
              “ops”:[
                {
                  “op”:“upsertTimelineEvent”,
                  “id”:“evt_5”,
                  “data”:{
                    “id”:“evt_5”,
                    “title”:“Camp arrival”,
                    “order”:5,
                    “summary”:“The team reaches camp”
                  }
                }
              ]
            }
            """;

            bool ok = applier.TryApply(BibleType.Timeline, existing, payload, out BiblePatchApplyResult result);

            Assert.True(ok);
            using JsonDocument doc = JsonDocument.Parse(result.ContentJson);
            JsonElement events = doc.RootElement.GetProperty("events");
            Assert.Equal(1, events.GetArrayLength());
            Assert.Equal("evt_5", events[0].GetProperty("id").GetString());
        }
    }
}
