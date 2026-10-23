using Microsoft.EntityFrameworkCore;
using WriterApp.Application.Collaboration;
using WriterApp.Data.Collaboration;
using WriterApp.Shared.Collaboration;
using Xunit;

namespace WriterApp.Tests;

public sealed partial class DocumentSyncTests
{
    [Theory]
    [InlineData(CollaborationMode.Migrating)]
    [InlineData(CollaborationMode.Collaborative)]
    public async Task CollaborativeManuscriptRejectsNewSnapshotEvenWithCurrentVersion(CollaborationMode mode)
    {
        foreach (string sql in CollaborationWriteFenceSchema.Install(_sqlDatabase is not null)) await _db.Database.ExecuteSqlRawAsync(sql);
        Guid id = Guid.NewGuid(); var upload = Create();
        await _sync.MutateAsync("paid", id, upload);
        var before = await _sync.DownloadAsync("paid", id);
        await _db.Documents.Where(d => d.Id == id).ExecuteUpdateAsync(s => s.SetProperty(d => d.CollaborationMode, mode));
        var version = (await _db.DocumentSyncRecords.AsNoTracking().SingleAsync(r => r.DocumentId == id)).Version;
        var blockedRead = await Assert.ThrowsAsync<WriterApp.Application.Documents.DocumentSyncException>(() => _sync.DownloadAsync("paid", id));
        Assert.Equal("collaboration_snapshot_unavailable", blockedRead.Error.Code);
        var changed = upload.Document! with { Title = "stale client snapshot" };
        var error = await Assert.ThrowsAsync<CollaborationWriteException>(() => _sync.MutateAsync("paid", id,
            new(Guid.NewGuid(), version, "upload", changed)));
        Assert.Equal(mode == CollaborationMode.Migrating ? CollaborationErrors.MigrationInProgress : CollaborationErrors.LegacyWriteBlocked, error.Error.Code);
        Assert.True(error.Error.PreserveLocalDraft);
        Assert.Equal(before.Document!.Title, (await _db.Documents.AsNoTracking().SingleAsync(d => d.Id == id)).Title);
        Assert.Equal(before.Document.Sections.SelectMany(s => s.Pages).Select(p => p.Content),
            await _db.Pages.AsNoTracking().Where(p => p.DocumentId == id).OrderBy(p => p.OrderIndex).Select(p => p.Content).ToArrayAsync());
        Assert.Equal(version, (await _db.DocumentSyncRecords.AsNoTracking().SingleAsync(r => r.DocumentId == id)).Version);
    }

    [Fact]
    public async Task CollaborativeFenceDoesNotBreakLegacySnapshotRetry()
    {
        foreach (string sql in CollaborationWriteFenceSchema.Install(_sqlDatabase is not null)) await _db.Database.ExecuteSqlRawAsync(sql);
        Guid id = Guid.NewGuid(); var operation = Create("<p>Legacy manuscript</p>");
        var first = await _sync.MutateAsync("paid", id, operation);
        Assert.Equal(first, await _sync.MutateAsync("paid", id, operation));
        var snapshot = await _sync.DownloadAsync("paid", id);
        await _sync.MutateAsync("paid", id, new(Guid.NewGuid(), snapshot.State.Version, "upload", operation.Document! with { Title = "Still editable" }));
        Assert.Equal("Still editable", (await _sync.DownloadAsync("paid", id)).Document!.Title);
    }
}
