using Microsoft.EntityFrameworkCore;
using WriterApp.Application.Documents;
using WriterApp.Data.Documents;
using WriterApp.Device.Shared.Services;
using WriterApp.Device.Shared.Storage;
using WriterApp.Shared.Sync;
using Xunit;

namespace WriterApp.Tests;

public sealed partial class DocumentSyncTests
{
    private async Task DeleteWebSceneAsync(ProjectNodeRecord scene)
    {
        // Match explicit dependent cleanup used by web deletion (SQL Server has non-cascading FKs).
        _db.SceneContents.RemoveRange(await _db.SceneContents.Where(n => n.SceneNodeId == scene.Id).ToListAsync());
        _db.SceneNotes.RemoveRange(await _db.SceneNotes.Where(n => n.SceneNodeId == scene.Id).ToListAsync());
        _db.SceneCards.RemoveRange(await _db.SceneCards.Where(n => n.SceneNodeId == scene.Id).ToListAsync());
        _db.ProjectNodes.Remove(scene);
    }
    private static SyncMutation ProjectRequest()
    {
        var request = Create("<p>Räksmörgås 日本語</p>");
        var chapter = new SyncProjectNode(Guid.NewGuid(), null, "chapter", "Chapter", 0, null);
        var scene = new SyncProjectNode(Guid.NewGuid(), chapter.Id, "scene", "Scene", 0, request.Document!.Sections[0].Id,
            MetadataJson: "{\"synopsis\":\"Keep this\"}", Notes: "Scene notes");
        return request with { Document = request.Document with { Project = new(Guid.NewGuid(), "Project", [chapter, scene], AuthorName: "Author") } };
    }

    [Fact]
    public async Task ProjectRoundTripPreservesMetadataLinksTombstonesAndIdempotency()
    {
        var request = ProjectRequest(); Guid id = Guid.NewGuid();
        var created = await _sync.MutateAsync("paid", id, request, projects: true, planning: true);
        Assert.Equal(created, await _sync.MutateAsync("paid", id, request, projects: true, planning: true));
        var snapshot = await _sync.DownloadAsync("paid", id, projects: true, planning: true);
        var local = DeviceSyncMapping.Download(snapshot, Guid.NewGuid(), null, DateTimeOffset.UtcNow);
        Assert.Equal(request.Document!.Project!.Id, local.Project!.ServerProjectId);
        Assert.NotEqual(local.Project.ProjectId, local.Project.ServerProjectId);
        Assert.Equal("Scene notes", local.Project.Nodes.Single(n => n.NodeType == "scene").Notes);
        Assert.Equal("Author", local.Project.AuthorName);
        var localScene = local.Project.Nodes.Single(n => n.NodeType == "scene");
        local = LocalProjectStructure.Apply(local, new(LocalProjectAction.Delete, localScene.NodeId), DateTimeOffset.UtcNow);
        var upload = DeviceSyncMapping.Upload(local);
        await _sync.MutateAsync("paid", id, new(Guid.NewGuid(), snapshot.State.Version, "upload", upload), projects: true, planning: true);
        Assert.Single(await _db.ProjectNodes.ToListAsync());
        Assert.Equal(2, await _db.ProjectNodes.IgnoreQueryFilters().CountAsync());
        Assert.Equal("Scene notes", (await _db.SceneNotes.SingleAsync()).NotesText);
        Assert.Equal("<p>Räksmörgås 日本語</p>", (await _db.SceneContents.SingleAsync()).ContentJson);
        snapshot = await _sync.DownloadAsync("paid", id, projects: true, planning: true);
        var reopened = DeviceSyncMapping.Download(snapshot, local.DocumentId, local, DateTimeOffset.UtcNow);
        Assert.Equal(localScene.NodeId, reopened.Project!.Nodes.Single(n => n.NodeType == "scene").NodeId);
        Assert.Equal(upload.Project!.Nodes.Single(n => n.NodeType == "scene").DeletionId,
            reopened.Project.Nodes.Single(n => n.NodeType == "scene").DeletionId);
        reopened = LocalProjectStructure.Apply(reopened, new(LocalProjectAction.Restore, localScene.NodeId), DateTimeOffset.UtcNow);
        await _sync.MutateAsync("paid", id, new(Guid.NewGuid(), snapshot.State.Version, "upload", DeviceSyncMapping.Upload(reopened)), projects: true, planning: true);
        Assert.Equal(2, await _db.ProjectNodes.CountAsync());
    }

    [Theory]
    [InlineData("rename")]
    [InlineData("notes")]
    [InlineData("card")]
    [InlineData("project")]
    [InlineData("delete")]
    public async Task WebProjectChangesInvalidateOfflineVersion(string action)
    {
        Guid id = Guid.NewGuid(); var request = ProjectRequest();
        var created = await _sync.MutateAsync("paid", id, request, projects: true, planning: true);
        var scene = await _db.ProjectNodes.SingleAsync(n => n.NodeType == ProjectNodeType.Scene);
        switch (action)
        {
            case "rename": scene.Title = "Web rename"; break;
            case "notes": (await _db.SceneNotes.SingleAsync()).NotesText = "Web notes"; break;
            case "card": _db.SceneCards.Add(new() { SceneNodeId = scene.Id, Summary = "Web card" }); break;
            case "project": (await _db.Projects.SingleAsync()).Title = "Web project"; break;
            case "delete": await DeleteWebSceneAsync(scene); break;
        }
        await _db.SaveChangesAsync();
        var current = await _sync.DownloadAsync("paid", id, projects: true, planning: true);
        Assert.NotEqual(created.State.Version, current.State.Version);
        var stale = request with { OperationId = Guid.NewGuid(), ExpectedVersion = created.State.Version };
        Assert.Equal(409, (await Assert.ThrowsAsync<DocumentSyncException>(() => _sync.MutateAsync("paid", id, stale, projects: true, planning: true))).Status);
        Assert.Contains((await _sync.ChangesAsync("paid", null, 100)).Changes, c => c.DocumentId == id && c.Version == current.State.Version);
    }

    [Fact]
    public async Task ProjectGuardsRejectOldClientsMissingParentsAndCrossOwnerWithoutPartialWrites()
    {
        Guid id = Guid.NewGuid(); var request = ProjectRequest();
        var created = await _sync.MutateAsync("paid", id, request, projects: true, planning: true);
        Assert.Equal(426, (await Assert.ThrowsAsync<DocumentSyncException>(() => _sync.MutateAsync("paid", id,
            new(Guid.NewGuid(), created.State.Version, "rename", Title: "Old client")))).Status);
        Assert.Equal(404, (await Assert.ThrowsAsync<DocumentSyncException>(() => _sync.DownloadAsync("other", id, projects: true, planning: true))).Status);
        _plans.Paid = false;
        Assert.Equal(403, (await Assert.ThrowsAsync<DocumentSyncException>(() => _sync.MutateAsync("paid", id, request, projects: true, planning: true))).Status);
        _plans.Paid = true;
        var project = request.Document!.Project!;
        var bad = request with { OperationId = Guid.NewGuid(), ExpectedVersion = created.State.Version,
            Document = request.Document with { Title = "Must roll back", Project = project with
                { Nodes = project.Nodes.Select(n => n.NodeType == "scene" ? n with { ParentId = Guid.NewGuid() } : n).ToArray() } } };
        Assert.Equal(400, (await Assert.ThrowsAsync<DocumentSyncException>(() => _sync.MutateAsync("paid", id, bad, projects: true, planning: true))).Status);
        bad = bad with { Document = request.Document with { Title = "Must roll back", Project = project with { Nodes = [] } } };
        Assert.Equal(400, (await Assert.ThrowsAsync<DocumentSyncException>(() => _sync.MutateAsync("paid", id, bad, projects: true, planning: true))).Status);
        Assert.Equal("Book", (await _sync.DownloadAsync("paid", id, projects: true, planning: true)).Document!.Title);
        Assert.Equal(created.State.Version, (await _sync.DownloadAsync("paid", id, projects: true, planning: true)).State.Version);
    }

    private sealed class ProjectApi(DocumentSyncService service) : IDeviceSyncApi
    {
        public string Owner = "paid";
        public bool LoseReply;
        public readonly List<Guid> Operations = [];
        public Task<string> GetOwnerAsync(CancellationToken ct) => Task.FromResult(Owner);
        private static async Task<T> Call<T>(Func<Task<T>> operation)
        { try { return await operation(); } catch (DocumentSyncException e) { throw new DeviceSyncApiException(e.Status, e.Error.Code, e.Message); } }
        public Task<SyncChanges> ChangesAsync(string? cursor, CancellationToken ct) => Call(() => service.ChangesAsync(Owner, cursor, 100, ct));
        public Task<SyncSnapshot> DownloadAsync(Guid id, CancellationToken ct) => Call(() => service.DownloadAsync(Owner, id, ct, projects: true, planning: true, multipleDocuments: true));
        public async Task<SyncMutationResult> MutateAsync(Guid id, SyncMutation request, CancellationToken ct)
        {
            Operations.Add(request.OperationId);
            var result = await Call(() => service.MutateAsync(Owner, id, request, ct, projects: true, planning: true, multipleDocuments: true));
            if (LoseReply) throw new IOException("Simulated lost acknowledgment");
            return result;
        }
    }

    private sealed class ProjectDevice : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "WriterApp.ProjectSync", Guid.NewGuid().ToString("N"));
        public FileLocalDocumentStore Store { get; }
        public DeviceConnectivity Network { get; } = new();
        public DeviceSyncEngine Engine { get; private set; } = null!;
        private readonly ProjectApi _api;
        public ProjectDevice(ProjectApi api) { _api = api; Store = new(_root); Restart(); }
        public void Restart(string backend = "https://test.invalid/")
        {
            Engine?.Dispose();
            Engine = new(Store, new(Path.Combine(_root, "sync")), _api, new(new UnconfiguredDeviceIdentityClient()), Network,
                new(Store), new("Test", new Uri(backend)));
        }
        public void Dispose() { Engine.Dispose(); if (Directory.Exists(_root)) Directory.Delete(_root, true); }
    }

    [Fact]
    public async Task TwoDevicesAndWebRoundTripWithOfflineRestartLostReplyAndAccountIsolation()
    {
        var api = new ProjectApi(_sync);
        using var first = new ProjectDevice(api); using var second = new ProjectDevice(api);
        var local = await first.Store.CreateProjectAsync("Offline project");
        api.LoseReply = true;
        await first.Engine.EnableAsync(local.DocumentId);
        Assert.Single(await _db.Documents.ToListAsync());
        api.LoseReply = false; first.Restart();
        await first.Engine.SyncAsync();
        Assert.Equal(api.Operations[0], api.Operations[1]);
        await second.Engine.SyncAsync();
        var other = Assert.Single((await second.Store.ListAsync()).Documents);
        Assert.NotEqual(local.Project!.ProjectId, other.Project!.ProjectId);
        Assert.Equal(local.Project.ProjectId, other.Project.ServerProjectId);
        first.Network.SetOnline(false);
        local = (await first.Store.GetAsync(local.DocumentId))!;
        local = await first.Store.SaveAsync(LocalProjectStructure.Apply(local, new(LocalProjectAction.RenameProject, Title: "Offline rename"), DateTimeOffset.UtcNow));
        first.Restart(); first.Network.SetOnline(true); await first.Engine.SyncAsync();
        Assert.Equal("Offline rename", (await _db.Projects.SingleAsync()).Title);
        var scene = await _db.ProjectNodes.SingleAsync(n => n.NodeType == ProjectNodeType.Scene);
        _db.SceneNotes.Add(new() { SceneNodeId = scene.Id, NotesText = "Web notes" }); await _db.SaveChangesAsync();
        await first.Engine.SyncAsync(); await second.Engine.SyncAsync();
        Assert.Equal("Web notes", (await second.Store.GetAsync(other.DocumentId))!.Project!.Nodes.Single(n => n.NodeType == "scene").Notes);
        api.Owner = "other"; first.Restart(); await first.Engine.SyncAsync();
        Assert.Null(first.Engine.Status(local.DocumentId));
        Assert.Equal("paid", (await _db.Projects.SingleAsync()).OwnerUserId);
        api.Owner = "paid"; first.Restart("https://different.invalid/"); await first.Engine.EnableAsync(local.DocumentId);
        Assert.Single(await _db.Documents.ToListAsync());
        Assert.Contains("linked elsewhere", first.Engine.Message);
    }

    [Theory]
    [InlineData("rename")]
    [InlineData("delete")]
    [InlineData("hard-delete")]
    [InlineData("move-delete")]
    public async Task DivergentProjectTreesKeepIndependentDurableCopies(string action)
    {
        var api = new ProjectApi(_sync); using var a = new ProjectDevice(api); using var b = new ProjectDevice(api);
        var original = await a.Store.CreateProjectAsync("Shared project"); await a.Engine.EnableAsync(original.DocumentId);
        await b.Engine.SyncAsync(); var remote = Assert.Single((await b.Store.ListAsync()).Documents);
        original = (await a.Store.GetAsync(original.DocumentId))!;
        Guid scene = original.Project!.Nodes.Single(n => n.NodeType == "scene").NodeId;
        var changed = LocalProjectStructure.Apply(original, new(LocalProjectAction.Rename, scene, Title: "Local rename"), DateTimeOffset.UtcNow);
        if (action == "move-delete")
        {
            changed = LocalProjectStructure.Apply(changed, new(LocalProjectAction.Create, NodeType: "chapter", Title: "New parent"), DateTimeOffset.UtcNow);
            changed = LocalProjectStructure.Apply(changed, new(LocalProjectAction.Move, scene,
                ParentId: changed.Project!.Nodes.Single(n => n.Title == "New parent").NodeId), DateTimeOffset.UtcNow);
        }
        await a.Store.SaveAsync(changed);
        if (action == "hard-delete")
        { await DeleteWebSceneAsync(await _db.ProjectNodes.SingleAsync(n => n.NodeType == ProjectNodeType.Scene)); await _db.SaveChangesAsync(); }
        else
        {
            Guid otherScene = remote.Project!.Nodes.Single(n => n.NodeType == "scene").NodeId;
            await b.Store.SaveAsync(LocalProjectStructure.Apply(remote, new(action is "delete" or "move-delete" ? LocalProjectAction.Delete : LocalProjectAction.Rename,
                otherScene, Title: "Remote rename"), DateTimeOffset.UtcNow));
            await b.Engine.SyncAsync();
        }
        await a.Engine.SyncAsync(); Assert.True(a.Engine.Status(original.DocumentId)!.Conflict);
        a.Restart(); await a.Engine.SyncAsync(); Assert.True(a.Engine.Status(original.DocumentId)!.Conflict);
        var copies = (await a.Store.ListAsync()).Documents;
        Assert.Equal(2, copies.Count);
        var backup = copies.Single(d => d.DocumentId != original.DocumentId);
        Assert.NotNull(backup.Project); Assert.Null(backup.ServerDocumentId); Assert.Null(backup.Project.ServerProjectId);
        Assert.All(backup.Project.Nodes, n => Assert.Null(n.ServerNodeId));
        Assert.Contains(copies.Single(d => d.DocumentId == original.DocumentId).Project!.Nodes, n => n.Title == "Local rename");
        bool keepLocal = action is "rename" or "move-delete";
        await a.Engine.ResolveAsync(original.DocumentId, keepLocal);
        Assert.False(a.Engine.Status(original.DocumentId)!.Conflict);
        Assert.Equal(3, (await a.Store.ListAsync()).Documents.Count);
        var final = await _sync.DownloadAsync("paid", (await a.Store.GetAsync(original.DocumentId))!.ServerDocumentId!.Value, projects: true, planning: true);
        if (keepLocal) Assert.Contains(final.Document!.Project!.Nodes, n => n.Title == "Local rename" && n.DeletionId is null);
        else if (action == "delete") Assert.NotNull(final.Document!.Project!.Nodes.Single(n => n.NodeType == "scene").DeletionId);
        else Assert.DoesNotContain(final.Document!.Project!.Nodes, n => n.NodeType == "scene");
    }

    [Fact]
    public async Task CleanLocalProjectPreservesWebHardDeletionAsConflict()
    {
        using var device = new ProjectDevice(new(_sync));
        var doc = await device.Store.CreateProjectAsync("Tree"); await device.Engine.EnableAsync(doc.DocumentId);
        await DeleteWebSceneAsync(await _db.ProjectNodes.SingleAsync(n => n.NodeType == ProjectNodeType.Scene));
        await _db.SaveChangesAsync(); await device.Engine.SyncAsync();
        Assert.True(device.Engine.Status(doc.DocumentId)!.Conflict);
        Assert.Contains((await device.Store.GetAsync(doc.DocumentId))!.Project!.Nodes, n => n.NodeType == "scene");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PermanentDeletionCleansReferencesToSoftDeletedNodes(bool wholeProject)
    {
        Guid id = Guid.NewGuid(); var request = ProjectRequest();
        request = request with { Document = request.Document! with { Project = request.Document.Project! with
        { Nodes = request.Document.Project.Nodes.Select(n => n with { DeletionId = Guid.NewGuid() }).ToArray() } } };
        var created = await _sync.MutateAsync("paid", id, request, projects: true, planning: true);
        if (wholeProject)
        {
            var deletion = new ProjectDeletionService(_db, Microsoft.Extensions.Logging.Abstractions.NullLogger<ProjectDeletionService>.Instance);
            await deletion.DeleteOwnedProjectAsync(request.Document.Project!.Id, "paid", default);
            Assert.Empty(await _db.ProjectNodes.IgnoreQueryFilters().ToListAsync());
            Assert.Empty(await _db.SceneNotes.ToListAsync());
        }
        else
        {
            var trashed = await _sync.MutateAsync("paid", id, new(Guid.NewGuid(), created.State.Version, "trash"), projects: true, planning: true);
            await _sync.MutateAsync("paid", id, new(Guid.NewGuid(), trashed.State.Version, "delete"), projects: true, planning: true);
            Assert.All(await _db.ProjectNodes.IgnoreQueryFilters().ToListAsync(), n => Assert.Null(n.LinkedSectionId));
        }
        Assert.True((await _sync.DownloadAsync("paid", id, projects: true, planning: true)).State.IsDeleted);
    }
}
