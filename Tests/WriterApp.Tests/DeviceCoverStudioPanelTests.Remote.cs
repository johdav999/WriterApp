using WriterApp.Device.Shared.Storage;
using Xunit;

namespace WriterApp.Tests;

public sealed partial class DeviceCoverStudioPanelTests
{
    [Fact]
    public async Task ActualPageMalformedReceiptShowsErrorAndRetainsCachedPreviewWithoutChangingSavedCover() {
        await using var h=new Harness();await h.Start(remote:true);await h.Event("Materialize");h.Fixture.Api.MissingAsset=true;
        await h.Event("Materialize");Assert.Contains("Owned cover response does not match",await h.Html());Assert.Contains("role=\"alert\"",await h.Html());
        Assert.Contains("Validated cached project cover",await h.Html());Assert.Equal(RemoteCoverNetwork.Reference,(await h.Fixture.Documents.LoadAsync(h.Fixture.Source.DocumentId))!.Project!.CoverImageUrl);
        await RemoteEvidence(h,"invalid-receipt");
    }
    private static async Task RemoteEvidence(Harness h,string state) {
        if(Environment.GetEnvironmentVariable("WRITERAPP_P16_EVIDENCE") is { } root) {
            var folder=Path.Combine(root,"p16");Directory.CreateDirectory(folder);await File.WriteAllTextAsync(Path.Combine(folder,"remote-cover-"+state+".html"),await h.Html());
        }
    }
    [Fact]
    public async Task ActualPageMaterializesSelectsExplicitlySavesRestoresAndClearsPrivateOfflineAssetsOnSignOut() {
        await using var h=new Harness();await h.Start(remote:true);var f=h.Fixture;
        Assert.Contains("Cache owned remote cover",await h.Html());Assert.DoesNotContain("src=\"https://provider.test",await h.Html());await RemoteEvidence(h,"empty");
        var original=LocalDocumentCodec.Encode(f.Source);await h.Event("Materialize");
        Assert.Equal(original,LocalDocumentCodec.Encode((await f.Documents.LoadAsync(f.Source.DocumentId))!));
        Assert.Contains("Validated cached project cover",await h.Html());Assert.Contains("saved project cover is unchanged",await h.Html());await RemoteEvidence(h,"cached");
        await h.Event("Select",0);await h.Offline();await RemoteEvidence(h,"offline");await h.Event("Save");
        Assert.Equal(CoverTestFixture.Image,(await f.Documents.LoadAsync(f.Source.DocumentId))!.Project!.CoverImageUrl);await RemoteEvidence(h,"saved");
        await h.Event("Restore");Assert.Equal(RemoteCoverNetwork.Reference,(await f.Documents.LoadAsync(f.Source.DocumentId))!.Project!.CoverImageUrl);
        Assert.Contains("Validated cached project cover",await h.Html());
        await h.SignOut();Assert.DoesNotContain("Validated cached project cover",await h.Html());Assert.DoesNotContain("Select cover concept 1",await h.Html());await RemoteEvidence(h,"signed-out");
    }
    [Fact]
    public async Task ActualPageExpiredRemotePreservesExistingConceptsAndShowsActionableFailure() {
        await using var h=new Harness();await h.Start(remote:true);h.Fixture.Remote!.Scenario="expired";
        await h.Event("Materialize");Assert.Contains("expired or was deleted",await h.Html());
        Assert.Equal(RemoteCoverNetwork.Reference,(await h.Fixture.Documents.LoadAsync(h.Fixture.Source.DocumentId))!.Project!.CoverImageUrl);
        Assert.Null(await h.Fixture.Studio.CachedProjectCoverAsync(h.Fixture.Source));await RemoteEvidence(h,"expired");
    }
    [Fact]
    public async Task ActualPageLoadingCancelNeverPromotesFetchedBytes() {
        await using var h=new Harness();await h.Start(remote:true);var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        h.Fixture.Remote!.During=async ()=>{entered.TrySetResult();await release.Task;};
        var fetching=h.Event("Materialize");await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Contains("Working",await h.Html());await RemoteEvidence(h,"loading");await h.Event("Cancel");release.TrySetResult();await fetching;
        Assert.Contains("Canceled",await h.Html());Assert.Null(await h.Fixture.Studio.CachedProjectCoverAsync(h.Fixture.Source));
        Assert.Equal(RemoteCoverNetwork.Reference,(await h.Fixture.Documents.LoadAsync(h.Fixture.Source.DocumentId))!.Project!.CoverImageUrl);
    }
}
