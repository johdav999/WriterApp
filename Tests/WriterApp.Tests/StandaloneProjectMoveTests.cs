using System.Text.Json;
using WriterApp.Device.Shared.Services;
using WriterApp.Device.Shared.Storage;
using Xunit;

namespace WriterApp.Tests;

public sealed class StandaloneProjectMoveTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "WriterApp.ProjectMove", Guid.NewGuid().ToString("N"));
    private FileLocalDocumentStore Store => new(_root);

    [Fact]
    public async Task MovePreservesWritingIdentitiesSharedMetadataAndPrimaryAcrossRestart()
    {
        var store = Store;
        var project = await store.CreateProjectAsync("Book");
        var sibling = await store.CreateProjectDocumentAsync(project.Project!.ProjectId, "Alternative", "manuscript");
        var source = await store.CreateImportedAsync("Draft", "<p>Räksmörgås 日本語</p>");
        var moved = await store.MoveStandaloneToProjectAsync(source, project.DocumentId);
        Assert.Equal(source.DocumentId, moved.DocumentId);
        Assert.Equal(source.CreatedAtUtc, moved.CreatedAtUtc);
        Assert.Equal(source.LocalRevision + 1, moved.LocalRevision);
        Assert.Equal(JsonSerializer.Serialize(source.Sections), JsonSerializer.Serialize(moved.Sections));
        Assert.Equal(project.Project.ProjectId, moved.Project!.ProjectId);
        Assert.Equal(project.DocumentId, moved.Project.PrimaryDocumentId);
        Assert.Equal(moved.DocumentId, moved.Project.ManuscriptId);
        Assert.Equal(source.Sections[0].SectionId, moved.Project.Nodes.Single(n => n.NodeType == "scene").SectionId);
        Assert.DoesNotContain(moved.Project.Nodes, n => project.Project.Nodes.Any(p => p.NodeId == n.NodeId));
        Assert.Equal(LocalSyncState.LocalOnly, moved.SyncState);
        Assert.Null(moved.ServerDocumentId);
        var restarted = Store;
        Assert.Equal(project.Project.ProjectId, (await restarted.GetAsync(source.DocumentId))!.Project!.ProjectId);
        Assert.Equal(JsonSerializer.Serialize(project), JsonSerializer.Serialize(await restarted.GetAsync(project.DocumentId)));
        Assert.Equal(JsonSerializer.Serialize(sibling), JsonSerializer.Serialize(await restarted.GetAsync(sibling.DocumentId)));
        await Assert.ThrowsAsync<LocalDocumentConflictException>(() => restarted.SaveAsync(source));
    }

    [Fact]
    public async Task InvalidStaleTrashedAndAlreadyLinkedMovesLeaveStoredDocumentsIntact()
    {
        var store = Store;
        var target = await store.CreateProjectAsync("Book");
        var source = await store.CreateAsync("Draft");
        var latest = await store.RenameAsync(source.DocumentId, source.LocalRevision, "Latest");
        await Assert.ThrowsAsync<LocalDocumentConflictException>(() => store.MoveStandaloneToProjectAsync(source, target.DocumentId));
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.MoveStandaloneToProjectAsync(latest, latest.DocumentId));
        await Assert.ThrowsAsync<FileNotFoundException>(() => store.MoveStandaloneToProjectAsync(latest, Guid.NewGuid()));
        target = await store.MoveToTrashAsync(target.DocumentId, target.LocalRevision);
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.MoveStandaloneToProjectAsync(latest, target.DocumentId));
        Assert.Null((await store.GetAsync(source.DocumentId))!.Project);
        target = await store.RestoreAsync(target.DocumentId, target.LocalRevision);
        var trashed = await store.MoveToTrashAsync(latest.DocumentId, latest.LocalRevision);
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.MoveStandaloneToProjectAsync(trashed, target.DocumentId));
        latest = await store.RestoreAsync(trashed.DocumentId, trashed.LocalRevision);
        var moved = await store.MoveStandaloneToProjectAsync(latest, target.DocumentId);
        var another = await store.CreateProjectAsync("Another");
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.MoveStandaloneToProjectAsync(moved, another.DocumentId));
        Assert.Equal(target.Project!.ProjectId, (await store.GetAsync(moved.DocumentId))!.Project!.ProjectId);
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
