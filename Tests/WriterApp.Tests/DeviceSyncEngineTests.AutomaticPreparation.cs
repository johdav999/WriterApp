using WriterApp.Device.Shared.Services;
using WriterApp.Device.Shared.Storage;
using Xunit;

namespace WriterApp.Tests;

public sealed partial class DeviceSyncEngineTests
{
    private static async Task<DeviceAccountService> PreparationAccount()
    {
        var account = new DeviceAccountService(new Identity());
        await account.SignInAsync();
        return account;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OnlineToolAutomaticallyLinksAndSynchronizesLatestWritingAndPlanning(bool project)
    {
        var doc = project ? await Store.CreateProjectAsync("Book") : await Store.CreateAsync("Story");
        var engine = Engine(account: await PreparationAccount());
        var saved = await engine.SynchronizeForAiAsync(doc.DocumentId);
        Assert.Equal(LocalSyncState.Synced, saved.SyncState);
        Assert.NotNull(saved.ServerDocumentId);
        Assert.NotNull(saved.Sections[0].ServerSectionId);
        Assert.NotNull(saved.Sections[0].Pages[0].ServerPageId);
        await EditAsync(doc.DocumentId, "<p>Latest mobile writing</p>");
        if (project) {
            var latest = (await Store.GetAsync(doc.DocumentId))!;
            await Store.SaveAsync(latest with { Project = latest.Project! with { Synopsis = new(Logline: "Latest mobile planning") } });
        }
        saved = await engine.SynchronizeForAiAsync(doc.DocumentId);
        Assert.Equal("<p>Latest mobile writing</p>", Text(saved));
        Assert.Equal(Text(saved), _api.Documents[saved.ServerDocumentId!.Value].Document!.Sections[0].Pages[0].Content);
        Assert.Equal(LocalSyncState.Synced, saved.SyncState);
        int uploads = _api.Requests.Count;
        await engine.SynchronizeForAiAsync(doc.DocumentId);
        Assert.Equal(uploads, _api.Requests.Count);
        if (project) Assert.Equal("Latest mobile planning", _api.Requests.Last().Document!.Project!.Synopsis!.Logline);
    }

    [Fact]
    public async Task AutomaticPreparationRetainsBothCopiesAndStopsOnConflict()
    {
        var doc = await Store.CreateAsync("Story");
        var engine = Engine(account: await PreparationAccount());
        await engine.SynchronizeForAiAsync(doc.DocumentId);
        await EditAsync(doc.DocumentId, "<p>Local pending writing</p>");
        _api.EditRemote("<p>Remote writing</p>");
        await Assert.ThrowsAsync<DeviceSyncConflictException>(() => engine.SynchronizeForAiAsync(doc.DocumentId));
        Assert.Equal("<p>Local pending writing</p>", Text((await Store.GetAsync(doc.DocumentId))!));
        Assert.Contains((await Store.ListAsync()).Documents, d => Text(d) == "<p>Remote writing</p>");
    }

    [Fact]
    public async Task OfflineAndSignedOutPreparationDoNotEnableOrUploadCloudCopies()
    {
        var doc = await Store.CreateAsync("Local story");
        var account = await PreparationAccount();
        var engine = Engine(account: account);
        _network.SetOnline(false);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => engine.SynchronizeForAiAsync(doc.DocumentId));
        Assert.Contains("Connect", error.Message);
        _network.SetOnline(true);
        await account.SignOutAsync();
        await Assert.ThrowsAsync<DeviceSignInRequiredException>(() => engine.SynchronizeForAiAsync(doc.DocumentId));
        Assert.Empty(_api.Requests);
        Assert.Null((await Store.GetAsync(doc.DocumentId))!.ServerDocumentId);
    }

    [Fact]
    public async Task RejectedUploadAndCloudDeletionStopAutomaticPreparation()
    {
        var doc = await Store.CreateAsync("Story");
        var engine = Engine(account: await PreparationAccount());
        await engine.SynchronizeForAiAsync(doc.DocumentId);
        await EditAsync(doc.DocumentId, "<p>Retained local writing</p>");
        _api.FailStatus = 403;
        await Assert.ThrowsAsync<InvalidOperationException>(() => engine.SynchronizeForAiAsync(doc.DocumentId));
        Assert.Equal("<p>Retained local writing</p>", Text((await Store.GetAsync(doc.DocumentId))!));
        _api.FailStatus = null;
        await engine.RetryAsync(doc.DocumentId);
        _api.DeleteRemote();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => engine.SynchronizeForAiAsync(doc.DocumentId));
        Assert.Contains("deleted in the cloud", error.Message);
        Assert.Equal("<p>Retained local writing</p>", Text((await Store.GetAsync(doc.DocumentId))!));
    }

    [Fact]
    public async Task CancellationAndAccountChangeCannotReturnAnAiSource()
    {
        var doc = await Store.CreateAsync("Story");
        var account = await PreparationAccount();
        var engine = Engine(account: account);
        using var cancellation = new CancellationTokenSource();
        _api.BeforeResponse = () => { cancellation.Cancel(); return Task.CompletedTask; };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => engine.SynchronizeForAiAsync(doc.DocumentId, cancellation.Token));
        _api.BeforeResponse = () => account.SignOutAsync();
        await Assert.ThrowsAsync<DeviceSignInRequiredException>(() => engine.SynchronizeForAiAsync(doc.DocumentId));
        Assert.NotNull(await Store.GetAsync(doc.DocumentId));
    }
}
