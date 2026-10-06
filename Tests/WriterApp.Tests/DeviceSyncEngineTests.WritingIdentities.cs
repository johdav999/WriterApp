using WriterApp.Device.Shared.Services;
using WriterApp.Device.Shared.Storage;
using WriterApp.Shared;
using Xunit;

namespace WriterApp.Tests;

public sealed partial class DeviceSyncEngineTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AcknowledgedUploadBindsWritingTargetsWithoutChangingLocalIdentities(bool project)
    {
        var source = project ? await Store.CreateProjectAsync("Rewrite project") : await Store.CreateAsync("Rewrite document");
        source = await EditAsync(source.DocumentId, "<p>Original passage.</p>");
        await Engine().EnableAsync(source.DocumentId);
        var saved = (await Store.GetAsync(source.DocumentId))!;
        AssertWritingTargets(source, saved);
        var prepared = LocalWritingTests.Prepare(saved, WritingScope.Selection);
        Assert.Equal(saved.Sections[0].ServerSectionId, prepared.Request.Request.SectionId);
        Assert.Equal(saved.Sections[0].Pages[0].ServerPageId, prepared.Request.Request.PageId);
        Assert.NotNull(prepared.Request.Request.WritingOutline);
    }

    [Fact]
    public async Task SyncRepairsPreviouslyAcknowledgedTargetsWithoutAnotherUpload()
    {
        var source = await Store.CreateProjectAsync("Legacy synchronized document");
        source = await EditAsync(source.DocumentId, "<p>Original passage.</p>");
        var engine = Engine();
        await engine.EnableAsync(source.DocumentId);
        var saved = (await Store.GetAsync(source.DocumentId))!;
        var legacy = await Store.ApplySyncAsync(saved with { Sections = saved.Sections.Select(s => s with {
            ServerSectionId = null, Pages = s.Pages.Select(p => p with { ServerPageId = null }).ToArray()
        }).ToArray() }, saved.LocalRevision, default, projects: true);
        engine.Dispose();
        await Engine().SyncAsync();
        var repaired = (await Store.GetAsync(source.DocumentId))!;
        Assert.Single(_api.Requests);
        AssertWritingTargets(legacy, repaired);
        Assert.Equal(saved.ServerVersion, repaired.ServerVersion);
        LocalWritingTests.Prepare(repaired, WritingScope.Selection);
    }

    [Fact]
    public async Task NewTargetsStayUnboundUntilTheirOwnUploadIsAcknowledged()
    {
        var repository = new LocalDocumentRepository(Store);
        var source = await repository.CreateAsync("Concurrent structure");
        LocalDocument? added = null;
        _api.BeforeResponse = async () => {
            _api.BeforeResponse = null;
            var current = (await Store.GetAsync(source.DocumentId))!;
            added = await repository.ChangeStructureAsync(current, new(LocalStructureAction.CreateSection, Title: "New section"));
            _api.BeforeResponse = async () => {
                _api.BeforeResponse = null;
                var pending = (await Store.GetAsync(source.DocumentId))!;
                Assert.Equal(LocalSyncState.PendingUpload, pending.SyncState);
                Assert.Null(pending.Sections[^1].ServerSectionId);
                Assert.Null(pending.Sections[^1].Pages[0].ServerPageId);
                await Task.CompletedTask;
            };
        };
        await Engine(repo: repository).EnableAsync(source.DocumentId);
        Assert.Equal(2, _api.Requests.Count);
        AssertWritingTargets(added!, (await Store.GetAsync(source.DocumentId))!);
    }

    private void AssertWritingTargets(LocalDocument before, LocalDocument after)
    {
        Assert.Equal(LocalSyncState.Synced, after.SyncState);
        Assert.Equal(before.DocumentId, after.DocumentId);
        Assert.Equal(before.Sections.Select(s => s.SectionId), after.Sections.Select(s => s.SectionId));
        Assert.Equal(before.Sections.SelectMany(s => s.Pages).Select(p => p.PageId), after.Sections.SelectMany(s => s.Pages).Select(p => p.PageId));
        Assert.Equal(LocalTranslation.Signature(before), LocalTranslation.Signature(after));
        var remote = _api.Documents[after.ServerDocumentId!.Value].Document!;
        Assert.Equal(remote.Sections.Select(s => (Guid?)s.Id), after.Sections.Select(s => s.ServerSectionId));
        Assert.Equal(remote.Sections.SelectMany(s => s.Pages).Select(p => (Guid?)p.Id), after.Sections.SelectMany(s => s.Pages).Select(p => p.ServerPageId));
    }
}
