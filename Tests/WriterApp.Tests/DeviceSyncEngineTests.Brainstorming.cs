using WriterApp.Device.Shared.Services;
using WriterApp.Device.Shared.Storage;
using Xunit;

namespace WriterApp.Tests;

public sealed partial class DeviceSyncEngineTests
{
    private DeviceBrainstormingContext BrainstormContext(LocalDocumentRepository repository, DeviceAccountService account) =>
        new(repository, null!, account, new(new LocalBrainstormingStore(Path.Combine(_root,"brainstorm")), account,
            new("Test", new Uri("https://test.invalid/")), _network, repository, new HttpClient()), null!);

    [Theory]
    [InlineData("pending")]
    [InlineData("acknowledged")]
    public async Task BrainstormPreparationSavesSyncsAndRefreshesOpenEditorMetadata(string state)
    {
        var repository=new LocalDocumentRepository(Store);var original=await repository.CreateAsync("Retain writing");
        var account=new DeviceAccountService(new UnconfiguredDeviceIdentityClient());var engine=Engine(repo:repository,account:account);
        await engine.EnableAsync(original.DocumentId);
        var session=new LocalEditorSession(repository,(await repository.LoadAsync(original.DocumentId))!);
        var page=session.Document.Sections[0].Pages[0].PageId;
        using var autosave=new LocalAutosaveCoordinator(session,new LocalRecoveryStore(Path.Combine(_root,"recovery")),()=>Task.CompletedTask);
        session.Edit(page,"<p>Åsa's pending manuscript</p>");
        if(state=="acknowledged") { await autosave.SaveAsync();await engine.SyncAsync();Assert.Equal(LocalSyncState.PendingUpload,session.Document.SyncState); }
        var requests=_api.Requests.Count;
        await BrainstormContext(repository,account).SynchronizeForReviewAsync(session,async()=>{await autosave.SaveAsync();return true;},engine);
        Assert.False(session.IsDirty);Assert.Equal(LocalSyncState.Synced,session.Document.SyncState);
        Assert.Equal("<p>Åsa's pending manuscript</p>",Text(session.Document));Assert.Equal(Text(session.Document),Text((await repository.LoadAsync(original.DocumentId))!));
        Assert.Equal(Text(session.Document),_api.Documents.Values.Single().Document!.Sections[0].Pages[0].Content);
        Assert.Equal(requests+(state=="pending"?1:0),_api.Requests.Count);
    }

    [Theory]
    [InlineData("offline")][InlineData("conflict")][InlineData("rejected")][InlineData("save-failure")][InlineData("typing")][InlineData("local-only")]
    public async Task BrainstormPreparationRetainsWritingWhenSaveOrSyncCannotComplete(string failure)
    {
        var repository=new LocalDocumentRepository(Store);var doc=await repository.CreateAsync("Retain writing");
        var account=new DeviceAccountService(new UnconfiguredDeviceIdentityClient());var engine=Engine(repo:repository,account:account);
        if(failure!="local-only")await engine.EnableAsync(doc.DocumentId);
        var session=new LocalEditorSession(repository,(await repository.LoadAsync(doc.DocumentId))!);var page=session.Document.Sections[0].Pages[0].PageId;
        using var autosave=new LocalAutosaveCoordinator(session,new LocalRecoveryStore(Path.Combine(_root,"recovery")),()=>Task.CompletedTask);
        session.Edit(page,"<p>Keep my current writing</p>");var requests=_api.Requests.Count;
        if(failure=="offline")_network.SetOnline(false);
        if(failure=="conflict")_api.EditRemote("<p>Keep cloud writing too</p>");
        if(failure=="rejected")_api.FailStatus=400;
        if(failure=="typing")_api.BeforeResponse=()=>{session.Edit(page,"<p>New typing while syncing</p>");return Task.CompletedTask;};
        var error=await Assert.ThrowsAsync<InvalidDataException>(()=>BrainstormContext(repository,account).SynchronizeForReviewAsync(session,async()=>{if(failure=="save-failure")return false;await autosave.SaveAsync();return true;},engine));
        Assert.NotEmpty(error.Message);
        Assert.Equal(failure=="typing"?"<p>New typing while syncing</p>":"<p>Keep my current writing</p>",Text(session.Snapshot()));
        if(failure is "save-failure" or "typing")Assert.True(session.IsDirty);
        else Assert.Equal("<p>Keep my current writing</p>",Text((await repository.LoadAsync(doc.DocumentId))!));
        if(failure is "offline" or "save-failure" or "local-only")Assert.Equal(requests,_api.Requests.Count);
        if(failure=="local-only")Assert.Null(session.Document.ServerDocumentId);
        if(failure=="conflict")Assert.Contains((await Store.ListAsync()).Documents,d=>Text(d)=="<p>Keep cloud writing too</p>");
    }
}
