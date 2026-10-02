using System.Text.Json;
using System.Text.Json.Nodes;
using WriterApp.Device.Shared.Services;
using WriterApp.Device.Shared.Storage;
using Xunit;

namespace WriterApp.Tests;

public sealed class LocalProjectTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "WriterApp.ProjectTests", Guid.NewGuid().ToString("N"));
    private FileLocalDocumentStore Store => new(_root);
    private LocalDocumentRepository Repo => new(Store);
    private string PathFor(Guid id) => Path.Combine(_root, $"{id:N}.json");
    private Task<LocalDocument> Change(LocalDocument d, LocalProjectAction action, Guid id = default, Guid? parent = null, string? type = null, string? title = null) =>
        Repo.ChangeProjectAsync(d, new(action, id, parent, type, title));

    [Fact]
    public async Task OfflineProjectOrganizeWriteRestartResumeRetainsCanonicalIdentity()
    {
        var d = await Repo.CreateProjectAsync("Boken 日本語");
        var projectId = d.Project!.ProjectId;
        var chapter = d.Project.Nodes.Single(n => n.NodeType == "chapter");
        d = await Change(d, LocalProjectAction.Create, type: "part", title: "Part I");
        var part = d.Project!.Nodes.Single(n => n.NodeType == "part");
        d = await Change(d, LocalProjectAction.Move, chapter.NodeId, part.NodeId);
        d = await Change(d, LocalProjectAction.Create, parent: chapter.NodeId, type: "scene", title: "Second scene");
        var scene = d.Project!.Nodes.Single(n => n.Title == "Second scene");
        var page = d.Sections.Single(s => s.SectionId == scene.SectionId).Pages[0];
        var session = new LocalEditorSession(Repo, d);
        session.Edit(page.PageId, "<p>Räksmörgås café 日本語</p>"); session.RememberPage(page.PageId); await session.SaveAsync();
        d = await Change(session.Document, LocalProjectAction.Up, scene.NodeId);
        Assert.Equal(scene.SectionId, d.Sections[0].SectionId);
        d = await Change(d, LocalProjectAction.Rename, scene.NodeId, title: "Opening");
        d = await Change(d, LocalProjectAction.RenameProject, title: "Finished book");
        var reopened = (await Store.GetAsync(d.DocumentId))!;
        Assert.Equal(projectId, reopened.Project!.ProjectId);
        Assert.Equal(d.DocumentId, reopened.Project.ManuscriptId);
        Assert.Null(reopened.Project.ServerProjectId); Assert.Null(reopened.ServerDocumentId);
        Assert.Equal(page.PageId, reopened.Project.LastPageId);
        Assert.Equal("Opening", reopened.Sections[0].Title);
        Assert.Equal("<p>Räksmörgås café 日本語</p>", reopened.Sections[0].Pages[0].Content);
        Assert.Equal(LocalSyncState.LocalOnly, reopened.SyncState);
    }

    [Fact]
    public async Task AttachPreservesStandaloneContentAndAllExistingIdentities()
    {
        var original = await Repo.CreateImportedAsync("Standalone", "<p>Keep this writing</p>");
        var before = await File.ReadAllBytesAsync(PathFor(original.DocumentId));
        Assert.Null((await Store.GetAsync(original.DocumentId))!.Project);
        Assert.Equal(before, await File.ReadAllBytesAsync(PathFor(original.DocumentId)));
        var attached = await Repo.AttachProjectAsync(original, "Project");
        Assert.Equal(original.DocumentId, attached.DocumentId);
        Assert.Equal(original.Sections[0].SectionId, attached.Project!.Nodes.Single(n => n.NodeType == "scene").SectionId);
        Assert.Equal(original.Sections[0].Pages[0], attached.Sections[0].Pages[0]);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Repo.AttachProjectAsync(attached, "Another"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Repo.SaveAsync(attached with { Project = null }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Repo.SaveAsync(attached with { Project = attached.Project with { ProjectId = Guid.NewGuid() } }));
    }

    [Fact]
    public async Task DeletingTreePreservesWritingAndRestoresOnlyItsOwnSubtree()
    {
        var d = await Repo.CreateProjectAsync("Book");
        var chapter = d.Project!.Nodes.Single(n => n.NodeType == "chapter");
        var first = d.Project.Nodes.Single(n => n.NodeType == "scene");
        d = await Change(d, LocalProjectAction.Delete, first.NodeId);
        d = await Change(d, LocalProjectAction.Create, parent: chapter.NodeId, type: "scene", title: "Second");
        var second = d.Project!.Nodes.Single(n => n.Title == "Second");
        var pageIds = d.Sections.SelectMany(s => s.Pages).Select(p => p.PageId).ToArray();
        d = await Change(d, LocalProjectAction.Delete, chapter.NodeId);
        await Assert.ThrowsAsync<JsonException>(() => Change(d, LocalProjectAction.Restore, first.NodeId));
        d = await Change(d, LocalProjectAction.Restore, chapter.NodeId);
        Assert.NotNull(d.Project!.Nodes.Single(n => n.NodeId == first.NodeId).DeletionId);
        Assert.Null(d.Project.Nodes.Single(n => n.NodeId == second.NodeId).DeletionId);
        d = await Change(d, LocalProjectAction.Restore, first.NodeId);
        Assert.Equal(pageIds.Order(), d.Sections.SelectMany(s => s.Pages).Select(p => p.PageId).Order());
        Assert.All(d.Project!.Nodes, n => Assert.Null(n.DeletionId));
    }

    [Theory]
    [InlineData("scene", null)]
    [InlineData("chapter", "chapter")]
    [InlineData("part", "chapter")]
    [InlineData("scene", "part")]
    [InlineData("unknown", null)]
    public async Task InvalidPlacementNeverCommits(string type, string? parentType)
    {
        var d = await Repo.CreateProjectAsync("Book");
        d = await Change(d, LocalProjectAction.Create, type: "part", title: "Part");
        Guid? parent = d.Project!.Nodes.FirstOrDefault(n => n.NodeType == parentType)?.NodeId;
        var before = await File.ReadAllBytesAsync(PathFor(d.DocumentId));
        await Assert.ThrowsAsync<JsonException>(() => Change(d, LocalProjectAction.Create, parent: parent, type: type, title: "Invalid"));
        Assert.Equal(before, await File.ReadAllBytesAsync(PathFor(d.DocumentId)));
    }

    [Fact]
    public async Task CyclesForeignParentsDuplicateSceneBindingsAndOrderingAreRejected()
    {
        var d = await Repo.CreateProjectAsync("Book"); var p = d.Project!;
        var chapter = p.Nodes.Single(n => n.NodeType == "chapter"); var scene = p.Nodes.Single(n => n.NodeType == "scene");
        await Assert.ThrowsAsync<JsonException>(() => Change(d, LocalProjectAction.Move, chapter.NodeId, chapter.NodeId));
        await Assert.ThrowsAsync<JsonException>(() => Change(d, LocalProjectAction.Move, chapter.NodeId, scene.NodeId));
        await Assert.ThrowsAsync<JsonException>(() => Change(d, LocalProjectAction.Move, scene.NodeId, Guid.NewGuid()));
        await Assert.ThrowsAsync<JsonException>(() => Repo.SaveAsync(d with { Project = p with { Nodes = p.Nodes.Append(scene with { NodeId = Guid.NewGuid(), OrderIndex = 99 }).ToArray() } }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Repo.SaveAsync(d with { Project = p with { ManuscriptId = Guid.NewGuid() } }));
        await Assert.ThrowsAsync<JsonException>(() => Repo.SaveAsync(d with { Project = p with { Nodes = p.Nodes.Append(chapter with { NodeId = Guid.NewGuid() }).ToArray() } }));
    }

    [Fact]
    public async Task AssociationGuardsTrashRestoreAndDuplicateAreNondestructive()
    {
        var d = await Repo.CreateProjectAsync("Book");
        await Assert.ThrowsAsync<InvalidOperationException>(() => Repo.ChangeStructureAsync(d, new(LocalStructureAction.DeleteSection, d.Sections[0].SectionId)));
        var copy = await Repo.DuplicateAsync(d.DocumentId);
        Assert.Null(copy.Project); Assert.NotEqual(d.Sections[0].Pages[0].PageId, copy.Sections[0].Pages[0].PageId);
        d = await Repo.MoveToTrashAsync(d);
        Assert.DoesNotContain((await Repo.ListAsync()).Documents, x => x.DocumentId == d.DocumentId);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Repo.PermanentlyDeleteAsync(d));
        var restored = await Repo.RestoreAsync(d);
        Assert.Equal(d.Project!.ProjectId, restored.Project!.ProjectId);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task PreviousSchemasMigrateWithoutAttachingAndPreserveExactBackup(int version)
    {
        var d = await Repo.CreateAsync("Old writing"); var path = PathFor(d.DocumentId);
        var json = JsonNode.Parse(await File.ReadAllBytesAsync(path))!; json["schemaVersion"] = version;
        json["document"]!.AsObject().Remove("project"); json["document"]!["futureField"] = "Keep";
        await File.WriteAllTextAsync(path, json.ToJsonString()); var original = await File.ReadAllBytesAsync(path);
        var migrated = (await Store.GetAsync(d.DocumentId))!;
        Assert.Null(migrated.Project); Assert.Equal("Keep", migrated.ExtensionData!["futureField"].GetString());
        Assert.Equal(original, await File.ReadAllBytesAsync(path + $".v{version}.bak"));
        Assert.Equal(4, JsonNode.Parse(await File.ReadAllBytesAsync(path))!["schemaVersion"]!.GetValue<int>());
    }

    [Fact]
    public async Task InterruptedProjectCommitLeavesPreviousWritingAndTreeIntact()
    {
        var d = await Repo.CreateProjectAsync("Book"); var original = await File.ReadAllBytesAsync(PathFor(d.DocumentId));
        var broken = new FileLocalDocumentStore(_root, TimeProvider.System, new AtomicDocumentWriter(_ => throw new IOException("Interrupted after flush")));
        var changed = LocalProjectStructure.Apply(d, new(LocalProjectAction.Create, ParentId: d.Project!.Nodes.Single(n => n.NodeType == "chapter").NodeId, NodeType: "scene", Title: "New"), DateTimeOffset.UtcNow);
        await Assert.ThrowsAsync<IOException>(() => broken.SaveAsync(changed));
        Assert.Equal(original, await File.ReadAllBytesAsync(PathFor(d.DocumentId)));
        await File.WriteAllTextAsync(PathFor(d.DocumentId) + ".partial.tmp", "partial");
        Assert.Single((await Store.ListAsync()).Documents);
        Assert.Equal(2, (await Store.GetAsync(d.DocumentId))!.Project!.Nodes.Count);
    }

    [Fact]
    public async Task InterruptedCreationDoesNotLeaveHalfProjectOrManuscript()
    {
        var broken = new FileLocalDocumentStore(_root, TimeProvider.System, new AtomicDocumentWriter(_ => throw new IOException("Interrupted")));
        await Assert.ThrowsAsync<IOException>(() => broken.CreateProjectAsync("Book"));
        Assert.Empty((await Store.ListAsync()).Documents);
    }

    [Fact]
    public async Task StaleProjectMutationCannotOverwriteWritingAndUnknownFieldsRoundTrip()
    {
        var d = await Repo.CreateProjectAsync("Book");
        var session = new LocalEditorSession(Repo, d); session.Edit(d.Sections[0].Pages[0].PageId, "<p>Newest</p>"); await session.SaveAsync();
        await Assert.ThrowsAsync<LocalDocumentConflictException>(() => Change(d, LocalProjectAction.RenameProject, title: "Stale"));
        d = session.Document;
        d = await Repo.SaveAsync(d with { Project = d.Project! with { ExtensionData = new() { ["future"] = JsonSerializer.SerializeToElement(new { keep = true }) } } });
        d = await Change(d, LocalProjectAction.RenameProject, title: "Fresh");
        Assert.True((await Store.GetAsync(d.DocumentId))!.Project!.ExtensionData!["future"].GetProperty("keep").GetBoolean());
        Assert.Equal("<p>Newest</p>", d.Sections[0].Pages[0].Content);
    }

    [Fact]
    public async Task DocumentSyncCannotReplaceOrBindLocalProjectAggregate()
    {
        var d = await Repo.CreateProjectAsync("Offline");
        await Assert.ThrowsAsync<InvalidOperationException>(() => Store.ApplySyncAsync(d with { Project = null, ServerDocumentId = Guid.NewGuid() }, d.LocalRevision, default));
        Assert.Equal(d.Project!.ProjectId, (await Store.GetAsync(d.DocumentId))!.Project!.ProjectId);
    }

    [Fact]
    public async Task FutureProjectVersionIsPreservedAndNotOverwritten()
    {
        var d = await Repo.CreateProjectAsync("Book"); var path = PathFor(d.DocumentId);
        var json = JsonNode.Parse(await File.ReadAllBytesAsync(path))!; json["document"]!["project"]!["version"] = 99;
        await File.WriteAllTextAsync(path, json.ToJsonString()); var before = await File.ReadAllBytesAsync(path);
        await Assert.ThrowsAsync<LocalDocumentReadException>(() => Store.GetAsync(d.DocumentId));
        Assert.Equal(before, await File.ReadAllBytesAsync(path));
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
