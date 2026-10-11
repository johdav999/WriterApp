using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WriterApp.Application.AI;
using WriterApp.Application.Security;
using WriterApp.Controllers;
using WriterApp.Device.Shared.Services;
using WriterApp.Device.Shared.Storage;
using WriterApp.Shared.Brainstorming;
using Xunit;

namespace WriterApp.Tests;

public sealed class BrainstormingSyncTests
{
    private static string Root() => Path.Combine(Path.GetTempPath(), "WriterApp.Brainstorming.Sync", Guid.NewGuid().ToString("N"));
    private sealed class Remote(BrainstormingPersistenceTests.Database f) : IBrainstormingRemote
    {
        public bool LoseAcknowledgement; public int Writes; public Func<Task>? AfterWrite;
        public async Task<BrainstormingReceipt> WriteAsync(BrainstormingWrite value, CancellationToken ct = default) {
            Writes++; await using var db = new WriterApp.Data.AppDbContext(f.Options);
            var receipt = await new BrainstormingSessions(db,"author",f.Backend).WriteAsync(value,ct);
            if (AfterWrite is not null) await AfterWrite();
            if (LoseAcknowledgement) { LoseAcknowledgement=false; throw new HttpRequestException("Synthetic lost acknowledgement after durable commit"); }
            return receipt;
        }
        public async Task<BrainstormingSession?> ReadAsync(BrainstormingScope scope, CancellationToken ct = default) {
            await using var db = new WriterApp.Data.AppDbContext(f.Options); return await new BrainstormingSessions(db,"author",f.Backend).ReadAsync(scope.DocumentId,scope.SessionId,ct);
        }
        public async Task<BrainstormingReceipt?> InspectAsync(BrainstormingScope scope, Guid operation, CancellationToken ct = default) {
            await using var db = new WriterApp.Data.AppDbContext(f.Options); return await new BrainstormingSessions(db,"author",f.Backend).InspectAsync(scope.DocumentId,scope.SessionId,operation,ct);
        }
        public async Task<BrainstormingSessionPage> ListAsync(Guid doc, Guid? after=null,int limit=25,CancellationToken ct=default) {
            await using var db = new WriterApp.Data.AppDbContext(f.Options); return await new BrainstormingSessions(db,"author",f.Backend).ListAsync(doc,after,limit,ct);
        }
    }
    private static BrainstormingIdentityMap Map(BrainstormingScope local, BrainstormingScope server) => new(new Dictionary<Guid,Guid>{{local.DocumentId,server.DocumentId}},new Dictionary<Guid,Guid>(),new Dictionary<Guid,Guid>(),new Dictionary<Guid,Guid>());
    private static BrainstormingScope Local(BrainstormingPersistenceTests.Database f) => f.Scope() with {Space=BrainstormingIdentitySpace.Local,DocumentId=Guid.NewGuid()};

    [Fact]
    public async Task UnchangedSyncAndAcknowledgedDeletionDoNotCreateNewWritesButSourceChangesAreDirty()
    {
        await using var f=new BrainstormingPersistenceTests.Database();await f.Start();var local=Local(f);var server=f.Scope();var remote=new Remote(f);
        var service=new BrainstormingSessionService(new LocalBrainstormingStore(Root()),remote);await service.CreateAsync(local);await service.MapAsync(local,server);
        var value=await service.SyncAsync(local,Map(local,server),()=>{});await service.SaveComposerAsync(local,"Local-only composer");await service.SyncAsync(local,Map(local,server),()=>{});Assert.Equal(1,remote.Writes);
        value=(await service.ReadAsync(local))!;value=await service.SaveAsync(value with{Session=value.Session with{Revision=2,UpdatedAt=DateTimeOffset.UtcNow,EnabledSources=[new(local.DocumentId,BrainstormingSourceKind.StoryNote,"note")]}},value.CacheRevision,Guid.NewGuid());
        Assert.Equal(BrainstormingConversationState.PendingSave,value.Session.State);await service.SyncAsync(local,Map(local,server),()=>{});Assert.Equal(2,remote.Writes);
        await service.DeleteAsync(local);await service.SyncAsync(local,Map(local,server),()=>{});await service.SyncAsync(local,Map(local,server),()=>{});Assert.Equal(3,remote.Writes);
        Assert.Equal(BrainstormingConversationState.Deleted,(await service.ReadAsync(local))!.Session.State);
    }
    [Fact]
    public void CanonSourceKeysRemainOpaqueEvenWhenTheyLookLikeMappedGuids()
    {
        var local=BrainstormingContractTests.Fixture().Scope;var server=local with{Space=BrainstormingIdentitySpace.Server,DocumentId=Guid.NewGuid()};var key=Guid.NewGuid().ToString();
        var map=new BrainstormingIdentityMap(new Dictionary<Guid,Guid>{{local.DocumentId,server.DocumentId}},new Dictionary<Guid,Guid>(),new Dictionary<Guid,Guid>(),new Dictionary<Guid,Guid>{{Guid.Parse(key),Guid.NewGuid()}});
        var converted=map.Convert(BrainstormingPersistence.Empty(local) with{EnabledSources=[new(local.DocumentId,BrainstormingSourceKind.Place,key),new(local.DocumentId,BrainstormingSourceKind.Timeline,key)]},server);
        Assert.All(converted.EnabledSources!,s=>Assert.Equal(key,s.EntityKey));
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task RichMappedStandaloneAndProjectSessionsRoundTripWithoutChangingWriting(bool structured)
    {
        await using var f=new BrainstormingPersistenceTests.Database();await f.Start(migrations:true);var remote=new Remote(f);var original=BrainstormingContractTests.Fixture();
        var local=original.Scope with{BackendKey=f.Backend,AccountKey=BrainstormingValidation.Fingerprint("author"),ProjectId=structured?Guid.NewGuid():null};
        var server=f.Scope() with{ProjectId=structured?f.Project:null};var localSupport=Guid.NewGuid();var serverSupport=Guid.NewGuid();
        var localChapter=Guid.NewGuid();var localScene=Guid.NewGuid();var serverChapter=Guid.NewGuid();var serverScene=Guid.NewGuid();var foreignNode=Guid.NewGuid();
        string clock;await using(var db=new WriterApp.Data.AppDbContext(f.Options)){
            db.Documents.Add(new(){Id=serverSupport,ProjectId=f.Project,OwnerUserId="author",DocumentKind=WriterApp.Data.Documents.DocumentKind.Notes,Title="Explicit supporting notes"});
            if(structured){db.ProjectNodes.Add(new(){Id=serverChapter,ProjectId=f.Project,DocumentId=f.Document,NodeType=WriterApp.Data.Documents.ProjectNodeType.Chapter,Title="Chapter 1"});
                db.ProjectNodes.Add(new(){Id=serverScene,ProjectId=f.Project,DocumentId=f.Document,ParentId=serverChapter,LinkedSectionId=f.Section,Title="Scene"});
                db.ProjectNodes.Add(new(){Id=foreignNode,ProjectId=f.Project,DocumentId=serverSupport,Title="Other manuscript scene"});}
            await db.SaveChangesAsync();clock=(await db.DocumentSyncRecords.SingleAsync(d=>d.DocumentId==f.Document)).Version;
        }
        var session=BrainstormingPersistence.ReScope(original,local);var manual=session.Drafts[0] with{Id=Guid.NewGuid(),ParentDraftId=session.Drafts[0].Id,Revision=2,ManuallyEdited=true,PlainText="Manually edited Åsa 日本語",EditorHtml="<p><em>Manually edited</em> Åsa 日本語</p>"};
        session=session with{State=BrainstormingConversationState.LocalOnly,Drafts=[..session.Drafts,manual],EnabledSources=[new(localSupport,BrainstormingSourceKind.SupportingDocument,null)],
            Targets=session.Targets.Select(t=>t with{ChapterNodeId=structured?localChapter:null,SceneNodeId=structured?localScene:null}).ToArray(),
            Contexts=session.Contexts.Select(c=>c with{Sources=[..c.Sources,new(Guid.NewGuid(),localSupport,BrainstormingSourceKind.SupportingDocument,"Selected support",c.Sources[0].Source,BrainstormingFreshness.Cached,true,true,false,false,"Explicitly selected","Supporting excerpt")] }).ToArray(),
            Planning=[new(1,Guid.NewGuid(),local,session.Turns[1].Id,session.Contexts[0].Id,BrainstormingPlanningKind.StoryNote,"notes",session.Targets[0].Source,"Notes","","An inert decision",1,session.CreatedAt)]};
        var map=new BrainstormingIdentityMap(new Dictionary<Guid,Guid>{{local.DocumentId,f.Document},{localSupport,serverSupport}},new Dictionary<Guid,Guid>{{session.Targets[0].SectionId!.Value,f.Section}},new Dictionary<Guid,Guid>{{session.Targets[0].PageId!.Value,f.Page}},structured?new Dictionary<Guid,Guid>{{localChapter,serverChapter},{localScene,serverScene}}:new Dictionary<Guid,Guid>());
        var files=new LocalBrainstormingStore(Root());await files.WriteAsync(new(1,1,session,"Unsent composer",null,null,null,[],[]),0,Guid.NewGuid());var service=new BrainstormingSessionService(files,remote);
        await service.MapAsync(local,server);await service.SyncAsync(local,map,()=>{});var cloud=(await remote.ReadAsync(server))!;
        Assert.Equal(f.Page,cloud.Targets[0].PageId);Assert.Equal(serverSupport,cloud.EnabledSources![0].DocumentId);Assert.Equal(manual.Id,cloud.Drafts[1].Id);
        var next=manual with{Scope=server,Id=Guid.NewGuid(),ParentDraftId=manual.Id,Revision=3,PlainText="Another manual version"};
        cloud=cloud with{Revision=2,Drafts=[..cloud.Drafts,next],UpdatedAt=DateTimeOffset.UtcNow};await remote.WriteAsync(new(1,Guid.NewGuid(),1,cloud));
        var pulled=await service.PullAsync(local,map,()=>{});Assert.Equal(local.DocumentId,pulled.Session.Targets[0].DocumentId);Assert.Equal(session.Targets[0].PageId,pulled.Session.Targets[0].PageId);
        Assert.Equal(localSupport,pulled.Session.EnabledSources![0].DocumentId);Assert.Equal(manual.Id,pulled.Session.Drafts[2].ParentDraftId);Assert.Equal("Unsent composer",pulled.Composer);
        Assert.Equal(session.Planning[0].Id,pulled.Session.Planning[0].Id);Assert.Equal("notes",pulled.Session.Planning[0].EntityKey);
        if(structured){var invalid=BrainstormingPersistence.ReScope(cloud,server with{SessionId=Guid.NewGuid()}) with{Revision=1,Targets=[cloud.Targets[0] with{SceneNodeId=foreignNode}]};
            await Assert.ThrowsAsync<BrainstormingAccessException>(()=>remote.WriteAsync(new(1,Guid.NewGuid(),0,invalid)));}
        await using var finalDb=new WriterApp.Data.AppDbContext(f.Options);Assert.Equal(clock,(await finalDb.DocumentSyncRecords.SingleAsync(d=>d.DocumentId==f.Document)).Version);
        Assert.Equal("<p><strong>Åsa 日本語 🧭</strong></p>",(await finalDb.Pages.SingleAsync()).Content);Assert.Empty(await finalDb.AiActionHistoryEntries.ToArrayAsync());
    }

    [Fact]
    public async Task LostAcknowledgementRestartInspectAndRetryKeepOneOrderedTurn()
    {
        await using var f = new BrainstormingPersistenceTests.Database(); await f.Start(); var root=Root();
        var local=Local(f); var server=f.Scope(); var remote=new Remote(f){LoseAcknowledgement=true};
        var service=new BrainstormingSessionService(new LocalBrainstormingStore(root),remote);
        await service.CreateAsync(local); await service.SaveComposerAsync(local,"Unsent before network");
        var op=Guid.NewGuid(); await service.AppendUnsentAsync(local,"Öppningen 👩🏽‍💻",op); await service.MapAsync(local,server);
        await Assert.ThrowsAsync<HttpRequestException>(()=>service.SyncAsync(local,Map(local,server),()=>{}));
        var pending=(await service.ReadAsync(local))!.Pending!;
        var restarted=new BrainstormingSessionService(new LocalBrainstormingStore(root),remote);
        await restarted.SaveComposerAsync(local,"Next message survives restart");
        var ack=await restarted.SyncAsync(local,Map(local,server),()=>{});
        Assert.Null(ack.Pending); Assert.Equal(pending.OperationId,ack.Acknowledgement!.OperationId); Assert.Equal(1,remote.Writes);
        Assert.Equal("Next message survives restart",ack.Composer); Assert.Equal(BrainstormingTurnState.Accepted,Assert.Single(ack.Session.Turns).State);
        await restarted.AppendUnsentAsync(local,"Öppningen 👩🏽‍💻",op);
        Assert.Single((await restarted.ReadAsync(local))!.Session.Turns); Assert.Single((await remote.ReadAsync(server))!.Turns);
        await using var db=new WriterApp.Data.AppDbContext(f.Options); Assert.Empty(await db.UsageEvents.ToArrayAsync());
    }
    [Fact]
    public async Task InflightNewMessageAndComposerAreRetainedThenExplicitlyDelivered()
    {
        await using var f=new BrainstormingPersistenceTests.Database(); await f.Start(); var local=Local(f); var server=f.Scope(); var remote=new Remote(f);
        var service=new BrainstormingSessionService(new LocalBrainstormingStore(Root()),remote);
        await service.CreateAsync(local); await service.AppendUnsentAsync(local,"First",Guid.NewGuid()); await service.MapAsync(local,server);
        remote.AfterWrite=async()=>{await service.AppendUnsentAsync(local,"Second",Guid.NewGuid());await service.SaveComposerAsync(local,"Third composer");};
        var saved=await service.SyncAsync(local,Map(local,server),()=>{}); Assert.Equal(BrainstormingConversationState.PendingSave,saved.Session.State);
        Assert.Equal(BrainstormingTurnState.Unsent,saved.Session.Turns[1].State); Assert.Single((await remote.ReadAsync(server))!.Turns);
        remote.AfterWrite=null; saved=await service.SyncAsync(local,Map(local,server),()=>{});
        Assert.Equal(new long[]{1,2},saved.Session.Turns.Select(t=>t.Sequence)); Assert.Equal("Third composer",saved.Composer);
        Assert.All(saved.Session.Turns,t=>Assert.Equal(BrainstormingTurnState.Accepted,t.State));
        Assert.Equal(2,(await remote.ReadAsync(server))!.Turns.Count);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task RemoteConflictKeepsBothBranchesAndForkHasNewMappingAndTombstone(bool deleteWhileConflicting)
    {
        await using var f=new BrainstormingPersistenceTests.Database();await f.Start();var remote=new Remote(f);var root=Root();var local=Local(f);var server=f.Scope();
        var service=new BrainstormingSessionService(new LocalBrainstormingStore(root),remote);
        await service.CreateAsync(local);await service.AppendUnsentAsync(local,"Shared",Guid.NewGuid());await service.MapAsync(local,server);await service.SyncAsync(local,Map(local,server),()=>{});
        var other=(await remote.ReadAsync(server))!; var t=new BrainstormingTurn(1,Guid.NewGuid(),server,2,other.Turns[0].Id,Guid.NewGuid(),null,null,BrainstormingRole.User,BrainstormingTurnState.Accepted,"Remote branch",DateTimeOffset.UtcNow);
        await remote.WriteAsync(new(1,Guid.NewGuid(),1,other with{Revision=2,Turns=[..other.Turns,t],UpdatedAt=DateTimeOffset.UtcNow}));
        await service.AppendUnsentAsync(local,"Local branch",Guid.NewGuid());
        if(deleteWhileConflicting)await service.DeleteAsync(local);
        var expectedState=deleteWhileConflicting?BrainstormingConversationState.Deleted:BrainstormingConversationState.Conflict;
        var conflict=await service.SyncAsync(local,Map(local,server),()=>{});Assert.Equal(expectedState,conflict.Session.State);
        var pair=Assert.Single(conflict.Conflicts);Assert.Equal("Local branch",pair.Local.Turns[1].Text);Assert.Equal("Remote branch",pair.Server.Turns[1].Text);
        await Assert.ThrowsAsync<InvalidDataException>(()=>service.SyncAsync(local,Map(local,server),()=>{}));
        var fork=await service.ForkAsync(local,Guid.NewGuid());Assert.Equal(local.SessionId,fork.Session.BranchedFromSessionId);Assert.Null(fork.Mapping);
        var forkServer=server with{SessionId=Guid.NewGuid()};await service.MapAsync(fork.Session.Scope,forkServer);
        var saved=await service.SyncAsync(fork.Session.Scope,Map(fork.Session.Scope,forkServer),()=>{});Assert.Equal(server.SessionId,(await remote.ReadAsync(forkServer))!.BranchedFromSessionId);
        await service.DeleteAsync(fork.Session.Scope);await service.SyncAsync(fork.Session.Scope,Map(fork.Session.Scope,forkServer),()=>{});
        Assert.Equal(BrainstormingConversationState.Deleted,(await remote.ReadAsync(forkServer))!.State);
        Assert.Equal(expectedState,(await new LocalBrainstormingStore(root).ReadAsync(local))!.Session.State);
        var tombstone=(await service.ReadAsync(fork.Session.Scope))!;
        await Assert.ThrowsAsync<InvalidDataException>(()=>service.SaveAsync(tombstone with{Session=tombstone.Session with{State=BrainstormingConversationState.Saved}},tombstone.CacheRevision,Guid.NewGuid()));
    }
    [Fact]
    public async Task PullOfUnchangedBaseRetainsDirtyLocalWorkAndMappingMustBeExplicit()
    {
        await using var f=new BrainstormingPersistenceTests.Database();await f.Start();var remote=new Remote(f);var local=Local(f);var server=f.Scope();
        var service=new BrainstormingSessionService(new LocalBrainstormingStore(Root()),remote);await service.CreateAsync(local);
        await Assert.ThrowsAsync<InvalidDataException>(()=>service.SyncAsync(local,Map(local,server),()=>{}));
        await service.MapAsync(local,server);await service.SyncAsync(local,Map(local,server),()=>{});await service.AppendUnsentAsync(local,"Keep dirty",Guid.NewGuid());
        var read=await service.PullAsync(local,Map(local,server),()=>{});Assert.Empty(read.Conflicts);Assert.Equal("Keep dirty",Assert.Single(read.Session.Turns).Text);
        var accountless=local with{SessionId=Guid.NewGuid(),AccountKey=null,LocalOwnerId=Guid.NewGuid()};await service.CreateAsync(accountless);
        await Assert.ThrowsAsync<InvalidDataException>(()=>service.MapAsync(accountless,server with{SessionId=Guid.NewGuid()}));
    }
    [Fact]
    public async Task LateIdentityOrMappingChangeRetainsPendingUntilExplicitReconciliation()
    {
        await using var f=new BrainstormingPersistenceTests.Database();await f.Start();var remote=new Remote(f);var local=Local(f);var server=f.Scope();
        var service=new BrainstormingSessionService(new LocalBrainstormingStore(Root()),remote);await service.CreateAsync(local);await service.MapAsync(local,server);
        bool current=true;remote.AfterWrite=()=>{current=false;return Task.CompletedTask;};
        void Guard(){if(!current)throw new OperationCanceledException("Synthetic identity change");}
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>service.SyncAsync(local,Map(local,server),Guard));
        var pending=(await service.ReadAsync(local))!;Assert.NotNull(pending.Pending);Assert.Null(pending.Acknowledgement);
        await Assert.ThrowsAsync<InvalidDataException>(()=>service.SaveAsync(pending with{Pending=pending.Pending! with{OperationId=Guid.NewGuid()}},pending.CacheRevision,Guid.NewGuid()));
        current=true;remote.AfterWrite=null;
        await Assert.ThrowsAsync<InvalidDataException>(()=>service.SyncAsync(local,Map(local,server),Guard,requireMapping:_=>throw new InvalidDataException("Synthetic document remapping")));
        Assert.NotNull((await service.ReadAsync(local))!.Pending);
        Assert.Null((await service.SyncAsync(local,Map(local,server),Guard)).Pending);Assert.Equal(1,remote.Writes);
    }
    [Fact]
    public async Task RealAuthenticatedControllerAndHttpAdapterExerciseCrudPaginationAndWrongScope()
    {
        await using var f=new BrainstormingPersistenceTests.Database();await f.Start();
        using var host=new HostBuilder().ConfigureWebHost(web=>web.UseTestServer().ConfigureServices(services=>{
            services.AddAuthentication("test").AddScheme<AuthenticationSchemeOptions,BrainstormingCapabilityTests.TestAuthentication>("test",_=>{});
            services.AddAuthorization();services.AddScoped(_=>new WriterApp.Data.AppDbContext(f.Options));services.AddScoped<IUserIdResolver,UserIdResolver>();
            services.AddControllers().AddApplicationPart(typeof(BrainstormingSessionsController).Assembly);
        }).Configure(app=>{app.UseRouting();app.UseAuthentication();app.UseAuthorization();app.UseEndpoints(e=>e.MapControllers());})).Build();
        await host.StartAsync();using var http=host.GetTestClient();var route="api/brainstorming/sessions";
        Assert.Equal(HttpStatusCode.Unauthorized,(await http.GetAsync(route)).StatusCode);http.DefaultRequestHeaders.Add("X-Test-Identity","fixture");
        // Test auth deliberately identifies synthetic-owner; seed that owner explicitly.
        await using(var db=new WriterApp.Data.AppDbContext(f.Options)){(await db.Documents.SingleAsync()).OwnerUserId="synthetic-owner";(await db.Projects.SingleAsync()).OwnerUserId="synthetic-owner";await db.SaveChangesAsync();}
        var scope=f.Scope() with{BackendKey=BrainstormingValidation.Fingerprint(http.BaseAddress!.AbsoluteUri),AccountKey=BrainstormingValidation.Fingerprint("synthetic-owner")};
        var remote=new BrainstormingHttpRemote(http,_=>new(()=>{}),doc=>scope with{DocumentId=doc});
        var session=BrainstormingPersistence.Empty(scope) with{State=BrainstormingConversationState.Saved};var write=new BrainstormingWrite(1,Guid.NewGuid(),0,session);
        var receipt=await remote.WriteAsync(write);Assert.Equal(receipt,await remote.WriteAsync(write));Assert.Equal(receipt,await remote.InspectAsync(scope,write.OperationId));
        Assert.Equal(scope,(await remote.ReadAsync(scope))!.Scope);Assert.Single((await remote.ListAsync(scope.DocumentId,limit:1)).Items);
        var turn=new BrainstormingTurn(1,Guid.NewGuid(),scope,1,null,Guid.NewGuid(),null,null,BrainstormingRole.User,BrainstormingTurnState.Accepted,"Accepted without provider",DateTimeOffset.UtcNow);
        session=session with{Revision=2,Turns=[turn],UpdatedAt=DateTimeOffset.UtcNow};await remote.WriteAsync(new(1,Guid.NewGuid(),1,session));
        var page=await http.GetFromJsonAsync<BrainstormingTurnPage>($"{route}/{scope.SessionId}/turns?documentId={scope.DocumentId}&limit=1");Assert.Single(page!.Items);
        var error=await Assert.ThrowsAsync<BrainstormingConcurrencyException>(()=>remote.WriteAsync(new(1,Guid.NewGuid(),1,session)));Assert.Equal(2,error.Current!.Revision);
        Assert.Equal(HttpStatusCode.BadRequest,(await http.GetAsync($"{route}/{scope.SessionId}/turns?documentId={scope.DocumentId}&limit=51")).StatusCode);
        Assert.Null(await remote.ReadAsync(scope with{DocumentId=Guid.NewGuid()}));
        await Assert.ThrowsAsync<HttpRequestException>(()=>remote.WriteAsync(write with{Session=session with{Revision=1,Scope=scope with{AccountKey=new('C',64)},Turns=[]}}));
        await remote.WriteAsync(new(1,Guid.NewGuid(),2,session with{Revision=3,State=BrainstormingConversationState.Deleted}));
        Assert.Equal(BrainstormingConversationState.Deleted,(await remote.ReadAsync(scope))!.State);
        http.DefaultRequestHeaders.Remove("X-Test-Identity");await Assert.ThrowsAsync<HttpRequestException>(()=>remote.ReadAsync(scope));
    }
    private sealed class Identity : IDeviceIdentityClient {
        public bool IsConfigured=>true;
        public Task<DeviceAccessToken?> AcquireAsync(bool interactive,CancellationToken cancellationToken)=>Task.FromResult<DeviceAccessToken?>(new("fixture",DateTimeOffset.UtcNow.AddHours(1),"Author","author"));
        public Task SignOutAsync()=>Task.CompletedTask;
    }
    [Fact]
    public async Task ProductionDeviceHostRegistrationResolvesSharedPresentationBoundary()
    {
        var services=new ServiceCollection();services.AddWriterAppDeviceCore(new("Windows fixture",new("https://test.invalid/")),Root());
        await using var provider=services.BuildServiceProvider();using var lifetime=provider.CreateScope();
        var sp=lifetime.ServiceProvider;var adapter=sp.GetRequiredService<DeviceBrainstormingSessions>();
        Assert.Same(adapter,sp.GetRequiredService<IBrainstormingCacheStore>());Assert.IsType<BrainstormingHttpRemote>(sp.GetRequiredService<IBrainstormingRemote>());
        var doc=await sp.GetRequiredService<LocalDocumentRepository>().CreateAsync("Host fixture");var scope=await adapter.ScopeAsync(doc.DocumentId);
        var saved=await sp.GetRequiredService<BrainstormingSessionService>().CreateAsync(scope);Assert.Equal(scope,(await adapter.ReadAsync(scope))!.Session.Scope);
        Assert.Equal(BrainstormingConversationState.LocalOnly,saved.Session.State);
    }
    [Fact]
    public async Task ProductionDeviceAdapterKeepsAnonymousArchiveSeparateAndReadsOfflineAfterRestart()
    {
        var root=Root();var repository=new LocalDocumentRepository(new FileLocalDocumentStore(Path.Combine(root,"documents")));var doc=await repository.CreateAsync("Unchanged manuscript");
        var account=new DeviceAccountService(new Identity());var network=new DeviceConnectivity();network.SetOnline(false);var host=new DeviceHostOptions("test",new("https://test.invalid/"));
        using var http=new HttpClient(){BaseAddress=host.ApiBaseAddress};var files=new LocalBrainstormingStore(Path.Combine(root,"sessions"));var adapter=new DeviceBrainstormingSessions(files,account,host,network,repository,http);
        var anonymous=await adapter.ScopeAsync(doc.DocumentId);await adapter.Sessions.CreateAsync(anonymous);await adapter.Sessions.SaveComposerAsync(anonymous,"Private accountless idea");
        await Assert.ThrowsAsync<InvalidDataException>(()=>adapter.MapAsync(anonymous,Guid.NewGuid()));await account.SignInAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>adapter.ReadAsync(anonymous));var owned=await adapter.ScopeAsync(doc.DocumentId);Assert.NotEqual(BrainstormingPersistence.Partition(anonymous),BrainstormingPersistence.Partition(owned));
        await adapter.Sessions.CreateAsync(owned);await adapter.Sessions.AppendUnsentAsync(owned,"Offline authenticated idea",Guid.NewGuid());
        var reopened=new DeviceBrainstormingSessions(new LocalBrainstormingStore(Path.Combine(root,"sessions")),account,host,network,repository,http);
        Assert.Single((await reopened.ReadAsync(owned))!.Session.Turns);await account.SignOutAsync();Assert.Equal("Private accountless idea",(await reopened.ReadAsync(anonymous))!.Composer);
        Assert.Equal(doc.Sections[0].Pages[0].Content,(await repository.LoadAsync(doc.DocumentId))!.Sections[0].Pages[0].Content);
    }
}
