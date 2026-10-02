using WriterApp.Device.Shared.Services;
using WriterApp.Device.Shared.Storage;
using Xunit;

namespace WriterApp.Tests;

public sealed class LocalProjectDropTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "WriterApp.OutlineDrop", Guid.NewGuid().ToString("N"));
    private LocalDocumentRepository Repo => new(new FileLocalDocumentStore(_root));
    private static LocalProjectNode Find(LocalDocument d, string title) => d.Project!.Nodes.Single(n => n.Title == title);
    private static Guid[] Order(LocalDocument d, Guid? parent) => d.Project!.Nodes.Where(n => n.ParentId == parent && n.DeletionId is null).OrderBy(n => n.OrderIndex).Select(n => n.NodeId).ToArray();
    private async Task<LocalDocument> Book()
    {
        var d = await Repo.CreateProjectAsync("Book");
        var chapter = d.Project!.Nodes.Single(n => n.NodeType == "chapter");
        var first = d.Project.Nodes.Single(n => n.NodeType == "scene");
        d = await Repo.ChangeProjectAsync(d, new(LocalProjectAction.Rename, first.NodeId, Title: "A"));
        foreach (var title in new[] { "B", "C" }) d = await Repo.ChangeProjectAsync(d, new(LocalProjectAction.Create, ParentId: chapter.NodeId, NodeType: "scene", Title: title));
        return await Repo.ChangeProjectAsync(d, new(LocalProjectAction.Create, NodeType: "chapter", Title: "Empty chapter"));
    }
    private Task<LocalDocument> Drop(LocalDocument d, Guid id, Guid? target, string placement) =>
        LocalProjectDrop.Resolve(d.Project!, id, target, placement) is { } change ? Repo.ChangeProjectAsync(d, change) : Task.FromResult(d);

    [Theory]
    [InlineData("C", "A", "before", "C,A,B")]
    [InlineData("A", "B", "after", "B,A,C")]
    [InlineData("A", "C", "after", "B,C,A")]
    [InlineData("C", "B", "before", "A,C,B")]
    public async Task ReorderPersistsManuscriptOrderAndWriting(string source, string target, string placement, string expected)
    {
        var d = await Book();
        d = await Repo.SaveAsync(d with { Sections = d.Sections.Select(s => s with { Pages = s.Pages.Select(p => p with { Content = $"<p>{s.Title} 日本語</p>" }).ToArray() }).ToArray() });
        var pages = d.Sections.SelectMany(s => s.Pages).ToDictionary(p => p.PageId);
        var scene = Find(d, source);
        d = await Drop(d, scene.NodeId, Find(d, target).NodeId, placement);
        var reopened = (await Repo.LoadAsync(d.DocumentId))!;
        Assert.Equal(expected, string.Join(",", Order(reopened, scene.ParentId).Select(id => reopened.Project!.Nodes.Single(n => n.NodeId == id).Title)));
        Assert.Equal(expected, string.Join(",", reopened.Sections.Select(s => s.Title)));
        Assert.All(reopened.Sections.SelectMany(s => s.Pages), page => Assert.Equal(pages[page.PageId], page));
        LocalProjectStructure.Validate(reopened);
    }

    [Fact]
    public async Task CrossChapterInsertionAndEmptyChapterDropKeepIdentitiesAndNormalizeBothLists()
    {
        var d = await Book(); var a = Find(d, "A"); var b = Find(d, "B"); var c = Find(d, "C"); var chapter = Find(d, "Empty chapter");
        d = await Drop(d, a.NodeId, chapter.NodeId, "inside");
        d = await Drop(d, b.NodeId, a.NodeId, "before");
        Assert.Equal(new[] { b.NodeId, a.NodeId }, Order(d, chapter.NodeId));
        Assert.Equal(new[] { c.NodeId }, Order(d, c.ParentId));
        Assert.Equal(0, Find(d, "C").OrderIndex);
        Assert.Equal(a.SectionId, Find(d, "A").SectionId);
        Assert.Equal(new[] { c.SectionId, b.SectionId, a.SectionId }, d.Sections.Select(s => (Guid?)s.SectionId));
        var reopened = (await Repo.LoadAsync(d.DocumentId))!;
        Assert.Equal(new[] { b.NodeId, a.NodeId }, Order(reopened, chapter.NodeId));
        Assert.Equal(a.SectionId, Find(reopened, "A").SectionId);
    }

    [Fact]
    public async Task MovingChapterWithSubtreeIntoPartAndBackToRootKeepsItsScenes()
    {
        var d = await Book(); var a = Find(d, "A"); var chapterId = a.ParentId!.Value;
        d = await Repo.ChangeProjectAsync(d, new(LocalProjectAction.Create, NodeType: "part", Title: "Part"));
        var part = Find(d, "Part");
        d = await Drop(d, chapterId, part.NodeId, "inside");
        Assert.Equal(part.NodeId, d.Project!.Nodes.Single(n => n.NodeId == chapterId).ParentId);
        Assert.Equal(new[] { a.NodeId, Find(d, "B").NodeId, Find(d, "C").NodeId }, Order(d, chapterId));
        d = await Drop(d, chapterId, null, "inside");
        Assert.Null(d.Project!.Nodes.Single(n => n.NodeId == chapterId).ParentId);
        Assert.Equal(chapterId, Order(d, null)[^1]);
        Assert.Equal(chapterId, Find(d, "A").ParentId);
    }

    [Fact]
    public async Task SelfAndUnchangedDropsDoNotWriteAndInvalidDropsCannotCommit()
    {
        var d = await Book(); var a = Find(d, "A"); var b = Find(d, "B"); var c = Find(d, "C");
        Assert.Null(LocalProjectDrop.Resolve(d.Project!, a.NodeId, a.NodeId, "inside"));
        Assert.Null(LocalProjectDrop.Resolve(d.Project!, a.NodeId, b.NodeId, "before"));
        Assert.Null(LocalProjectDrop.Resolve(d.Project!, b.NodeId, a.NodeId, "after"));
        Assert.Null(LocalProjectDrop.Resolve(d.Project!, c.NodeId, c.ParentId, "inside"));
        var before = await File.ReadAllBytesAsync(Path.Combine(_root, $"{d.DocumentId:N}.json"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Drop(d, a.NodeId, null, "inside"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Drop(d, a.NodeId, b.NodeId, "inside"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Drop(d, a.ParentId!.Value, a.NodeId, "inside"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Drop(d, a.NodeId, Guid.NewGuid(), "before"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Drop(d, a.NodeId, b.NodeId, "invalid"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Repo.ChangeProjectAsync(d, new(LocalProjectAction.Move, a.NodeId, a.ParentId, BeforeNodeId: Find(d, "Empty chapter").NodeId)));
        Assert.Equal(before, await File.ReadAllBytesAsync(Path.Combine(_root, $"{d.DocumentId:N}.json")));
        d = await Repo.ChangeProjectAsync(d, new(LocalProjectAction.Delete, b.NodeId));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Drop(d, a.NodeId, b.NodeId, "before"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Drop(d, b.NodeId, a.NodeId, "before"));
    }

    [Fact]
    public async Task StaleDropCannotOverwriteNewWriting()
    {
        var d = await Book(); var a = Find(d, "A");
        var session = new LocalEditorSession(Repo, d);
        var page = d.Sections.Single(s => s.SectionId == a.SectionId).Pages[0];
        session.Edit(page.PageId, "<p>Newest writing</p>"); await session.SaveAsync();
        await Assert.ThrowsAsync<LocalDocumentConflictException>(() => Drop(d, a.NodeId, Find(d, "C").NodeId, "after"));
        var reopened = (await Repo.LoadAsync(d.DocumentId))!;
        Assert.Equal("<p>Newest writing</p>", reopened.Sections.Single(s => s.SectionId == a.SectionId).Pages[0].Content);
        Assert.Equal(new[] { a.NodeId, Find(d, "B").NodeId, Find(d, "C").NodeId }, Order(reopened, a.ParentId));
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
