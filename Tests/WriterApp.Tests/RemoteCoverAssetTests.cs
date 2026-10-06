using System.IO.Compression;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WriterApp.Application.Covers;
using WriterApp.Device.Shared.Services;
using WriterApp.Device.Shared.Storage;
using WriterApp.Shared;
using Xunit;

namespace WriterApp.Tests;

public sealed class RemoteCoverAssetTests
{
    [Fact]
    public async Task ForeignRegistrationDuringDownloadIsRejectedAgainBeforeReturningOrPersistingOwnedBytes() {
        using var f=new CoverTestFixture();await f.Start();await SetRemote(f);
        f.Remote!.During=async ()=>{await using var db=f.Db();db.CoverAssets.Add(new(){Id=Guid.NewGuid(),OwnerUserId="foreign-owner",ProjectId=Guid.NewGuid(),
            ReferenceHash=CoverAssetContract.ReferenceHash(RemoteCoverNetwork.Reference),RemoteReference=RemoteCoverNetwork.Reference});await db.SaveChangesAsync();};
        await Assert.ThrowsAsync<IOException>(()=>f.Studio.MaterializeProjectCoverAsync(f.Source));Assert.Null(await f.Studio.CachedProjectCoverAsync(f.Source));
        await using var verify=f.Db();Assert.Empty(await verify.CoverAssets.Where(a=>a.OwnerUserId=="user-1").ToListAsync());
        Assert.Equal(RemoteCoverNetwork.Reference,(await f.Documents.LoadAsync(f.Source.DocumentId))!.Project!.CoverImageUrl);
    }
    [Theory][InlineData("wrong-source")][InlineData("wrong-hash")][InlineData("missing-asset")][InlineData("oversize")][InlineData("partial")]
    public async Task UnusableAssetAcknowledgementPreservesExactPreviouslyCachedBytesAndDraft(string failure) {
        using var f=new CoverTestFixture();await f.Start();await SetRemote(f);var draft=await f.Studio.MaterializeProjectCoverAsync(f.Source);
        f.Api.WrongDocument=failure=="wrong-source";f.Api.WrongHash=failure=="wrong-hash";f.Api.MissingAsset=failure=="missing-asset";
        f.Api.Oversize=failure=="oversize";f.Api.Interrupted=failure=="partial";
        if(failure=="partial")await Assert.ThrowsAnyAsync<IOException>(()=>f.Studio.MaterializeProjectCoverAsync(f.Source));
        else await Assert.ThrowsAsync<InvalidDataException>(()=>f.Studio.MaterializeProjectCoverAsync(f.Source));
        Assert.Equal(draft.Id,(await f.Studio.CachedAsync(f.Source))!.Id);Assert.Equal(Convert.FromBase64String(CoverTestFixture.Png),(await f.Studio.CachedProjectCoverAsync(f.Source))!.Png);
        Assert.Equal(RemoteCoverNetwork.Reference,(await f.Documents.LoadAsync(f.Source.DocumentId))!.Project!.CoverImageUrl);Assert.Equal(1,f.Remote!.Calls);
    }
    [Fact]
    public async Task VersionOneDraftAndFutureCorruptAssetCacheArePreservedWithoutSilentReplacement() {
        using var f=new CoverTestFixture();await f.Start();var draft=await f.Studio.GenerateAsync(f.Source,f.Brief());
        await f.Cache.WriteAsync(draft with{Version=1,Assets=null});Assert.Equal(1,(await f.Studio.CachedAsync(f.Source))!.Version);
        await SetRemote(f);await f.Studio.MaterializeProjectCoverAsync(f.Source);
        var value=(await f.Cache.ReadAssetAsync(f.Studio.Scope!,f.Source))!;
        await Assert.ThrowsAsync<InvalidDataException>(()=>f.Cache.WriteAssetAsync(value with{Asset=value.Asset with{AssetId=Guid.NewGuid()}}));
        string path=Path.Combine(f.Root,"covers",value.Scope,"assets",value.ProjectId.ToString("N"),CoverAssetContract.ReferenceHash(RemoteCoverNetwork.Reference)+".json");
        byte[] future=JsonSerializer.SerializeToUtf8Bytes(value with{Version=99});await File.WriteAllBytesAsync(path,future);
        await Assert.ThrowsAsync<InvalidDataException>(()=>f.Studio.CachedProjectCoverAsync(f.Source));Assert.Equal(future,await File.ReadAllBytesAsync(path));
    }
    internal static async Task SetRemote(CoverTestFixture f,string reference=RemoteCoverNetwork.Reference) {
        await using(var db=f.Db()){var project=await db.Projects.SingleAsync();project.CoverImageUrl=reference;await db.SaveChangesAsync();}
        f.Source=await f.Store.ApplySyncAsync(f.Source with{Project=f.Source.Project! with{CoverImageUrl=reference,MetadataRevision=1,
            ServerMetadataRevision=1,MetadataDirty=false}},f.Source.LocalRevision,default,projects:true,overwriteProjectMetadata:true);
        f.Remote=new();
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task OwnedPngCachesWithoutMutationReopensOfflinePublishesExactBytesAndExplicitSaveRetainsRemoteRecovery(bool second) {
        using var f=new CoverTestFixture();await f.Start();await SetRemote(f);f.Remote!.Second=second;
        var original=LocalDocumentCodec.Encode(f.Source);var draft=await f.Studio.MaterializeProjectCoverAsync(f.Source);
        var asset=Assert.IsType<CoverAssetIdentity>(Assert.Single(draft.Assets!));var bytes=Convert.FromBase64String(second?CoverTestFixture.SecondPng:CoverTestFixture.Png);
        Assert.Equal(original,LocalDocumentCodec.Encode((await f.Documents.LoadAsync(f.Source.DocumentId))!));Assert.Equal(CoverAssetContract.Hash(bytes),asset.ContentHash);
        var restarted=new DeviceCoverStudio(f.Http,f.Account,f.Network,f.Host,new(new FileLocalDocumentStore(f.Root+"/docs")),new(f.Root+"/covers"));
        f.Network.SetOnline(false);var cover=(await restarted.CachedProjectCoverAsync(f.Source))!;Assert.Equal(bytes,cover.Png);
        foreach(var format in new[]{DeviceExportFormat.Html,DeviceExportFormat.Docx,DeviceExportFormat.Epub}) {
            var output=await DevicePublishing.PrepareAsync(f.Source,new(format,IncludeCover:true),cover);
            if(format==DeviceExportFormat.Html)Assert.Contains(Convert.ToBase64String(bytes),output.PreviewHtml);
            else if(format==DeviceExportFormat.Docx) { using var package=DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(new MemoryStream(output.File.Content),false);
                var entry=Assert.Single(package.MainDocumentPart!.ImageParts);using var actual=new MemoryStream();await entry.GetStream().CopyToAsync(actual);Assert.Equal(bytes,actual.ToArray()); }
            else { using var zip=new ZipArchive(new MemoryStream(output.File.Content));var entry=Assert.Single(zip.Entries,e=>e.FullName.EndsWith("cover.png"));
                using var actual=new MemoryStream();await entry.Open().CopyToAsync(actual);Assert.Equal(bytes,actual.ToArray()); }
        }
        var sibling=await f.Documents.CreateProjectDocumentAsync(f.Source.Project!.ProjectId,"Notes","notes");
        Assert.Equal(bytes,(await restarted.CachedProjectCoverAsync(sibling))!.Png);
        var saved=await restarted.SaveAsync(f.Source,draft);Assert.Equal(draft.Images[0],saved.Project!.CoverImageUrl);
        Assert.Equal(RemoteCoverNetwork.Reference,saved.Project.PreviousCoverImageUrl);Assert.True(saved.Project.HasCoverRecovery);
        Assert.Equal(saved.Project.MetadataRevision,(await restarted.SaveAsync(f.Source,draft)).Project!.MetadataRevision);
        Assert.Equal(JsonSerializer.Serialize(f.Source.Sections),JsonSerializer.Serialize(saved.Sections));
        var restored=await f.Documents.SetProjectCoverAsync(saved,null,Guid.NewGuid(),restore:true);
        Assert.Equal(RemoteCoverNetwork.Reference,restored.Project!.CoverImageUrl);Assert.Equal(bytes,(await restarted.CachedProjectCoverAsync(restored))!.Png);
        Assert.Equal(1,f.Remote.Calls);
    }
    [Fact]
    public async Task RemoteGeneratedConceptUsesOwnedMaterializationAndNormalSelectionSave() {
        using var f=new CoverTestFixture();await f.Start();f.Remote=new();f.Provider.Values=[RemoteCoverNetwork.Reference,CoverTestFixture.SecondImage];
        var draft=await f.Studio.GenerateAsync(f.Source,f.Brief());Assert.Equal(CoverTestFixture.Image,draft.Images[0]);Assert.NotNull(draft.Assets![0]);Assert.Equal(2,draft.Assets[1]!.Version);
        Assert.Null((await f.Documents.LoadAsync(f.Source.DocumentId))!.Project!.CoverImageUrl);
        draft=await f.Studio.SelectAsync(f.Source,draft,0);var saved=await f.Studio.SaveAsync(f.Source,draft);
        Assert.Equal(CoverTestFixture.Image,saved.Project!.CoverImageUrl);Assert.Equal(1,f.Remote.Calls);
    }
    [Theory][InlineData("expired")][InlineData("deleted")][InlineData("jpeg")][InlineData("signature")][InlineData("encoding")]
    [InlineData("corrupt")][InlineData("length")][InlineData("oversize-header")][InlineData("oversize-stream")][InlineData("partial")]
    public async Task InvalidDownloadsPreservePriorDraftCoverAndProjectMetadata(string scenario) {
        using var f=new CoverTestFixture();await f.Start();var prior=await f.Studio.GenerateAsync(f.Source,f.Brief());await SetRemote(f);f.Remote!.Scenario=scenario;
        var before=LocalDocumentCodec.Encode(f.Source);await Assert.ThrowsAnyAsync<IOException>(()=>f.Studio.MaterializeProjectCoverAsync(f.Source));
        Assert.Equal(before,LocalDocumentCodec.Encode((await f.Documents.LoadAsync(f.Source.DocumentId))!));
        Assert.Equal(prior.Id,(await f.Studio.CachedAsync(f.Source))!.Id);Assert.Null(await f.Studio.CachedProjectCoverAsync(f.Source));
        await using var db=f.Db();Assert.Empty(await db.CoverAssets.ToListAsync());
    }
    [Theory][InlineData("redirect-foreign")][InlineData("redirect-private")][InlineData("redirect-path")][InlineData("redirect-loop")]
    public async Task UntrustedRedirectsNeverReachAnotherDestination(string scenario) {
        var remote=new RemoteCoverNetwork{Scenario=scenario};await Assert.ThrowsAsync<CoverAssetException>(()=>remote.Fetcher().FetchAsync(RemoteCoverNetwork.Reference,default));
        Assert.All(remote.Targets,uri=>Assert.Equal("provider.test",uri.Host));Assert.True(remote.Calls<=3);
    }
    [Fact]
    public async Task TrustedRedirectRetainsOriginalProvenanceAndExactPng() {
        var remote=new RemoteCoverNetwork{Scenario="redirect-valid"};Assert.Equal(Convert.FromBase64String(CoverTestFixture.Png),await remote.Fetcher().FetchAsync(RemoteCoverNetwork.Reference,default));
        Assert.Equal(2,remote.Calls);Assert.EndsWith("/redirected.png",remote.Targets[1].AbsolutePath);
    }
    [Theory][InlineData("https://evil.test/owned-container/a.png")][InlineData("https://provider.test/other/a.png")][InlineData("http://provider.test/owned-container/a.png")]
    [InlineData("https://127.0.0.1/owned-container/a.png")][InlineData("https://user:secret@provider.test/owned-container/a.png")]
    [InlineData("https://provider.test:444/owned-container/a.png")][InlineData("https://provider.test/owned-container/a.png#fragment")]
    [InlineData("https://provider.test/owned-container/%2f..%2fprivate.png")]
    public async Task ForeignPrivateMalformedReferencesCannotCauseNetworkTraffic(string reference) {
        var remote=new RemoteCoverNetwork();await Assert.ThrowsAsync<CoverAssetException>(()=>remote.Fetcher().FetchAsync(reference,default));Assert.Equal(0,remote.Calls);
    }
    [Theory][InlineData("10.1.1.1")][InlineData("127.0.0.1")][InlineData("169.254.169.254")][InlineData("172.16.0.1")][InlineData("192.168.1.1")]
    [InlineData("100.64.0.1")][InlineData("::1")][InlineData("fe80::1")][InlineData("fc00::1")][InlineData("::ffff:127.0.0.1")][InlineData("2001:db8::1")][InlineData("2002:7f00:1::1")][InlineData("2001:2::1")]
    public async Task DnsWithEvenOnePrivateOrReservedAddressFailsBeforeConnecting(string address) {
        var remote=new RemoteCoverNetwork{Addresses=[IPAddress.Parse("93.184.216.34"),IPAddress.Parse(address)]};
        await Assert.ThrowsAsync<CoverAssetException>(()=>remote.Fetcher().FetchAsync(RemoteCoverNetwork.Reference,default));Assert.Equal(0,remote.Calls);
    }
    [Theory][InlineData("local-stale")][InlineData("server-stale")][InlineData("account")][InlineData("cancel")][InlineData("lost-ack")]
    public async Task InterruptedOrChangedSourceCannotPromoteOrReplaceOldCacheAndLostAckReusesImmutableAsset(string scenario) {
        using var f=new CoverTestFixture();await f.Start();var prior=await f.Studio.GenerateAsync(f.Source,f.Brief());await SetRemote(f);using var canceled=new CancellationTokenSource();
        f.Remote!.During=async ()=> {
            if(scenario=="server-stale"){await using var db=f.Db();var project=await db.Projects.SingleAsync();project.Title="Concurrent metadata";await db.SaveChangesAsync();}
            if(scenario=="cancel")canceled.Cancel();
        };
        f.Api.After=async ()=> {
            if(scenario=="local-stale")await f.Documents.RenameAsync(f.Source,"Later local metadata");
            if(scenario=="account")await f.Account.SignOutAsync();
            if(scenario=="lost-ack")throw new HttpRequestException("Lost successful asset acknowledgement.");
        };
        await Assert.ThrowsAnyAsync<Exception>(()=>f.Studio.MaterializeProjectCoverAsync(f.Source,canceled.Token));
        if(scenario=="account")await f.Account.SignInAsync();
        Assert.Equal(prior.Id,(await f.Studio.CachedAsync(f.Source))!.Id);
        Assert.Equal(RemoteCoverNetwork.Reference,(await f.Documents.LoadAsync(f.Source.DocumentId))!.Project!.CoverImageUrl);
        if(scenario=="lost-ack") {
            f.Api.After=null;f.Remote.During=null;var retried=await f.Studio.MaterializeProjectCoverAsync(f.Source);
            await using var db=f.Db();Assert.Equal(Assert.Single(await db.CoverAssets.ToListAsync()).Id,retried.Assets![0]!.AssetId);Assert.Equal(1,f.Remote.Calls);
        }
    }
    [Fact]
    public async Task OfflineAndAccountBackendIsolationNeverUseAnotherScopesRemoteBytesAndHashTamperingIsRefused() {
        using var f=new CoverTestFixture();await f.Start();await SetRemote(f);await f.Studio.MaterializeProjectCoverAsync(f.Source);
        f.Network.SetOnline(false);Assert.NotNull(await f.Studio.CachedProjectCoverAsync(f.Source));
        await Assert.ThrowsAsync<IOException>(()=>f.Studio.MaterializeProjectCoverAsync(f.Source));
        var otherBackend=new DeviceCoverStudio(f.Http,f.Account,f.Network,new("Test",new("https://other.test/")),f.Documents,f.Cache);
        Assert.Null(await otherBackend.CachedProjectCoverAsync(f.Source));f.Auth.Id="account-2";await f.Account.SignInAsync();Assert.Null(await f.Studio.CachedProjectCoverAsync(f.Source));
        await Assert.ThrowsAsync<InvalidDataException>(()=>DevicePublishing.PrepareAsync(f.Source,new(DeviceExportFormat.Html,IncludeCover:true)));
        f.Auth.Id="account-1";await f.Account.SignInAsync();var cover=(await f.Studio.CachedProjectCoverAsync(f.Source))!;
        await Assert.ThrowsAsync<InvalidDataException>(()=>DevicePublishing.PrepareAsync(f.Source,new(DeviceExportFormat.Html,IncludeCover:true),cover with{ContentHash=new string('0',64)}));
    }
}
