using WriterApp.Device.Shared.Services;
using WriterApp.Device.Shared.Storage;
using WriterApp.Shared.Sync;
using Xunit;

namespace WriterApp.Tests;

public sealed class LocalPlanningTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "WriterApp.PlanningTests", Guid.NewGuid().ToString("N"));
    private FileLocalDocumentStore Store => new(_root);
    private async Task<LocalDocument> Book()
    {
        var doc = await Store.CreateProjectAsync("Book");
        return await Store.SaveAsync(doc with { Sections = doc.Sections.Select(s => s with { Pages = s.Pages.Select(p => p with { Content = "<p>A unique anchor remains.</p>" }).ToArray() }).ToArray() });
    }
    [Fact]
    public async Task EditorContinuesSavingWritingAfterAdoptingAnInlinePlanningSave()
    {
        var document = await Book();
        var repository = new LocalDocumentRepository(Store);
        var session = new LocalEditorSession(repository, document);
        var pageId = document.Sections[0].Pages[0].PageId;
        var sceneId = document.Project!.Nodes.Single(n => n.NodeType == "scene").NodeId;
        session.Edit(pageId, "<p>Writing before the panel save.</p>");
        await session.SaveAsync();
        var planned = await repository.SaveAsync(LocalPlanning.Scene(session.Document, sceneId,
            LocalPlanning.EmptyCard with { Summary = "Saved from the right panel" }, "Scene notes"));
        session.AdoptSavedDocument(planned);
        session.Edit(pageId, "<p>Writing after the panel save. 日本語</p>");
        await session.SaveAsync();

        var reopened = (await new FileLocalDocumentStore(_root).GetAsync(document.DocumentId))!;
        Assert.Equal("<p>Writing after the panel save. 日本語</p>", reopened.Sections[0].Pages[0].Content);
        var scene = reopened.Project!.Nodes.Single(n => n.NodeId == sceneId);
        Assert.Equal("Saved from the right panel", scene.Card!.Summary);
        Assert.Equal("Scene notes", scene.Notes);
        Assert.False(session.IsDirty);
    }

    [Fact]
    public async Task PanelRevisionCannotReplacePendingManuscriptEdits()
    {
        var document = await Book();
        var repository = new LocalDocumentRepository(Store);
        var session = new LocalEditorSession(repository, document);
        var page = document.Sections[0].Pages[0];
        session.Edit(page.PageId, "<p>Unsaved writing remains here.</p>");
        var saved = await repository.SaveAsync(LocalPlanning.Synopsis(document, new(Logline: "Story intent")));

        Assert.Throws<InvalidOperationException>(() => session.AdoptSavedDocument(saved));
        Assert.Equal("<p>Unsaved writing remains here.</p>", session.ContentFor(page));
        Assert.True(session.IsDirty);
        await Assert.ThrowsAsync<LocalDocumentConflictException>(() => session.SaveAsync());
        Assert.Equal("Story intent", (await repository.LoadAsync(document.DocumentId))!.Project!.Synopsis!.Logline);
    }

    [Fact]
    public async Task PlanningRoundTripLeavesProseAndIdentitiesUntouched()
    {
        var d = await Book(); var original = d.Sections[0].Pages[0]; var node = d.Project!.Nodes.Single(n => n.NodeType == "scene");
        var updated = LocalPlanning.Scene(d, node.NodeId, LocalPlanning.EmptyCard with { Summary = "Scene summary", NarrativeRole = "Climax", SubplotTagsJson = "[\"mystery\"]" }, "Private notes");
        updated = LocalPlanning.Synopsis(updated, new(Logline: "Story hook", Stakes: "Everything"));
        await Store.SaveAsync(updated);
        var reopened = (await new FileLocalDocumentStore(_root).GetAsync(d.DocumentId))!;
        Assert.Equal(original, reopened.Sections[0].Pages[0]);
        Assert.Equal("Story hook", reopened.Project!.Synopsis!.Logline);
        Assert.Equal("Private notes", reopened.Project.Nodes.Single(n => n.NodeId == node.NodeId).Notes);
        Assert.Equal(3, reopened.Project.Version);
        Assert.False(File.Exists(Path.Combine(_root, $"{d.DocumentId:N}.json.project-v1.bak"))); // New projects already use the current format.
        await Assert.ThrowsAsync<LocalDocumentConflictException>(() => Store.SaveAsync(updated));
    }
    [Theory]
    [InlineData("<p>Before A unique anchor remains.</p>", false)]
    [InlineData("<p>Removed the passage.</p>", true)]
    [InlineData("<p>A unique anchor remains. A unique anchor remains.</p>", true)]
    public async Task AnchorsStayOrDetachWithoutGuessing(string content, bool detached)
    {
        var d = await Book(); var node = d.Project!.Nodes.Single(n => n.NodeType == "scene");
        d = await Store.SaveAsync(LocalPlanning.AddAnnotation(d, node.NodeId, "todo", "Check this", "A unique anchor"));
        d = await Store.SaveAsync(d with { Sections = d.Sections.Select(s => s with { Pages = s.Pages.Select(p => p with { Content = content }).ToArray() }).ToArray() });
        var annotation = d.Project!.Nodes.Single(n => n.NodeId == node.NodeId).Annotations.Single();
        Assert.Equal(detached, annotation.Value.AnchorDetached);
        Assert.Equal(0, annotation.Value.AnchorFrom);
        d = await Store.SaveAsync(LocalPlanning.Resolve(d, node.NodeId, annotation.LocalId, true));
        Assert.Equal("resolved", d.Project!.Nodes.Single(n => n.NodeId == node.NodeId).Annotations.Single().Value.Status);
        if (detached)
        {
            d = await Store.SaveAsync(d with { Sections = d.Sections.Select(s => s with { Pages = s.Pages.Select(p => p with { Content = "<p>A unique anchor remains.</p>" }).ToArray() }).ToArray() });
            Assert.False(d.Project!.Nodes.Single(n => n.NodeId == node.NodeId).Annotations.Single().Value.AnchorDetached);
            Assert.Equal(d.Sections[0].Pages[0].PageId, LocalAnnotationMarkup.PageFor(d, annotation.LocalId));
        }
    }
    [Fact]
    public async Task ExplicitRelinkingPersistsTheNewQuoteAndPreservesAnnotationIdentityAndResolution()
    {
        var document = await Book();
        var sceneId = document.Project!.Nodes.Single(n => n.NodeType == "scene").NodeId;
        document = await Store.SaveAsync(LocalPlanning.AddAnnotation(document, sceneId, "todo", "Review the facts", "Old wording"));
        var annotation = document.Project!.Nodes.Single(n => n.NodeId == sceneId).Annotations.Single();
        document = await Store.SaveAsync(LocalPlanning.Resolve(document, sceneId, annotation.LocalId, true));
        var before = document.Project!.Nodes.Single(n => n.NodeId == sceneId).Annotations.Single();
        Assert.Throws<InvalidOperationException>(() => LocalPlanning.Reanchor(document, sceneId, annotation.LocalId, "Missing"));
        document = await Store.SaveAsync(LocalPlanning.Reanchor(document, sceneId, annotation.LocalId, "unique anchor"));
        var reopened = (await new FileLocalDocumentStore(_root).GetAsync(document.DocumentId))!;
        var after = reopened.Project!.Nodes.Single(n => n.NodeId == sceneId).Annotations.Single();
        Assert.Equal(before with { Value = before.Value with { AnchorText = "unique anchor", AnchorDetached = false } }, after);
        Assert.Equal(document.Sections[0].Pages[0], reopened.Sections[0].Pages[0]);
        Assert.Single(LocalAnnotationMarkup.ForPage(reopened, reopened.Sections[0].Pages[0].PageId));
    }
    [Fact]
    public async Task PlanningSearchRevalidatesDeletionAndScopes()
    {
        var d = await Book(); var node = d.Project!.Nodes.Single(n => n.NodeType == "scene");
        d = await Store.SaveAsync(LocalPlanning.Scene(d,node.NodeId,LocalPlanning.EmptyCard with { Summary="Secret plot" },"Secret clue"));
        var search = new LocalDocumentSearch(Store);
        Assert.Empty((await search.SearchAsync("Secret")).Items);
        var found = Assert.Single((await search.SearchAsync("Secret", scope: LocalSearchScope.Planning)).Items);
        Assert.Equal(node.NodeId, found.PlanningNodeId);
        Assert.Equal("Story", found.PlanningView);
        d = await Store.SaveAsync(LocalPlanning.AddAnnotation(d,node.NodeId,"todo","Task-only keyword", ""));
        var task = Assert.Single((await search.SearchAsync("Task-only", scope: LocalSearchScope.Planning)).Items);
        Assert.Equal("Notes & Tasks", task.PlanningView);
        Assert.NotNull(await search.ResolveAsync(task,"Task-only"));
        d = await Store.SaveAsync(LocalProjectStructure.Apply(d,new(LocalProjectAction.Delete,node.NodeId),DateTimeOffset.UtcNow));
        Assert.Null(await search.ResolveAsync(found,"Secret"));
        Assert.Empty((await search.SearchAsync("Secret",scope:LocalSearchScope.All)).Items);
        d = await Store.SaveAsync(LocalProjectStructure.Apply(d,new(LocalProjectAction.Restore,node.NodeId),DateTimeOffset.UtcNow));
        Assert.Single((await search.SearchAsync("Secret",scope:LocalSearchScope.Planning)).Items);
        await Store.MoveToTrashAsync(d.DocumentId,d.LocalRevision);
        Assert.Empty((await search.SearchAsync("Secret",scope:LocalSearchScope.All)).Items);
    }
    [Fact]
    public async Task ConflictCopiesDetachAnnotationIdentitiesAndKeepEvidence()
    {
        var d = await Book(); var node=d.Project!.Nodes.Single(n=>n.NodeType=="scene");
        d=await Store.SaveAsync(LocalPlanning.AddAnnotation(d,node.NodeId,"comment","Keep evidence","unique"));
        var copy=DeviceSyncMapping.Copy(d,Guid.NewGuid()," copy");
        var annotation=copy.Project!.Nodes.Single(n=>n.NodeType=="scene").Annotations.Single();
        Assert.Null(annotation.ServerId);
        Assert.NotEqual(d.Project!.Nodes.Single(n=>n.NodeId==node.NodeId).Annotations.Single().LocalId,annotation.LocalId);
        Assert.Equal("Keep evidence",annotation.Value.Content);
    }
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root,true); }
}
