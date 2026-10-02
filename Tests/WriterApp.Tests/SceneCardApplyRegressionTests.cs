using WriterApp.Application.Documents;
using WriterApp.Device.Shared.Services;
using WriterApp.Device.Shared.Storage;
using Xunit;

namespace WriterApp.Tests;

public sealed class SceneCardApplyRegressionTests
{
    private const string Response = """
        { "narrativeRole": "Setup", "narrativeIntent": "Introduce Elin's return to Vinterhamn and establish a tense but familiar reunion with Jonas.",
          "emotionalBeat": "Nostalgia mixed with unease and unresolved feelings.",
          "keyEvents": "Elin arrives at Vinterhamn train station; meets Jonas unexpectedly.",
          "openQuestions": "Why has Elin avoided returning for eleven years?", "povCharacterId": "Elin",
          "placeId": "Vinterhamn_train_station", "timelineEventId": "", "timeRef": "shortly after sunset",
          "tags": ["arrival", "reunion"], "references": [] }
        """;

    [Fact]
    public async Task JsonOnlySceneProposalAppliesAfterMetadataOnlyRevisionAndSurvivesReload()
    {
        string root = Path.Combine(Path.GetTempPath(), "WriterApp.SceneApply", Guid.NewGuid().ToString("N"));
        try
        {
            var store = new FileLocalDocumentStore(root);
            var source = await store.CreateProjectAsync("Book");
            source = await store.SaveAsync(source with { Sections = source.Sections.Select(s => s with
            { Pages = s.Pages.Select(p => p with { Content = "<p>Elin returns.</p>" }).ToArray() }).ToArray() });
            source = source with { ServerDocumentId = Guid.NewGuid(), ServerVersion = "v1", SyncState = LocalSyncState.Synced };
            source = await store.ApplySyncAsync(source, source.LocalRevision, default, projects: true);
            var prepared = AdvancedAiRequests.Build(source, source.Sections[0].SectionId, AdvancedAiAction.SceneCard);
            var proposal = new DeviceAiProposal(prepared.Request, Response, null, "", new());
            Assert.Null(proposal.SceneCard);
            var current = await store.ApplySyncAsync(source with { LastSyncedAtUtc = DateTimeOffset.UtcNow },
                source.LocalRevision, default, projects: true);
            var applied = AdvancedAiRequests.Apply(current, prepared, proposal);
            var card = applied.Project!.Nodes.Single(n => n.NodeId == prepared.NodeId).Card!;
            Assert.Equal("Setup", card.NarrativeRole);
            Assert.Equal("Nostalgia mixed with unease and unresolved feelings.", card.EmotionalBeat);
            Assert.Contains("Introduce Elin", card.NarrativeIntent);
            Assert.Contains("meets Jonas", card.KeyEvents);
            Assert.Contains("eleven years", card.OpenQuestions);
            Assert.Equal(current.Sections, applied.Sections);
            Assert.Equal(current.LocalRevision, applied.LocalRevision);
            var saved = await store.SaveAsync(applied);
            var reloaded = await new FileLocalDocumentStore(root).GetAsync(saved.DocumentId);
            Assert.Equal(card, reloaded!.Project!.Nodes.Single(n => n.NodeId == prepared.NodeId).Card);
            Assert.Equal("<p>Elin returns.</p>", reloaded.Sections[0].Pages[0].Content);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData("writing")]
    [InlineData("planning")]
    [InlineData("cloud")]
    public async Task MetadataToleranceStillRejectsChangedSource(string change)
    {
        string root = Path.Combine(Path.GetTempPath(), "WriterApp.SceneGuard", Guid.NewGuid().ToString("N"));
        try
        {
            var store = new FileLocalDocumentStore(root);
            var source = await store.CreateProjectAsync("Book");
            source = source with { ServerDocumentId = Guid.NewGuid(), ServerVersion = "v1", SyncState = LocalSyncState.Synced,
                Sections = source.Sections.Select(s => s with { Pages = s.Pages.Select(p => p with { Content = "<p>Original</p>" }).ToArray() }).ToArray() };
            var prepared = AdvancedAiRequests.Build(source, source.Sections[0].SectionId, AdvancedAiAction.SceneCard);
            var current = source with { LocalRevision = source.LocalRevision + 1 };
            current = change switch
            {
                "cloud" => current with { ServerVersion = "v2" },
                "planning" => LocalPlanning.Scene(current, prepared.NodeId!.Value, LocalPlanning.EmptyCard with { NarrativeIntent = "Edited by author" }, ""),
                _ => current with { Sections = current.Sections.Select(s => s with { Pages = s.Pages.Select(p => p with { Content = "<p>Edited by author</p>" }).ToArray() }).ToArray() }
            };
            Assert.Throws<InvalidOperationException>(() => AdvancedAiRequests.Apply(current, prepared,
                new DeviceAiProposal(prepared.Request, Response, null, "", new())));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
