using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WriterApp.Application.Security;
using WriterApp.Controllers;
using WriterApp.Data;
using WriterApp.Device.Shared.Services;
using WriterApp.Device.Shared.Storage;
using WriterApp.Shared;
using Xunit;

namespace WriterApp.Tests;

public sealed class ReusablePromptTransferTests
{
    internal static PromptDefinition Custom(string name="Pacing")=>new(name,"Craft","custom",null,"Revise {context} with {{focus}} in ${scope}.",new(){["focus"]="more tension",["tone"]="dramatic"},WritingScope.Section,true);
    [Fact]
    public async Task LegacyFilesRemainByteIdenticalUntilExplicitEditAndOfflineCrudRetainsTypedMetadata() {
        using var f=new PresetTransferFixture();var old=await f.Store.SavePromptAsync("Old","Improve pacing");string path=Path.Combine(f.Root,"ai","prompts",old.Id.ToString("N")+".json");var bytes=await File.ReadAllBytesAsync(path);
        var read=Assert.Single(await new LocalAiStore(f.Root+"/ai").PresetsAsync(null));Assert.Equal(1,read.Version);Assert.Equal(bytes,await File.ReadAllBytesAsync(path));
        var saved=await f.Store.SavePresetAsync(Custom(),old.Id,old.Revision,null);Assert.Equal(2,saved.Version);Assert.Equal(old.CreatedAt,saved.CreatedAt);Assert.Equal(old.Id,saved.Id);
        var restart=Assert.Single(await new LocalAiStore(f.Root+"/ai").PresetsAsync(null));Assert.Equal(ReusablePrompts.Canonical(Custom()),ReusablePrompts.Canonical(LocalAiStore.Definition(restart)));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Store.SavePresetAsync(Custom("Stale"),old.Id,old.Revision,null));
        await f.Store.DeletePresetAsync(saved.Id,saved.Revision,null);Assert.Empty(await f.Store.PresetsAsync(null));Assert.True(File.Exists(path));
    }
    [Fact]
    public async Task PinLimitPersistsAndPinnedImportNeverSilentlyLosesItsPin() {
        using var f=new PresetTransferFixture();await f.SignIn();for(int i=0;i<3;i++)await f.Store.SavePresetAsync(Custom("Preset "+i),null,null,f.Library.Scope);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Store.SavePresetAsync(Custom("Fourth"),null,null,f.Library.Scope));
        var fourth=await f.Store.SavePresetAsync(Custom("Fourth") with{Pinned=false},null,null,f.Library.Scope);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Store.PinPresetAsync(fourth.Id,fourth.Revision,f.Library.Scope));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Store.ImportPresetAsync(Custom("Cloud"),f.Library.Scope!,Guid.NewGuid(),"v1"));Assert.Equal(4,(await f.Store.PresetsAsync(f.Library.Scope)).Count);
    }
    [Fact]
    public async Task LostUploadResponseAndRestartRetryCommitOnceWithExactOriginalIntent() {
        using var f=new PresetTransferFixture();await f.SignIn();var local=await f.Store.SavePresetAsync(Custom(),null,null,f.Library.Scope);
        var pending=await f.Library.QueueCopyAsync(local,false,default);f.Handler.LoseNext=true;
        await Assert.ThrowsAsync<HttpRequestException>(()=>f.Library.SendAsync(pending.Id,default));Assert.Equal("Pending",Assert.Single(await f.Store.TransfersAsync(f.Library.Scope!)).Status);
        var edited=await f.Store.SavePresetAsync(Custom("Later local edit"),local.Id,local.Revision,f.Library.Scope);
        var retained=await new DevicePromptLibrary(f.Http,f.Account,new(f.Root+"/ai")).QueueCopyAsync(edited,false,default);Assert.Equal(pending.Id,retained.Id);
        var completed=await f.Library.SendAsync(pending.Id,default);Assert.Equal("Completed",completed.Status);Assert.Equal("Pacing",completed.Result!.Preset!.Name);
        await using var db=f.Db();Assert.Single(await db.PromptPresets.ToArrayAsync());Assert.Single(await db.PromptPresetTransfers.ToArrayAsync());Assert.Equal(2,f.Handler.Sent.Count);
        Assert.Equal(ReusablePrompts.Canonical(f.Handler.Sent[0]),ReusablePrompts.Canonical(f.Handler.Sent[1]));Assert.Contains(await f.Store.PresetsAsync(null),p=>p.Name=="Later local edit");
    }
    [Fact]
    public async Task ConcurrentQueueCreationAcrossStoreInstancesHasOneStableOperation() {
        using var f=new PresetTransferFixture();await f.SignIn();var local=await f.Store.SavePresetAsync(Custom(),null,null,f.Library.Scope);
        var other=new DevicePromptLibrary(f.Http,f.Account,new(f.Root+"/ai"));var queued=await Task.WhenAll(f.Library.QueueCopyAsync(local,false,default),other.QueueCopyAsync(local,false,default));
        Assert.Equal(queued[0].Id,queued[1].Id);Assert.Single(await f.Store.TransfersAsync(f.Library.Scope!));
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task ConcurrentCloudEditOrDeleteBecomesVisibleConflictAndKeepBothCreatesSeparateVersion(bool deleted) {
        using var f=new PresetTransferFixture();await f.SignIn();var local=await f.Store.SavePresetAsync(Custom() with{Pinned=false},null,null,f.Library.Scope);var first=await f.Library.SendAsync((await f.Library.QueueCopyAsync(local,false,default)).Id,default);
        local=await f.Store.SavePresetAsync(Custom("Local divergence") with{Pinned=false},local.Id,local.Revision,f.Library.Scope);var queued=await f.Library.QueueCopyAsync(local,false,default);
        await f.ChangeCloud(first.Request.PresetId,deleted);var conflict=await f.Library.SendAsync(queued.Id,default);Assert.Equal("Conflict",conflict.Status);Assert.Equal(deleted,conflict.Conflict!.Current is null);
        var fork=await f.Library.KeepBothAsync(queued.Id,default);Assert.NotEqual(first.Request.PresetId,fork.Request.PresetId);Assert.Equal("Local divergence",fork.Request.Preset!.Name);
        var sent=await f.Library.SendAsync(fork.Id,default);Assert.Equal("Completed",sent.Status);await using var db=f.Db();var cloud=await db.PromptPresets.ToArrayAsync();Assert.Equal(deleted?1:2,cloud.Length);
        Assert.Contains(cloud,p=>p.Name=="Local divergence");if(!deleted)Assert.Contains(cloud,p=>p.Name=="Cloud divergence");
        Assert.Contains(await f.Store.PresetsAsync(null),p=>p.Name=="Local divergence");
    }
    [Fact]
    public async Task KeepCloudImportsSeparateRevisionAndNeverOverwritesLocallyEditedImport() {
        using var f=new PresetTransferFixture();await f.SignIn();var local=await f.Store.SavePresetAsync(Custom() with{Pinned=false},null,null,f.Library.Scope);var first=await f.Library.SendAsync((await f.Library.QueueCopyAsync(local,false,default)).Id,default);
        local=await f.Store.SavePresetAsync(Custom("Local divergence") with{Pinned=false},local.Id,local.Revision,f.Library.Scope);var queued=await f.Library.QueueCopyAsync(local,false,default);await f.ChangeCloud(first.Request.PresetId,false);await f.Library.SendAsync(queued.Id,default);
        await f.Library.KeepCloudAsync(queued.Id,true,default);var imported=Assert.Single(await f.Store.PresetsAsync(f.Library.Scope),p=>p.OriginCloudId==first.Request.PresetId);
        Assert.Equal("Cloud divergence",imported.Name);Assert.Equal("Canceled",(await f.Store.TransfersAsync(f.Library.Scope!)).Single(t=>t.Id==queued.Id).Status);
        await f.Store.SavePresetAsync(LocalAiStore.Definition(imported) with{Name="Edited import"},imported.Id,imported.Revision,f.Library.Scope);
        await f.Library.ImportPresetAsync(first.Request.PresetId,default);Assert.Contains(await f.Store.PresetsAsync(f.Library.Scope),p=>p.Name=="Edited import");Assert.Contains(await f.Store.PresetsAsync(null),p=>p.Name=="Local divergence");
    }
    [Fact]
    public async Task DeletedSourceCannotBeUploadedAndCanceledIntentCannotBeReplaced() {
        using var f=new PresetTransferFixture();await f.SignIn();var local=await f.Store.SavePresetAsync(Custom(),null,null,f.Library.Scope);var q=await f.Library.QueueCopyAsync(local,false,default);
        await f.Store.DeletePresetAsync(local.Id,local.Revision,f.Library.Scope);await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Library.SendAsync(q.Id,default));Assert.Empty(f.Handler.Sent);
        await f.Library.CancelTransferAsync(q.Id,default);await Assert.ThrowsAsync<InvalidDataException>(()=>f.Store.SaveTransferAsync(q with{Request=q.Request with{Preset=Custom("Replacement")}}));
    }
    [Theory][InlineData("get")][InlineData("post")]
    public async Task AccountSwitchDuringNetworkCannotStoreOrRevealOtherAccountsResult(string operation) {
        using var f=new PresetTransferFixture();await f.SignIn();string scope=f.Library.Scope!;var local=await f.Store.SavePresetAsync(Custom(),null,null,scope);var queued=await f.Library.QueueCopyAsync(local,false,default);
        f.Handler.After=async()=>{f.Identity.Id="account-2";await f.Account.SignInAsync();};
        await Assert.ThrowsAnyAsync<Exception>(()=>operation=="get"?f.Library.RefreshPresetsAsync(default):f.Library.SendAsync(queued.Id,default));
        Assert.Null(await f.Store.CachedPromptsAsync(f.Library.Scope!));Assert.Empty(await f.Store.TransfersAsync(f.Library.Scope!));Assert.Equal("Pending",Assert.Single(await f.Store.TransfersAsync(scope)).Status);
    }
    [Fact]
    public async Task ImportedMetadataIsLosslessInertWhenUnsupportedAndIsolatedByAccountAndBackend() {
        using var f=new PresetTransferFixture();await f.SignIn();var unsupported=Custom() with{ActionKey=null,Pinned=false,Parameters=new(){["nested"]=new{opaque="retain"}}};
        var q=new PromptTransferRequest(1,Guid.NewGuid(),Guid.NewGuid(),"upsert",null,unsupported);var result=await f.Http.PostAsJsonAsync("api/ai/presets/transfer",q);Assert.Equal(HttpStatusCode.OK,result.StatusCode);
        var imported=await f.Library.ImportPresetAsync(q.PresetId,default);Assert.Equal(ReusablePrompts.Canonical(unsupported),ReusablePrompts.Canonical(LocalAiStore.Definition(imported)));Assert.NotNull(ReusablePrompts.Unavailable(LocalAiStore.Definition(imported)));
        Assert.Empty(await f.Store.PresetsAsync(null));Assert.Empty(await f.Store.PresetsAsync(LocalBibleStore.ScopeKey(new("https://other.invalid/"),f.Account.AccountId!)));
        f.Identity.Id="account-2";await f.Account.SignInAsync();Assert.Empty(await f.Store.PresetsAsync(f.Library.Scope));Assert.Empty(await f.Library.RefreshPresetsAsync(default));
    }
    [Fact]
    public async Task RetriedImportCannotReviveExplicitLocalDeletion() {
        using var f=new PresetTransferFixture();await f.SignIn();string scope=f.Library.Scope!;var p=await f.Store.ImportPresetAsync(Custom(),scope,Guid.NewGuid(),"v1");await f.Store.DeletePresetAsync(p.Id,p.Revision,scope);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Store.ImportPresetAsync(Custom(),scope,p.OriginCloudId!.Value,"v1"));Assert.Empty(await f.Store.PresetsAsync(scope));
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task SilentPrincipalChangeCannotDispatchPrivateAuthoredUpload(bool bindEarlierGeneration) {
        using var f=new PresetTransferFixture();await f.SignIn();string scope=f.Library.Scope!;var local=await f.Store.SavePresetAsync(Custom(),null,null,scope);var q=await f.Library.QueueCopyAsync(local,false,default);
        using var http=new HttpClient(new DeviceAuthenticatedHandler(f.Account,new("https://test.invalid/")){InnerHandler=f.Handler}){BaseAddress=new("https://test.invalid/")};var library=new DevicePromptLibrary(http,f.Account,f.Store);
        f.Identity.Id="account-2";
        await Assert.ThrowsAsync<DeviceSignInRequiredException>(async()=>{if(bindEarlierGeneration)await library.SendAsync(q.Id,default);else await http.PostAsJsonAsync("api/ai/presets/transfer",q.Request);});
        Assert.Equal(0,f.Handler.Calls);await using var db=f.Db();Assert.Empty(await db.PromptPresets.ToArrayAsync());Assert.Equal("Pending",Assert.Single(await f.Store.TransfersAsync(scope)).Status);
    }
    [Fact]
    public async Task OldBackendAndInvalidReceiptsKeepPendingIntentWithoutLegacyCreateFallback() {
        using var f=new PresetTransferFixture();await f.SignIn();var local=await f.Store.SavePresetAsync(Custom(),null,null,f.Library.Scope);var q=await f.Library.QueueCopyAsync(local,false,default);
        f.Handler.Unavailable=true;await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Library.SendAsync(q.Id,default));Assert.Equal("Pending",Assert.Single(await f.Store.TransfersAsync(f.Library.Scope!)).Status);Assert.Single(f.Handler.Sent);
        f.Handler.Unavailable=false;f.Handler.InvalidReceipt=true;await Assert.ThrowsAsync<InvalidDataException>(()=>f.Library.SendAsync(q.Id,default));Assert.Equal("Pending",Assert.Single(await f.Store.TransfersAsync(f.Library.Scope!)).Status);
        f.Handler.InvalidReceipt=false;await f.Library.SendAsync(q.Id,default);await using var db=f.Db();Assert.Single(await db.PromptPresets.ToArrayAsync());
    }
}

internal sealed class PresetTransferFixture : IDisposable
{
    public string Root {get;}=Path.Combine(Path.GetTempPath(),"WriterApp.PresetTests",Guid.NewGuid().ToString("N"));
    public LocalAiStore Store {get;} public FixtureIdentity Identity {get;}=new();public DeviceAccountService Account {get;}public FixtureHandler Handler {get;}public HttpClient Http {get;}public DevicePromptLibrary Library {get;}
    public PresetTransferFixture(){Directory.CreateDirectory(Root);Store=new(Root+"/ai");Account=new(Identity);Handler=new(this);Http=new(Handler){BaseAddress=new("https://test.invalid/")};Library=new(Http,Account,Store);using var db=Db();db.Database.EnsureCreated();}
    public Task SignIn()=>Account.SignInAsync();
    public AppDbContext Db()=>new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite("Data Source="+Path.Combine(Root,"cloud.db")).Options);
    public AiPresetsController Controller(AppDbContext db,string? owner=null)=>new(db,new Owner(owner??Account.AccountId!)){ControllerContext=new(){HttpContext=new DefaultHttpContext()}};
    public async Task ChangeCloud(Guid id,bool delete){await using var db=Db();var p=await db.PromptPresets.SingleAsync(p=>p.Id==id);if(delete)db.Remove(p);else{p.Name="Cloud divergence";p.UpdatedUtc=DateTimeOffset.UtcNow;}await db.SaveChangesAsync();}
    public void Dispose(){Http.Dispose();Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();Directory.Delete(Root,true);}
    private sealed class Owner(string id):IUserIdResolver{public string ResolveUserId(ClaimsPrincipal user)=>id;}
    internal sealed class FixtureIdentity:IDeviceIdentityClient {public string Id="account-1";public bool IsConfigured=>true;public Task<DeviceAccessToken?> AcquireAsync(bool interactive,CancellationToken ct)=>Task.FromResult<DeviceAccessToken?>(new("synthetic",DateTimeOffset.UtcNow.AddHours(1),"Writer",Id));public Task SignOutAsync()=>Task.CompletedTask;}
    internal sealed class FixtureHandler(PresetTransferFixture f):HttpMessageHandler {
        public bool LoseNext,Unavailable,InvalidReceipt;public int Calls;public Func<Task>? After;public List<PromptTransferRequest> Sent=[];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct){Calls++;await using var db=f.Db();var controller=f.Controller(db);IActionResult? result;
            if(request.RequestUri!.AbsolutePath=="/api/ai/presets/transfer"){var q=(await request.Content!.ReadFromJsonAsync<PromptTransferRequest>(cancellationToken:ct))!;Sent.Add(q);if(Unavailable)return new(HttpStatusCode.NotFound);result=(await controller.Transfer(q,ct)).Result;}
            else if(request.RequestUri.AbsolutePath=="/api/ai/presets/transfer-library")result=(await controller.TransferLibrary(ct)).Result;else throw new InvalidOperationException("Unexpected route "+request.RequestUri);
            if(After is not null)await After();if(LoseNext){LoseNext=false;throw new HttpRequestException("Response lost after commit.");}
            if(result is ObjectResult o){object? value=o.Value;if(InvalidReceipt&&value is PromptTransferResponse r)value=r with{Preset=r.Preset! with{Name="Wrong payload"}};return new((HttpStatusCode)(o.StatusCode??200)){Content=JsonContent.Create(value)};}
            return new((HttpStatusCode)(result is StatusCodeResult status?status.StatusCode:500));
        }
    }
}
