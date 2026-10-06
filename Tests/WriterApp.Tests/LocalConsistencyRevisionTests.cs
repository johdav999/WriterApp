using System.Text.Json;
using WriterApp.Device.Shared.Services;
using WriterApp.Device.Shared.Storage;
using Xunit;

namespace WriterApp.Tests;

public sealed class LocalConsistencyRevisionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "consistency-tests-" + Guid.NewGuid().ToString("N"));
    private async Task<LocalDocument> Book()
    {
        var store = new FileLocalDocumentStore(_root);
        var doc = await store.CreateProjectAsync("Book");
        doc = await store.SaveAsync(doc with { Sections = doc.Sections.Select(s => s with { Pages = s.Pages.Select(p => p with {
            Content = "<p>The train arrived. Elin carried a heavy suitcase.</p>", ContentFormat = LocalContentFormat.Html
        }).ToArray() }).ToArray() });
        return await store.ApplySyncAsync(doc with { ServerDocumentId = Guid.NewGuid(), ServerVersion = "v1", SyncState = LocalSyncState.Synced },
            doc.LocalRevision, default, projects: true);
    }
    private static DeviceAiProposal Report(AdvancedAiPrepared prepared, string quote, string fix, int start, int length, Guid? sectionId = null) =>
        new(prepared.Request, JsonSerializer.Serialize(new { schemaVersion = "1.0", issues = new[] { new {
            severity = "low", type = "timeline", message = "Review suitcase weight", evidence = new {
                sectionId = sectionId ?? prepared.SectionId, quote }, suggestedFix = fix,
            anchor = new { plainTextStart = start, plainTextLength = length }
        } } }), null, "", new());

    [Fact]
    public async Task BroadEvidenceUsesVerifiedPhraseAnchorAndRejectsOtherScenes()
    {
        var doc = await Book();
        var prepared = AdvancedAiRequests.Build(doc, doc.Sections[0].SectionId, AdvancedAiAction.Consistency);
        var report = Report(prepared, "The train arrived. Elin carried a heavy suitcase.", "light", 34, 5);
        var revision = LocalConsistencyRevisions.Resolve(prepared, report, 0);
        Assert.Equal("heavy", revision.Original);
        Assert.Equal(doc.Sections[0].Pages[0].PageId, revision.PageId);
        Assert.Throws<InvalidDataException>(() => LocalConsistencyRevisions.Resolve(prepared,
            Report(prepared, "heavy", "light", 34, 5, Guid.NewGuid()), 0));
    }

    [Fact]
    public async Task UnverifiedAnchorFallsBackToUniqueEvidenceAndRejectsMissingEvidence()
    {
        var doc = await Book();
        var prepared = AdvancedAiRequests.Build(doc, doc.Sections[0].SectionId, AdvancedAiAction.Consistency);
        Assert.Equal("heavy", LocalConsistencyRevisions.Resolve(prepared, Report(prepared, "heavy", "light", 0, 3), 0).Original);
        Assert.Throws<InvalidOperationException>(() => LocalConsistencyRevisions.Resolve(prepared, Report(prepared, "Missing writing", "light", 0, 3), 0));
        Assert.Throws<InvalidDataException>(() => LocalConsistencyRevisions.Resolve(prepared, Report(prepared, "heavy", "light", 0, 3), 1));
    }

    [Fact]
    public async Task ApprovalPersistsOnlyTargetPageAndHistoryCanUndoAndRedoAfterRestart()
    {
        var before = await Book();
        var prepared = AdvancedAiRequests.Build(before, before.Sections[0].SectionId, AdvancedAiAction.Consistency);
        var revision = LocalConsistencyRevisions.Resolve(prepared, Report(prepared, "heavy", "light", 34, 5), 0);
        var repository = new LocalDocumentRepository(new FileLocalDocumentStore(_root));
        var after = await repository.SaveAsync(LocalConsistencyRevisions.Apply(before, before, revision,
            "<p>The train arrived. Elin carried a light suitcase.</p>"));
        Assert.Equal(JsonSerializer.Serialize(before.Project), JsonSerializer.Serialize(after.Project));
        var entry = new LocalAiHistory(1, Guid.NewGuid(), before.DocumentId, "continuity.apply_suggestion", "Manuscript revision",
            before.LocalRevision, before.ServerVersion, DateTimeOffset.UtcNow, "Applied", before, revision.Proposed,
            PageId: revision.PageId, OriginalText: revision.Original, After: after);
        var history = new LocalAiStore(Path.Combine(_root, "ai"));
        await history.SaveHistoryAsync(entry);
        repository = new(new FileLocalDocumentStore(_root));
        var actions = new LocalAiHistoryActions(repository, new LocalAiStore(Path.Combine(_root, "ai")));
        await actions.ChangeAsync(before.DocumentId, entry.Id, false);
        Assert.Equal(before.Sections[0].Pages[0].Content, (await repository.LoadAsync(before.DocumentId))!.Sections[0].Pages[0].Content);
        await actions.ChangeAsync(before.DocumentId, entry.Id, true);
        Assert.Equal(after.Sections[0].Pages[0].Content, (await repository.LoadAsync(before.DocumentId))!.Sections[0].Pages[0].Content);
    }

    [Theory]
    [InlineData(35, 4)] // Starts inside heavy.
    [InlineData(34, 4)] // Ends inside heavy.
    public async Task MidWordAnchorFallsBackToUniqueEvidence(int start, int length)
    {
        var doc = await Book();
        var prepared = AdvancedAiRequests.Build(doc, doc.Sections[0].SectionId, AdvancedAiAction.Consistency);
        const string quote = "The train arrived. Elin carried a heavy suitcase.";
        var revision = LocalConsistencyRevisions.Resolve(prepared, Report(prepared, quote,
            "The train arrived. Elin carried a light suitcase.", start, length), 0);
        Assert.Equal(quote, revision.Original);
        Assert.Equal(0, revision.Start);
    }

    [Fact]
    public async Task InvalidAnchorDoesNotDisambiguateRepeatedEvidence()
    {
        var doc = await Book();
        var page = doc.Sections[0].Pages[0];
        doc = doc with { Sections = [doc.Sections[0] with { Pages = [page with { Content = page.Content + page.Content }] }] };
        var prepared = AdvancedAiRequests.Build(doc, doc.Sections[0].SectionId, AdvancedAiAction.Consistency);
        Assert.Throws<InvalidOperationException>(() => LocalConsistencyRevisions.Resolve(prepared,
            Report(prepared, "heavy", "light", 35, 4), 0));
    }

    [Fact]
    public async Task LaterEditsBlockApprovalAndMultipleRevisionsUseLatestSavedSource()
    {
        var before = await Book();
        var prepared = AdvancedAiRequests.Build(before, before.Sections[0].SectionId, AdvancedAiAction.Consistency);
        var revision = LocalConsistencyRevisions.Resolve(prepared, Report(prepared, "heavy", "light", 34, 5), 0);
        Assert.Throws<InvalidOperationException>(() => LocalConsistencyRevisions.Apply(before with { Title = "Later title" }, before, revision, "<p>light</p>"));
        var next = LocalConsistencyRevisions.Apply(before, before, revision, "<p>The train arrived. Elin carried a light suitcase.</p>") with { SyncState = LocalSyncState.PendingUpload, LocalRevision = before.LocalRevision + 1 };
        var second = LocalConsistencyRevisions.Resolve(prepared, Report(prepared, "The train arrived.", "The train left.", 0, 18), 0);
        var result = LocalConsistencyRevisions.Apply(next, next, second, "<p>The train left. Elin carried a light suitcase.</p>");
        Assert.Contains("light suitcase", result.Sections[0].Pages[0].Content);
        Assert.Contains("train left", result.Sections[0].Pages[0].Content);
    }
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
