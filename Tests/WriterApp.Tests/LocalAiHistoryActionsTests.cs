using WriterApp.Device.Shared.Services;
using WriterApp.Device.Shared.Storage;
using WriterApp.Shared.Sync;
using Xunit;

namespace WriterApp.Tests;

public sealed class LocalAiHistoryActionsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ai-history-tests-" + Guid.NewGuid().ToString("N"));
    private LocalAiHistory Entry(LocalDocument before, LocalDocument after, string target, Guid? page = null, Guid? node = null) =>
        new(1, Guid.NewGuid(), before.DocumentId, page is not null ? "rewrite.selection" : node is not null ? "scene.suggest" : "synopsis.story_coach",
            target, before.LocalRevision, before.ServerVersion, DateTimeOffset.UtcNow, "Applied", before, "New", page,
            page is null ? null : after.Sections.SelectMany(s => s.Pages).Single(p => p.PageId == page).Content, NodeId: node, After: after);

    [Fact]
    public async Task SynopsisUndoRedoSurvivesRestartAndPreservesLaterUnrelatedEdits()
    {
        var repository = new LocalDocumentRepository(new FileLocalDocumentStore(_root));
        var before = await repository.CreateProjectAsync("Book");
        before = await repository.SaveAsync(LocalPlanning.Synopsis(before, new(Logline: "Original", Stakes: "Stakes")));
        var after = await repository.SaveAsync(LocalPlanning.Synopsis(before, before.Project!.Synopsis! with { Logline = "New" }));
        var entry = Entry(before, after, "SynopsisField:logline");
        var store = new LocalAiStore(Path.Combine(_root, "ai")); await store.SaveHistoryAsync(entry);
        after = await repository.SaveAsync(LocalPlanning.Synopsis(after, after.Project!.Synopsis! with { Stakes = "Later stakes" }));
        await new LocalAiHistoryActions(repository, store).ChangeAsync(before.DocumentId, entry.Id, false);
        var undone = (await repository.LoadAsync(before.DocumentId))!;
        Assert.Equal("Original", undone.Project!.Synopsis!.Logline);
        Assert.Equal("Later stakes", undone.Project.Synopsis.Stakes);
        Assert.Equal("Undone", Assert.Single(await store.HistoryAsync(before.DocumentId)).Status);
        store = new LocalAiStore(Path.Combine(_root, "ai"));
        repository = new LocalDocumentRepository(new FileLocalDocumentStore(_root));
        await new LocalAiHistoryActions(repository, store).ChangeAsync(before.DocumentId, entry.Id, true);
        var redone = (await repository.LoadAsync(before.DocumentId))!;
        Assert.Equal("New", redone.Project!.Synopsis!.Logline);
        Assert.Equal("Later stakes", redone.Project.Synopsis.Stakes);
        Assert.Equal("Applied", Assert.Single(await store.HistoryAsync(before.DocumentId)).Status);
        Assert.Equal(before.Sections[0].Pages[0].Content, redone.Sections[0].Pages[0].Content);
    }

    [Fact]
    public async Task LaterChangesToAffectedWritingBlockUndoWithoutMutatingHistoryOrDocument()
    {
        var repository = new LocalDocumentRepository(new FileLocalDocumentStore(_root));
        var before = await repository.CreateAsync("Book");
        Guid pageId = before.Sections[0].Pages[0].PageId;
        LocalDocument Text(LocalDocument doc, string text) => doc with { Sections = doc.Sections.Select(s => s with {
            Pages = s.Pages.Select(p => p with { Content = text, ContentFormat = LocalContentFormat.Html }).ToArray() }).ToArray() };
        before = await repository.SaveAsync(Text(before, "<p>Original</p>"));
        var after = await repository.SaveAsync(Text(before, "<p>AI writing</p>"));
        var entry = Entry(before, after, "Manuscript selection", pageId);
        var store = new LocalAiStore(Path.Combine(_root, "ai")); await store.SaveHistoryAsync(entry);
        var actions = new LocalAiHistoryActions(repository, store);
        await actions.ChangeAsync(before.DocumentId, entry.Id, false);
        Assert.Equal("<p>Original</p>", (await repository.LoadAsync(before.DocumentId))!.Sections[0].Pages[0].Content);
        await actions.ChangeAsync(before.DocumentId, entry.Id, true);
        after = await repository.LoadAsync(before.DocumentId) ?? throw new Exception();
        var later = await repository.SaveAsync(Text(after, "<p>My later writing</p>"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => actions.ChangeAsync(before.DocumentId, entry.Id, false));
        Assert.Equal(LocalDocumentCodec.Encode(later), LocalDocumentCodec.Encode((await repository.LoadAsync(before.DocumentId))!));
        Assert.Equal("Applied", Assert.Single(await store.HistoryAsync(before.DocumentId)).Status);
        // Old writing records with only AfterHtml still work.
        var legacy = entry with { After = null };
        Assert.Equal("<p>Original</p>", LocalAiHistoryActions.Change(after, legacy, false).Sections[0].Pages[0].Content);
    }

    [Fact]
    public async Task SceneUndoChangesOnlyAppliedFieldsAndPreservesNotesLinksAndStatus()
    {
        var repository = new LocalDocumentRepository(new FileLocalDocumentStore(_root));
        var before = await repository.CreateProjectAsync("Book");
        var scene = before.Project!.Nodes.Single(n => n.NodeType == "scene");
        var card = LocalPlanning.EmptyCard with { Summary = "Original", NarrativeIntent = "Keep intent" };
        before = await repository.SaveAsync(LocalPlanning.Scene(before, scene.NodeId, card, "Original notes"));
        var after = await repository.SaveAsync(LocalPlanning.Scene(before, scene.NodeId, card with { Summary = "New" }, "Original notes"));
        var entry = Entry(before, after, "SceneCard:", node: scene.NodeId);
        var current = LocalPlanning.Scene(after, scene.NodeId, card with { Summary = "New", NarrativeIntent = "Later intent", Status = "Final", PlaceId = "Harbor" }, "Later notes");
        var undone = LocalAiHistoryActions.Change(current, entry, false);
        var result = undone.Project!.Nodes.Single(n => n.NodeId == scene.NodeId);
        Assert.Equal("Original", result.Card!.Summary);
        Assert.Equal("Later intent", result.Card.NarrativeIntent);
        Assert.Equal("Final", result.Card.Status); Assert.Equal("Harbor", result.Card.PlaceId); Assert.Equal("Later notes", result.Notes);
        Assert.Equal("New", LocalAiHistoryActions.Change(undone, entry with { Status = "Undone" }, true).Project!.Nodes.Single(n => n.NodeId == scene.NodeId).Card!.Summary);
        Assert.Throws<InvalidOperationException>(() => LocalAiHistoryActions.Change(current, entry with { Status = "Reviewed" }, false));
    }

    [Theory]
    [InlineData(false, "Undoing", "Original", "Undone")]
    [InlineData(true, "Redoing", "New", "Applied")]
    public async Task InterruptedChangeCanFinishAfterDocumentWasAlreadySaved(bool redo, string pending, string value, string final)
    {
        var repository = new LocalDocumentRepository(new FileLocalDocumentStore(_root));
        var before = await repository.CreateProjectAsync("Book");
        before = await repository.SaveAsync(LocalPlanning.Synopsis(before, new(Logline: "Original")));
        var after = await repository.SaveAsync(LocalPlanning.Synopsis(before, new(Logline: "New")));
        var entry = Entry(before, after, "SynopsisField:logline") with { Status = pending };
        var store = new LocalAiStore(Path.Combine(_root, "ai")); await store.SaveHistoryAsync(entry);
        await repository.SaveAsync(LocalPlanning.Synopsis(after, new(Logline: value)));
        await new LocalAiHistoryActions(repository, store).ChangeAsync(before.DocumentId, entry.Id, redo);
        Assert.Equal(value, (await repository.LoadAsync(before.DocumentId))!.Project!.Synopsis!.Logline);
        Assert.Equal(final, Assert.Single(await store.HistoryAsync(before.DocumentId)).Status);
    }

    [Fact]
    public async Task OlderSynopsisActionsWorkAndHistoryEvidenceCannotBeReplaced()
    {
        var repository = new LocalDocumentRepository(new FileLocalDocumentStore(_root));
        var before = await repository.CreateProjectAsync("Book");
        before = LocalPlanning.Synopsis(before, new(Logline: "Original"));
        var after = LocalPlanning.Synopsis(before, new(Logline: "New"));
        var entry = Entry(before, after, "SynopsisField:logline");
        Assert.Equal("Original", LocalAiHistoryActions.Change(after, entry with { After = null }, false).Project!.Synopsis!.Logline);
        var store = new LocalAiStore(Path.Combine(_root, "ai")); await store.SaveHistoryAsync(entry);
        await Assert.ThrowsAsync<InvalidDataException>(() => store.SaveHistoryAsync(entry with { After = before }));
        await Assert.ThrowsAsync<InvalidDataException>(() => store.SaveHistoryAsync(entry with { Target = "SynopsisField:stakes" }));
        await Assert.ThrowsAsync<InvalidDataException>(() => store.SaveHistoryAsync(entry with { NodeId = Guid.NewGuid() }));
    }

    [Fact]
    public async Task HistoryAcceptanceFixtureContainsWritingPlanningAndReviewedResults()
    {
        var repository = new LocalDocumentRepository(new FileLocalDocumentStore(_root));
        var before = await repository.CreateProjectAsync("AI history acceptance");
        before = await repository.SaveAsync(LocalPlanning.Synopsis(before, new(Logline: "A merchant returns to the harbor.")));
        var after = await repository.SaveAsync(LocalPlanning.Synopsis(before, new(Logline: "A merchant returns to the harbor to uncover a family secret.")));
        var synopsis = Entry(before, after, "SynopsisField:logline") with { Proposed = after.Project!.Synopsis!.Logline,
            OriginalText = before.Project!.Synopsis!.Logline, CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-4) };
        var store = new LocalAiStore(Path.Combine(_root, "ai-library")); await store.SaveHistoryAsync(synopsis);
        await new LocalAiHistoryActions(repository, store).ChangeAsync(before.DocumentId, synopsis.Id, false);
        before = (await repository.LoadAsync(before.DocumentId))!;
        var section = before.Sections[0]; var page = section.Pages[0];
        before = await repository.SaveAsync(before with { Sections = [section with { Pages = [page with { Content = "<p>The harbor was quiet.</p>", ContentFormat = LocalContentFormat.Html }] }] });
        after = await repository.SaveAsync(before with { Sections = [section with { Pages = [page with { Content = "<p>The harbor lay quiet beneath the evening sky.</p>", ContentFormat = LocalContentFormat.Html }] }] });
        var writing = Entry(before, after, "Manuscript selection", page.PageId) with { Proposed = "The harbor lay quiet beneath the evening sky.", OriginalText = "The harbor was quiet.", CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-2) };
        await store.SaveHistoryAsync(writing);
        await store.SaveHistoryAsync(new(1, Guid.NewGuid(), after.DocumentId, "scene.suggest", "SceneCard:", after.LocalRevision, after.ServerVersion,
            DateTimeOffset.UtcNow, "Reviewed", after, "{\"narrativeRole\":\"Setup\",\"narrativeIntent\":\"Establish the merchant's return to the harbor.\",\"emotionalBeat\":\"Nostalgia and uncertainty.\",\"keyEvents\":\"The merchant arrives after eleven years.\",\"openQuestions\":\"What happened to the family business?\"}", OriginalText: "null", NodeId: after.Project!.Nodes.Single(n => n.NodeType == "scene").NodeId));
        Assert.Equal(3, (await store.HistoryAsync(before.DocumentId)).Count);
        // Opt-in, isolated native acceptance data; normal tests only use their temporary directory.
        if (Environment.GetEnvironmentVariable("PROSA_AI_HISTORY_UAT_ROOT") is { Length: > 0 } destination)
        {
            foreach (string file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories))
            {
                string target = Path.Combine(destination, "documents", Path.GetRelativePath(_root, file));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.Copy(file, target, overwrite: false);
            }
        }
    }
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
