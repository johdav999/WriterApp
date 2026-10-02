using Microsoft.EntityFrameworkCore;
using WriterApp.Application.Documents;
using WriterApp.Data.Documents;
using WriterApp.Device.Shared.Services;
using WriterApp.Shared.Sync;
using Xunit;

namespace WriterApp.Tests;

public sealed partial class DocumentSyncTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StandaloneMovePersistsDatabaseLinkAndRoundTripsAcrossDevices(bool alreadySynced)
    {
        foreach (var sql in MultiDocumentSyncSchemaV4.Install(_sqlDatabase is not null)) await _db.Database.ExecuteSqlRawAsync(sql);
        var api = new ProjectApi(_sync);
        using var first = new ProjectDevice(api);
        using var second = new ProjectDevice(api);
        var primary = await first.Store.CreateProjectAsync("Novel");
        await first.Engine.EnableAsync(primary.DocumentId);
        primary = (await first.Store.GetAsync(primary.DocumentId))!;
        var source = await first.Store.CreateImportedAsync("Standalone", "<p>Keep this standalone writing 日本語</p>");
        if (alreadySynced) await first.Engine.EnableAsync(source.DocumentId);
        source = (await first.Store.GetAsync(source.DocumentId))!;
        var originalServerId = source.ServerDocumentId;
        await second.Engine.SyncAsync();
        var moved = await first.Store.MoveStandaloneToProjectAsync(source, primary.DocumentId);
        Assert.Equal(source.Sections[0].Pages[0], moved.Sections[0].Pages[0]);
        Assert.Equal(source.ServerDocumentId, moved.ServerDocumentId);
        Assert.Equal(alreadySynced ? WriterApp.Device.Shared.Storage.LocalSyncState.PendingUpload
            : WriterApp.Device.Shared.Storage.LocalSyncState.LocalOnly, moved.SyncState);
        if (alreadySynced) await first.Engine.SyncAsync();
        else await first.Engine.EnableAsync(moved.DocumentId);
        Assert.Empty(first.Engine.Conflicts);
        Assert.Null(first.Engine.LastError);
        moved = (await first.Store.GetAsync(source.DocumentId))!;
        var cloud = await _db.Documents.AsNoTracking().SingleAsync(d => d.Id == moved.ServerDocumentId);
        Assert.Equal(primary.ServerProjectId, cloud.ProjectId);
        if (alreadySynced) Assert.Equal(originalServerId, cloud.Id);
        Assert.Equal(2, await _db.Documents.CountAsync());
        Assert.Equal(primary.ServerDocumentId, (await _db.Projects.AsNoTracking().SingleAsync(p => p.Id == cloud.ProjectId)).PrimaryDocumentId);
        Assert.All(await _db.ProjectNodes.AsNoTracking().Where(n => n.DocumentId == cloud.Id).ToArrayAsync(), n => Assert.Equal(cloud.ProjectId, n.ProjectId));
        await second.Engine.SyncAsync();
        second.Restart();
        Assert.Null(second.Engine.LastError);
        Assert.Empty(second.Engine.Conflicts);
        var members = (await second.Store.ListAsync()).Documents;
        Assert.Equal(2, members.Count);
        Assert.Single(members.Select(d => d.Project!.ProjectId).Distinct());
        Assert.Equal("<p>Keep this standalone writing 日本語</p>", members.Single(d => d.Title == "Standalone").Sections[0].Pages[0].Content);
    }

    [Fact]
    public async Task StandaloneRelinkingRejectsForeignProjectsAndExistingMembersWithoutPartialWrites()
    {
        var sourceId = Guid.NewGuid();
        var source = Create();
        await _sync.MutateAsync("paid", sourceId, source, projects: true, planning: true, multipleDocuments: true);
        var before = await _sync.DownloadAsync("paid", sourceId, projects: true, planning: true, multipleDocuments: true);
        var targetId = Guid.NewGuid();
        await _sync.MutateAsync("other", targetId, ProjectRequest(), projects: true, planning: true, multipleDocuments: true);
        var foreign = await _sync.DownloadAsync("other", targetId, projects: true, planning: true, multipleDocuments: true);
        var chapter = new SyncProjectNode(Guid.NewGuid(), null, "chapter", "Chapter", 0, null);
        var scene = new SyncProjectNode(Guid.NewGuid(), chapter.Id, "scene", "Scene", 0, source.Document!.Sections[0].Id);
        var move = source with { OperationId = Guid.NewGuid(), ExpectedVersion = before.State.Version,
            Document = source.Document with { Title = "Must not change", Project = foreign.Document!.Project! with { Nodes = [chapter, scene] } } };
        Assert.Equal(404, (await Assert.ThrowsAsync<DocumentSyncException>(() => _sync.MutateAsync("paid", sourceId, move,
            projects: true, planning: true, multipleDocuments: true))).Status);
        var unchanged = await _sync.DownloadAsync("paid", sourceId, projects: true, planning: true, multipleDocuments: true);
        Assert.Equal(before.State.Version, unchanged.State.Version);
        Assert.Equal(before.Document!.ProjectId, unchanged.Document!.ProjectId);
        Assert.Equal("Book", unchanged.Document.Title);
        Assert.Empty(await _db.ProjectNodes.Where(n => n.DocumentId == sourceId).ToArrayAsync());

        var memberId = Guid.NewGuid();
        await _sync.MutateAsync("paid", memberId, ProjectRequest(), projects: true, planning: true, multipleDocuments: true);
        var member = await _sync.DownloadAsync("paid", memberId, projects: true, planning: true, multipleDocuments: true);
        move = new(Guid.NewGuid(), member.State.Version, "upload", source.Document with
            { Project = member.Document!.Project! with { Id = Guid.NewGuid(), Nodes = [chapter, scene] } });
        Assert.Equal(400, (await Assert.ThrowsAsync<DocumentSyncException>(() => _sync.MutateAsync("paid", memberId, move,
            projects: true, planning: true, multipleDocuments: true))).Status);
        Assert.Equal(member.Document.ProjectId, (await _sync.DownloadAsync("paid", memberId,
            projects: true, planning: true, multipleDocuments: true)).Document!.ProjectId);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SharedMetadataConflictCanRetainEitherChosenCopy(bool keepLocal)
    {
        var api = new ProjectApi(_sync);
        using var first = new ProjectDevice(api);
        using var second = new ProjectDevice(api);
        var manuscript = await first.Store.CreateProjectAsync("Novel");
        await first.Engine.EnableAsync(manuscript.DocumentId);
        await second.Engine.SyncAsync();
        var local = Assert.Single((await second.Store.ListAsync()).Documents);
        await second.Store.UpdateProjectMetadataAsync(local, title: "Local title");
        var project = await _db.Projects.SingleAsync();
        project.Title = "Cloud title";
        await _db.SaveChangesAsync();
        await second.Engine.SyncAsync();
        Assert.Single(second.Engine.Conflicts);
        await second.Engine.ResolveAsync(local.DocumentId, keepLocal);
        await second.Engine.SyncAsync();
        Assert.Empty(second.Engine.Conflicts);
        Assert.Equal(keepLocal ? "Local title" : "Cloud title", (await _db.Projects.SingleAsync()).Title);
        Assert.Equal(keepLocal ? "Local title" : "Cloud title", (await second.Store.GetAsync(local.DocumentId))!.Project!.Title);
        Assert.True((await second.Store.ListAsync()).Documents.Count >= 3); // Both independent backup copies survive the choice.
    }

    [Fact]
    public async Task MultiDocumentSyncScopesTreesAndBlocksOlderClientsAndForeignNodes()
    {
        foreach (var sql in MultiDocumentSyncSchemaV4.Install(_sqlDatabase is not null)) await _db.Database.ExecuteSqlRawAsync(sql);
        var firstId = Guid.NewGuid();
        var first = ProjectRequest();
        first = first with { Document = first.Document! with { Project = first.Document!.Project! with { Version = 3, PrimaryDocumentId = firstId } } };
        await _sync.MutateAsync("paid", firstId, first, projects: true, planning: true, multipleDocuments: true);
        var before = await _sync.DownloadAsync("paid", firstId, projects: true, planning: true, multipleDocuments: true);
        var secondId = Guid.NewGuid();
        var second = ProjectRequest();
        second = second with { Document = second.Document! with { Title = "Alternative", Project = second.Document!.Project! with
            { Id = before.Document!.Project!.Id, Version = 3, PrimaryDocumentId = firstId, MetadataRevision = before.Document.Project.MetadataRevision } } };
        await _sync.MutateAsync("paid", secondId, second, projects: true, planning: true, multipleDocuments: true);
        var alternative = await _sync.DownloadAsync("paid", secondId, projects: true, planning: true, multipleDocuments: true);
        Assert.Equal(before.State.Version, (await _sync.DownloadAsync("paid", firstId, projects: true, planning: true, multipleDocuments: true)).State.Version);
        Assert.Equal(2, alternative.Document!.Project!.Nodes.Count);
        Assert.All(await _db.ProjectNodes.Where(n => n.DocumentId == secondId).ToListAsync(), n => Assert.Equal(secondId, n.DocumentId));
        Assert.Equal(426, (await Assert.ThrowsAsync<DocumentSyncException>(() => _sync.DownloadAsync("paid", firstId, projects: true, planning: true))).Status);

        var node = await _db.ProjectNodes.SingleAsync(n => n.DocumentId == secondId && n.NodeType == ProjectNodeType.Scene);
        node.Title = "Changed alternative scene";
        await _db.SaveChangesAsync();
        Assert.Equal(before.State.Version, (await _sync.DownloadAsync("paid", firstId, projects: true, planning: true, multipleDocuments: true)).State.Version);
        alternative = await _sync.DownloadAsync("paid", secondId, projects: true, planning: true, multipleDocuments: true);
        var foreignChapter = before.Document.Project.Nodes.Single(n => n.NodeType == "chapter") with { OrderIndex = 1 };
        var forged = second with { OperationId = Guid.NewGuid(), ExpectedVersion = alternative.State.Version,
            Document = second.Document with { Project = second.Document.Project! with
                { Nodes = second.Document.Project!.Nodes.Append(foreignChapter).ToArray() } } };
        Assert.Equal(400, (await Assert.ThrowsAsync<DocumentSyncException>(() => _sync.MutateAsync("paid", secondId, forged,
            projects: true, planning: true, multipleDocuments: true))).Status);
        Assert.Equal("Changed alternative scene", (await _db.ProjectNodes.AsNoTracking().SingleAsync(n => n.Id == node.Id)).Title);

        var project = await _db.Projects.SingleAsync();
        project.Title = "Web project name";
        await _db.SaveChangesAsync();
        alternative = await _sync.DownloadAsync("paid", secondId, projects: true, planning: true, multipleDocuments: true);
        var staleMetadata = second with { OperationId = Guid.NewGuid(), ExpectedVersion = alternative.State.Version };
        Assert.Equal("project_metadata_conflict", (await Assert.ThrowsAsync<DocumentSyncException>(() => _sync.MutateAsync("paid", secondId, staleMetadata,
            projects: true, planning: true, multipleDocuments: true))).Error.Code);
        alternative = await _sync.DownloadAsync("paid", secondId, projects: true, planning: true, multipleDocuments: true);
        var trashed = await _sync.MutateAsync("paid", secondId, new(Guid.NewGuid(), alternative.State.Version, "trash"), projects: true, planning: true, multipleDocuments: true);
        await _sync.MutateAsync("paid", secondId, new(Guid.NewGuid(), trashed.State.Version, "delete"), projects: true, planning: true, multipleDocuments: true);
        Assert.Equal(2, await _db.ProjectNodes.IgnoreQueryFilters().CountAsync());
        Assert.All(await _db.ProjectNodes.IgnoreQueryFilters().ToListAsync(), n => Assert.Equal(firstId, n.DocumentId));
        Assert.Equal(firstId, (await _db.Projects.SingleAsync()).PrimaryDocumentId);
    }

    [Fact]
    public async Task MultipleProjectDocumentsRoundTripAcrossTwoDesktopStoresAndRestart()
    {
        var api = new ProjectApi(_sync);
        using var first = new ProjectDevice(api);
        using var second = new ProjectDevice(api);
        var manuscript = await first.Store.CreateProjectAsync("Novel");
        await first.Engine.EnableAsync(manuscript.DocumentId);
        manuscript = (await first.Store.GetAsync(manuscript.DocumentId))!;
        var alternative = await first.Store.CreateProjectDocumentAsync(manuscript.Project!.ProjectId, "Alternative", "manuscript");
        var research = await first.Store.CreateProjectDocumentAsync(manuscript.Project.ProjectId, "Research", "notes");
        await first.Engine.EnableAsync(alternative.DocumentId);
        await first.Engine.EnableAsync(research.DocumentId);
        await second.Engine.SyncAsync();
        second.Restart();
        var members = (await second.Store.ListAsync()).Documents;
        Assert.Equal(3, members.Count);
        Assert.Single(members.Select(d => d.Project!.ProjectId).Distinct());
        Assert.Equal(2, members.Count(d => d.Kind == "manuscript"));
        Assert.Empty(members.Single(d => d.Kind == "notes").Project!.Nodes);
        Assert.All(members, d => Assert.Equal(d.DocumentId, d.Project!.ManuscriptId));
        Assert.Equal(members.Single(d => d.Title == "Novel").DocumentId, members[0].Project!.PrimaryDocumentId);
        var cloud = await _db.Documents.AsNoTracking().ToListAsync();
        Assert.Equal(3, cloud.Count);
        Assert.Single(cloud.Select(d => d.ProjectId).Distinct());
        alternative = (await first.Store.GetAsync(alternative.DocumentId))!;
        await first.Store.UpdateProjectMetadataAsync(alternative, title: "Renamed novel", makePrimary: true);
        await first.Engine.SyncAsync();
        Assert.Empty(first.Engine.Conflicts);
        Assert.All((await first.Store.ListAsync()).Documents, d => Assert.Equal("Renamed novel", d.Project!.Title));
        await second.Engine.SyncAsync();
        var refreshed = (await second.Store.ListAsync()).Documents;
        Assert.Empty(second.Engine.Conflicts);
        Assert.All(refreshed, d => Assert.Equal("Renamed novel", d.Project!.Title));
        Assert.Equal(refreshed.Single(d => d.Title == "Alternative").DocumentId, refreshed[0].Project!.PrimaryDocumentId);
    }
}
