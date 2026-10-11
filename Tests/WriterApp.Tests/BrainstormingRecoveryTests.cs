using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using WriterApp.Application.AI;
using WriterApp.Client.Services;
using WriterApp.Device.Shared.Services;
using WriterApp.Device.Shared.Storage;
using WriterApp.Shared.Brainstorming;
using WriterApp.UI.Shared;
using Xunit;
using static WriterApp.Tests.BrainstormingKeepHostTests;

namespace WriterApp.Tests;

public sealed class BrainstormingRecoveryTests
{
    private static string Root()=>Path.Combine(Path.GetTempPath(),"BrainstormingP09",Guid.NewGuid().ToString());
    private sealed class Identity:IDeviceIdentityClient {public bool IsConfigured=>true;public Task<DeviceAccessToken?> AcquireAsync(bool interactive,CancellationToken ct)=>Task.FromResult<DeviceAccessToken?>(new("synthetic",DateTimeOffset.UtcNow.AddHours(1),"Author","author"));public Task SignOutAsync()=>Task.CompletedTask;}
    private sealed class CacheJs:IJSRuntime,IJSObjectReference {
        private readonly LocalBrainstormingStore store=new(Root());private readonly Dictionary<string,BrainstormingScope> scopes=[];
        private static string Key(object?[] a)=>a[0]+":"+a[1]+":"+a[2];
        public ValueTask<T> InvokeAsync<T>(string n,object?[]? a)=>InvokeAsync<T>(n,default,a);
        public async ValueTask<T> InvokeAsync<T>(string n,CancellationToken ct,object?[]? a){a??=[];object? value=null;
            switch(n){case "import":value=this;break;case "isOffline":value=false;break;case "rememberIdentity":break;
                case "read":if(scopes.TryGetValue(Key(a),out var scope))value=BrainstormingPersistence.Json(await store.ReadAsync(scope,ct));break;
                case "write":var cache=JsonSerializer.Deserialize<BrainstormingCache>((string)a[3]!,BrainstormingValidation.Json)!;scopes[Key(a)]=cache.Session.Scope;value=JsonSerializer.SerializeToElement(new{conflict=false,json=BrainstormingPersistence.Json(await store.WriteAsync(cache,(long)a[4]!,(Guid)a[5]!,ct))});break;
                case "list":var summaries=new List<string>();foreach(var s in scopes.Values.Where(s=>BrainstormingPersistence.Partition(s)==(string)a[0]! && s.DocumentId==(Guid)a[1]!).Where(s=>a[2] is not Guid after || s.SessionId.CompareTo(after)>0).OrderBy(s=>s.SessionId).Take((int)a[3]!).ToArray()){var c=(await store.ReadAsync(s,ct))!;summaries.Add(BrainstormingPersistence.Json(new BrainstormingSummary(s,c.Session.Revision,c.Session.State,c.Session.UpdatedAt)));}value=summaries.ToArray();break;
                default:throw new InvalidOperationException(n);
            }return value is null?default!:(T)value;
        }
        public ValueTask DisposeAsync()=>ValueTask.CompletedTask;
    }
    private sealed class Components:IComponentActivator {public BrainstormingWorkspace Workspace=default!;public BrainstormingConversationPanel Conversation=default!;public BrainstormingProposalPreview Preview=default!;public IComponent CreateInstance(Type t){var c=(IComponent)Activator.CreateInstance(t)!;if(c is BrainstormingWorkspace w)Workspace=w;if(c is BrainstormingConversationPanel p)Conversation=p;if(c is BrainstormingProposalPreview v)Preview=v;return c;}}
    private sealed class OfflineConversation:IBrainstormingConversation {public int Sends;public async IAsyncEnumerable<BrainstormingConversationEvent> SendAsync(BrainstormingSend send,[System.Runtime.CompilerServices.EnumeratorCancellation]CancellationToken ct=default){Sends++;await Task.CompletedTask;yield break;}public Task<BrainstormingGeneration?> InspectAsync(BrainstormingScope s,Guid op,CancellationToken ct=default)=>Task.FromResult<BrainstormingGeneration?>(null);public Task StopAsync(BrainstormingScope s,Guid op,CancellationToken ct=default)=>Task.CompletedTask;}
    [Fact]
    public async Task MissingInspectedGenerationRetainsMessageAndCannotBorrowEarlierSuccess() {
        await using var f=new BrainstormingConversationTests.Fixture();await f.Start();var local=f.Send.Scope with {Space=BrainstormingIdentitySpace.Local};var store=new LocalBrainstormingStore(Root());var old=f.Send with {OperationId=Guid.NewGuid()};var cache=new BrainstormingCache(1,1,BrainstormingPersistence.ReScope(BrainstormingPersistence.Empty(f.Send.Scope) with {Contexts=[f.Context],Targets=[f.Target]},local),f.Send.Instruction,new(1,local,f.Send.Scope,1,DateTimeOffset.UtcNow),null,null,[],[],PendingGeneration:f.Send,LastGeneration:new(1,old,BrainstormingGenerationState.Completed,[],"Previous answer",new(BrainstormingUsageState.Settled,"fixture","test",10,20),"Old success",DateTimeOffset.UtcNow));await store.WriteAsync(cache,0,Guid.NewGuid());var remote=new OfflineConversation();
        var restored=await new BrainstormingConversationWorkflow(store,remote,async(s,_,ct)=>(await store.ReadAsync(s,ct))!).InspectAsync(local);Assert.Null(restored.PendingGeneration);Assert.Equal(BrainstormingGenerationState.Failed,restored.LastGeneration!.State);Assert.Equal(f.Send,restored.LastGeneration.Send);Assert.Equal(BrainstormingUsageState.NotDispatched,restored.LastGeneration.Usage.State);Assert.Equal(f.Send.Instruction,restored.Composer);Assert.Equal(0,remote.Sends);
    }
    [Theory]
    [InlineData("sessions")][InlineData("context")][InlineData("stream")][InlineData("inspect")][InlineData("stop")]
    public async Task RevokedAuthenticatedSessionClearsActiveSharedWorkspaceAndRetainsCacheAndWriting(string action) {
        await using var f=new BrainstormingConversationTests.Fixture();var (_,server)=await BrainstormingPlanningTests.Server(f);using var host=await Host(f);using var http=host.GetTestClient();http.BaseAddress=new("https://test.invalid/");http.DefaultRequestHeaders.Add("X-Test-Identity","author");var adapter=new WebBrainstormingSessions(new(new CacheJs()),http,new AuthStateService(http,NullLogger<AuthStateService>.Instance,new(),new()));var partition=await adapter.ScopeAsync(server.Scope.DocumentId,null);await adapter.DiscoverAsync(partition);var summary=(await adapter.ListAsync(partition)).Single();
        var components=new Components();using var services=new ServiceCollection().AddLogging().AddSingleton<IComponentActivator>(components).BuildServiceProvider();await using var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async()=>{
            var html=await renderer.RenderComponentAsync<BrainstormingWorkspace>(ParameterView.FromDictionary(new Dictionary<string,object?>{{"Active",true},{"DocumentId",partition.DocumentId},{"Store",adapter},{"Sessions",adapter.Sessions},{"Workflow",adapter.ConversationWorkflow},{"ReviewIdentity",adapter},{"Capabilities",new Available()},{"ScopeFactory",(Func<Task<BrainstormingScope>>)(()=>Task.FromResult(partition))}}));
            Set(components.Workspace,"_selected",summary.Scope.SessionId.ToString());await Call(components.Workspace,"Reopen");http.DefaultRequestHeaders.Remove("X-Test-Identity");
            var error=await Record.ExceptionAsync(async()=>{
                switch(action) {
                    case "sessions":await adapter.Remote.ReadAsync(server.Scope);break;
                    case "context":await adapter.Conversation.PrepareAsync(new(1,server.Scope,new(SectionId:f.Data.Section),[],[],60_000));break;
                    case "stream":await foreach(var item in adapter.Conversation.SendAsync(f.Send)){}break;
                    case "inspect":await adapter.Conversation.InspectAsync(server.Scope,f.Send.OperationId);break;
                    case "stop":await adapter.Conversation.StopAsync(server.Scope,f.Send.OperationId);break;
                }
            });Assert.True(error is HttpRequestException or OperationCanceledException);
            await renderer.Dispatcher.InvokeAsync(()=>{});typeof(BrainstormingWorkspace).GetMethod("StateHasChanged",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.Invoke(components.Workspace,null);Assert.Contains("Make room for your next idea",html.ToHtmlString());
        });Assert.NotNull(await adapter.ReadAsync(summary.Scope));Assert.Equal("<p><strong>Åsa 日本語 🧭</strong></p>",(await f.Db.Pages.SingleAsync()).Content);
    }
    private sealed class Available:IBrainstormingCapabilityService {public Task<BrainstormingCapabilities> ReadAsync(CancellationToken ct=default)=>Task.FromResult(BrainstormingCapabilities.Current);}
    [Theory][InlineData("Web")][InlineData("Device")]
    public async Task ChapterTwoConversationManualRevisionKeepAndUndoThroughProductionRoutes(string surface) {
        await using var f=new BrainstormingConversationTests.Fixture();await BrainstormingKeepTests.Server(f,"new");using var host=await Host(f,conversation:true);using var http=host.GetTestClient();http.BaseAddress=new("https://test.invalid/");http.DefaultRequestHeaders.Add("X-Test-Identity","author");
        var server=(await new BrainstormingSessions(f.Db,"author",f.Data.Backend).ReadAsync(f.Data.Document,f.Send.Scope.SessionId,default))!;
        var originalPages=await f.Db.Pages.AsNoTracking().Select(p=>new {p.Id,p.Content}).ToArrayAsync();var js=new Js();IBrainstormingCacheStore store;BrainstormingSessionService sessions;BrainstormingConversationWorkflow workflow;BrainstormingScope local;WebBrainstormingSessions? web=null;DeviceBrainstormingSessions? device=null;LocalDocumentRepository? documents=null;DeviceAccountService? account=null;
        if(surface=="Web") {web=new(new(new CacheJs()),http,new AuthStateService(http,NullLogger<AuthStateService>.Instance,new(),new()));var partition=await web.ScopeAsync(f.Data.Document,f.Data.Project);await web.DiscoverAsync(partition);local=(await web.ListAsync(partition)).Single(s=>s.Scope.SessionId==server.Scope.SessionId).Scope;store=web;sessions=web.Sessions;workflow=web.ConversationWorkflow;}
        else {var source=await new BrainstormingStorySourceReader(f.Db,"author").ReadAsync(f.Data.Document);var nodes=source.Outline.Nodes.Select(n=>new WriterApp.Shared.Sync.SyncProjectNode(n.Id,n.ParentId,n.Type,n.Title,n.Order,n.SectionId)).ToArray();
            var snapshot=new WriterApp.Shared.Sync.SyncSnapshot(new(f.Data.Document,source.Outline.DocumentVersion,false,false),new(f.Data.Document,f.Data.Project,"Book","sv","manuscript",false,DateTimeOffset.UtcNow,DateTimeOffset.UtcNow,
                source.Outline.Sections.Select(s=>new WriterApp.Shared.Sync.SyncSection(s.Id,s.Title,s.Order,null,"sv",source.Pages.Where(p=>p.SectionId==s.Id).Select(p=>new WriterApp.Shared.Sync.SyncPage(p.Id,p.Title,p.Order,p.Content)).ToArray())).ToArray(),new(f.Data.Project,"Book",nodes,Version:3,PrimaryDocumentId:f.Data.Document,MetadataRevision:1)));
            var d=DeviceSyncMapping.Download(snapshot,Guid.NewGuid(),null,DateTimeOffset.UtcNow);var docStore=new FileLocalDocumentStore(Root());await docStore.ApplySyncAsync(d,null,default,projects:true);documents=new(docStore);account=new(new Identity());await account.SignInAsync();var online=new DeviceConnectivity();online.SetOnline(true);device=new(new(Root()),account,new("Test",http.BaseAddress),online,documents,http);var partition=await device.ScopeAsync(d.DocumentId);await device.DiscoverAsync(partition);local=(await device.ListAsync(partition)).Single(s=>s.Scope.SessionId==server.Scope.SessionId).Scope;store=device;sessions=device.Sessions;workflow=device.ConversationWorkflow;
        }
        var components=new Components();using var services=new ServiceCollection().AddLogging().AddSingleton<IComponentActivator>(components).BuildServiceProvider();await using var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async()=>{
            await renderer.RenderComponentAsync<BrainstormingConversationPanel>(ParameterView.FromDictionary(new Dictionary<string,object?>{{"Scope",local},{"Ready",true},{"Store",store},{"Workflow",workflow}}));
            f.Provider.Wire=BrainstormingPersistence.Json(new BrainstormingAnswer("Three alternatives, without changing the draft.",1,BrainstormingIntent.Explore,null,[],[]));Set(components.Conversation,"_composer","Explore three opening ideas without changing writing.");await Call(components.Conversation,"Send");var cache=(await store.ReadAsync(local))!;Assert.Equal(2,cache.Session.Drafts.Count);Assert.Null(cache.PendingGeneration);
            var parent=cache.Session.Drafts.Last();await sessions.SaveDraftEditAsync(local,parent.Id,BrainstormingValidation.CandidateFingerprint(parent),"<p><strong>She stepped back.</strong> 日本語</p>","She stepped back. 日本語");cache=await sessions.CheckpointDraftAsync(local);var manual=cache.Session.Drafts.Last();Assert.True(manual.ManuallyEdited);
            // Reopen the actual shared composer, then revise its retained manual version over authenticated HTTP.
            await Call(components.Conversation,"Inspect");f.Provider.Wire=BrainstormingPersistence.Json(new BrainstormingAnswer("More suspicious, with restrained dialogue.",1,BrainstormingIntent.Revise,new("She kept her distance. 日本語 🧭","Restrained dialogue",[],RevisionInstruction:"Make her more suspicious and keep the dialogue understated."),[],[]));Set(components.Conversation,"_composer","Make her more suspicious and keep the dialogue understated.");await Call(components.Conversation,"Send");cache=(await store.ReadAsync(local))!;Assert.Null(cache.PendingGeneration);var candidate=cache.Session.Drafts.Last();Assert.Equal(manual.Id,candidate.ParentDraftId);
            using var request=JsonDocument.Parse(f.Provider.Request!.Inputs["conversation_data"].ToString()!);Assert.Equal(manual.PlainText,request.RootElement.GetProperty("previousDraft").GetProperty("plainText").GetString());Assert.Contains("The letter arrived",request.RootElement.GetProperty("context").GetRawText());
            Func<Guid,bool,string,Task<BrainstormingKeepReview>> prepare;Func<BrainstormingKeepReview,Task<BrainstormingCache>> apply;Func<BrainstormingKeepJournal,Task<BrainstormingCache>> undo;Mount? mount=null;LocalAutosaveCoordinator? autosave=null;
            if(web is not null){var editor=new MountedWeb();Set(editor,"DocumentId",local.DocumentId);Set(editor,"_brainstormContextScope",local);Set(editor,"BrainstormSessions",web);Set(editor,"BrainstormContext",new WebBrainstormingContext(http,null!,web,new(js)));Set(editor,"JSRuntime",js);prepare=(id,replace,title)=>Call<BrainstormingKeepReview>(editor,"PrepareBrainstormKeep",id,replace,title);apply=r=>Call<BrainstormingCache>(editor,"ApplyBrainstormKeep",r);undo=j=>Call<BrainstormingCache>(editor,"ChangeBrainstormKeep",j);}
            else {var d=(await documents!.LoadAsync(local.DocumentId))!;var history=new LocalAiStore(Root());var editor=new MountedDevice();mount=new(services);mount.Attach(editor);var editing=new LocalEditorSession(documents,d);autosave=new(editing,new LocalRecoveryStore(Root()),()=>Task.CompletedTask);var text=new WriterApp.Device.Shared.Components.DeviceTextEditor();Set(text,"_editor",js);js.Html=d.Sections.First().Pages.First().Content;
                Set(editor,"DocumentId",local.DocumentId);Set(editor,"_brainstormContextScope",local);Set(editor,"BrainstormSessions",device);Set(editor,"Documents",documents);Set(editor,"_session",editing);Set(editor,"_selectedPage",d.Sections.First().Pages.First().PageId);Set(editor,"_editor",text);Set(editor,"_autosave",autosave);Set(editor,"AiHistory",history);Set(editor,"BrainstormKeeps",new LocalBrainstormingKeeps(documents,history,device!,account!,new("Test",http.BaseAddress)));Set(editor,"BrainstormKeepJS",js);
                prepare=(id,replace,title)=>mount.Dispatcher.InvokeAsync(()=>Call<BrainstormingKeepReview>(editor,"PrepareBrainstormKeepAsync",id,replace,title));apply=r=>mount.Dispatcher.InvokeAsync(async()=>{Set(editor,"_editor",null);return await Call<BrainstormingCache>(editor,"ApplyBrainstormKeepAsync",r);});undo=j=>mount.Dispatcher.InvokeAsync(()=>Call<BrainstormingCache>(editor,"ChangeBrainstormKeepAsync",j));
            }
            try {var html=await renderer.RenderComponentAsync<BrainstormingProposalPreview>(ParameterView.FromDictionary(new Dictionary<string,object?>{{"Cache",cache},{"DocumentId",local.DocumentId},{"Sessions",sessions},{"PrepareKeep",prepare},{"ApplyKeep",apply},{"ChangeApplication",undo},{"CacheChanged",EventCallback.Factory.Create<BrainstormingCache>(components,c=>{cache=c;Set(components.Preview,"Cache",c);})},{"EditorHost",surface}}));await Call(components.Preview,"ReviewKeep",false);await Call(components.Preview,"ConfirmKeep");cache=(await store.ReadAsync(local))!;var journal=Assert.Single(cache.Keeps!);Assert.Equal(BrainstormingApplicationState.Committed,journal.Receipt!.State);Assert.Equal(BrainstormingTargetKind.CreateSection,journal.Review.Kind);
                if(surface=="Web"){Assert.Equal(journal.Review.AfterHtml,(await f.Db.Pages.AsNoTracking().SingleAsync(p=>p.Id==journal.Review.NewPageId)).Content);foreach(var p in originalPages)Assert.Equal(p.Content,(await f.Db.Pages.AsNoTracking().SingleAsync(x=>x.Id==p.Id)).Content);}
                else {var saved=(await documents!.LoadAsync(local.DocumentId))!;Assert.Equal(journal.Review.AfterHtml,saved.Sections.Single(s=>s.SectionId==journal.Review.NewSectionId).Pages.Single().Content);Assert.Equal(originalPages.Length+1,saved.Sections.Sum(s=>s.Pages.Count));}
                await Call(components.Preview,"MoveKeep",journal);Assert.Equal(BrainstormingApplicationState.Undone,(await store.ReadAsync(local))!.Keeps!.Single().Receipt!.State);
                typeof(BrainstormingProposalPreview).GetMethod("StateHasChanged",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.Invoke(components.Preview,null);Assert.Contains("Undone",html.ToHtmlString());
            }finally{autosave?.Dispose();mount?.Dispose();}
        });Assert.Equal(3,f.Provider.Calls);Assert.Equal(3,f.Provider.Charges);
    }
    [Theory][InlineData("Web")][InlineData("Device")]
    public async Task ProductionAdaptersDiscoverMappedSessionAndAuthoritativeHistoryWithoutReplay(string surface) {
        await using var f=new BrainstormingConversationTests.Fixture();var (planning,server)=await BrainstormingPlanningTests.Server(f,migrated:true);
        var review=BrainstormingPlanningTests.Review(server,await planning.CaptureAsync(server.Scope),BrainstormingPlanningKind.StoryNote);await planning.ApplyAsync(review);
        using var host=await Host(f);using var http=host.GetTestClient();http.BaseAddress=new("https://test.invalid/");http.DefaultRequestHeaders.Add("X-Test-Identity","author");
        IBrainstormingCacheStore store;BrainstormingScope partition;Func<BrainstormingScope,Task> discover;Func<BrainstormingScope,bool,Task<BrainstormingCache>> sync;DeviceAccountService? account=null;LocalDocumentRepository? documents=null;
        if(surface=="Web") {var adapter=new WebBrainstormingSessions(new(new CacheJs()),http,new AuthStateService(http,NullLogger<AuthStateService>.Instance,new(),new()));partition=await adapter.ScopeAsync(server.Scope.DocumentId,null);store=adapter;discover=s=>adapter.DiscoverAsync(s);sync=(s,p)=>adapter.SyncAsync(s,p);}
        else {var docStore=new FileLocalDocumentStore(Root());documents=new(docStore);var d=await documents.CreateAsync("Mapped local copy");d=d with {ServerDocumentId=f.Data.Document,ServerProjectId=f.Data.Project,ServerVersion="v1",SyncState=LocalSyncState.Synced,Sections=d.Sections.Select(s=>s with {ServerSectionId=f.Data.Section,Pages=s.Pages.Select(p=>p with {ServerPageId=f.Data.Page}).ToArray()}).ToArray()};await docStore.ApplySyncAsync(d,d.LocalRevision,default);
            account=new(new Identity());await account.SignInAsync();var online=new DeviceConnectivity();online.SetOnline(true);var adapter=new DeviceBrainstormingSessions(new(Root()),account,new("Test",http.BaseAddress),online,documents,http);partition=await adapter.ScopeAsync(d.DocumentId);store=adapter;discover=s=>adapter.DiscoverAsync(s);sync=(s,p)=>adapter.SyncAsync(s,p);}
        await discover(partition);var summaries=await store.ListAsync(partition);Assert.Single(summaries);var local=summaries.Single().Scope;Assert.Equal(partition.DocumentId,local.DocumentId);
        var cache=(await store.ReadAsync(local))!;Assert.Equal(server.Scope,cache.Mapping!.Server);Assert.Equal(review.OperationId,cache.Recovery!.ServerPlanning.Single().Review.OperationId);Assert.Equal(BrainstormingApplicationState.Committed,cache.Recovery.ServerPlanning.Single().Receipt!.State);
        Assert.Empty(cache.PlanningOperations??[]);Assert.Equal(1,f.Provider.Calls);await discover(partition);Assert.Single(await store.ListAsync(partition));await sync(local,true);Assert.Equal(1,f.Provider.Calls);
        var before=await store.ReadAsync(local);if(account is not null){await account.SignOutAsync();await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>store.ReadAsync(local));Assert.NotNull(await documents!.LoadAsync(local.DocumentId));}
        Assert.Equal(server.Contexts.Count,before!.Session.Contexts.Count);
    }
    [Fact]
    public async Task DeviceReportedPendingRecoverySurvivesServerRestartWithoutManufacturingApplication() {
        await using var f=new BrainstormingConversationTests.Fixture();var (service,server)=await BrainstormingPlanningTests.Server(f,migrated:true);var r=BrainstormingPlanningTests.Review(server,await service.CaptureAsync(server.Scope),BrainstormingPlanningKind.CharacterNote);
        var local=server.Scope with {Space=BrainstormingIdentitySpace.Local,DocumentId=Guid.NewGuid()};var localReview=r with {Scope=local};var journal=new BrainstormingPlanningJournal(localReview,new(1,r.OperationId,local,BrainstormingPlanningContract.Hash(localReview),BrainstormingApplicationState.Unknown,DateTimeOffset.UtcNow,"Device response lost."));
        var ids=new BrainstormingIdentityMap(new Dictionary<Guid,Guid>{{server.Scope.DocumentId,local.DocumentId}},new Dictionary<Guid,Guid>{{f.Data.Section,f.Data.Section}},new Dictionary<Guid,Guid>{{f.Data.Page,f.Data.Page}},new Dictionary<Guid,Guid>());
        var cache=new BrainstormingCache(1,1,ids.Convert(server,local),"Private unsent composer",new(1,local,server.Scope,1,DateTimeOffset.UtcNow),null,null,[],[],PlanningOperations:[journal]);var copy=BrainstormingRecovery.Copy(cache)!;
        var history=new BrainstormingRecoveryHistory(f.Db,"author",f.Data.Backend);var bundle=await history.ExchangeAsync(server.Scope,copy);Assert.Single(bundle.Copies);Assert.Empty(bundle.ServerPlanning);Assert.Empty(await f.Db.BrainstormingPlanningSaves.ToArrayAsync());
        Assert.DoesNotContain("Private unsent composer",BrainstormingPersistence.Json(bundle));await using var reopened=new WriterApp.Data.AppDbContext(f.Data.Options);history=new(reopened,"author",f.Data.Backend);Assert.Equal(BrainstormingPersistence.Json(bundle),BrainstormingPersistence.Json(await history.ExchangeAsync(server.Scope,copy)));
        await Assert.ThrowsAsync<BrainstormingAccessException>(()=>new BrainstormingRecoveryHistory(reopened,"foreign",f.Data.Backend).ExchangeAsync(server.Scope,copy));
        await Assert.ThrowsAsync<InvalidDataException>(()=>history.ExchangeAsync(server.Scope,copy with {Id=new string('F',64)}));
        Assert.Empty((await new BrainstormingSessions(reopened,"author",f.Data.Backend).ReadAsync(server.Scope.DocumentId,server.Scope.SessionId,default))!.Applications);
        cache=await new LocalBrainstormingStore(Root()).WriteAsync(cache with {Recovery=bundle},0,Guid.NewGuid());
        using var services=new ServiceCollection().AddLogging().BuildServiceProvider();await using var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());await renderer.Dispatcher.InvokeAsync(async()=>{
            var html=await renderer.RenderComponentAsync<BrainstormingRecoveryPanel>(ParameterView.FromDictionary(new Dictionary<string,object?>{{"Cache",cache with {Recovery=bundle}}}));Assert.Contains("do not confirm a server commit",html.ToHtmlString());Assert.Contains("Her brother",html.ToHtmlString());
            if(Environment.GetEnvironmentVariable("WRITERAPP_BRAINSTORMING_CONTEXT_HTML") is {Length:>0} path){await File.WriteAllTextAsync(Path.Combine(Path.GetDirectoryName(path)!,"recovery-review.html"),html.ToHtmlString());await File.WriteAllTextAsync(Path.Combine(Path.GetDirectoryName(path)!,"recovery-cache.json"),BrainstormingPersistence.Json(cache with {Recovery=bundle}));}
        });
    }
    [Fact]
    public async Task PullWithManualEditRetainsBothBranchesAndForkRetainsEditAndOriginalJournals() {
        await using var f=new BrainstormingConversationTests.Fixture();await BrainstormingKeepTests.Server(f,"insert");var remote=new BrainstormingHttpRemote(new HttpClient(),_=>new(()=>{}),_=>f.Send.Scope);
        var server=(await new BrainstormingSessions(f.Db,"author",f.Data.Backend).ReadAsync(f.Data.Document,f.Send.Scope.SessionId,default))!;using var host=await Host(f);using var http=host.GetTestClient();http.BaseAddress=new("https://test.invalid/");http.DefaultRequestHeaders.Add("X-Test-Identity","author");remote=new(http,_=>new(()=>{}),_=>server.Scope);
        var store=new LocalBrainstormingStore(Root());var sessions=new BrainstormingSessionService(store,remote);var local=server.Scope with {Space=BrainstormingIdentitySpace.Local};var ids=new BrainstormingIdentityMap(new Dictionary<Guid,Guid>{{local.DocumentId,server.Scope.DocumentId}},new Dictionary<Guid,Guid>(),new Dictionary<Guid,Guid>(),new Dictionary<Guid,Guid>(),true);
        var cache=await sessions.ImportAsync(local,server,ids,()=>{});var draft=cache.Session.Drafts.Last();await sessions.SaveDraftEditAsync(local,draft.Id,BrainstormingValidation.CandidateFingerprint(draft),"<p><strong>My retained edit</strong></p>","My retained edit");
        await new BrainstormingSessions(f.Db,"author",f.Data.Backend).WriteAsync(new(1,Guid.NewGuid(),server.Revision,server with {Revision=server.Revision+1,UpdatedAt=DateTimeOffset.UtcNow,EnabledSources=[]}),default);
        cache=await sessions.PullAsync(local,ids,()=>{});Assert.Single(cache.Conflicts);Assert.Equal("My retained edit",cache.DraftEdit!.PlainText);var originalJson=BrainstormingPersistence.Json(cache);
        var branch=await sessions.ForkAsync(local,Guid.NewGuid());Assert.Equal(cache.DraftEdit,branch.DraftEdit);Assert.Empty(branch.Session.Applications);Assert.Null(branch.PendingGeneration);Assert.Equal(originalJson,BrainstormingPersistence.Json(await store.ReadAsync(local)));
        var cloudBranch=await sessions.ForkServerAsync(local,Guid.NewGuid(),ids);Assert.Null(cloudBranch.DraftEdit);Assert.Equal(server.Drafts.Count,cloudBranch.Session.Drafts.Count);Assert.Equal(originalJson,BrainstormingPersistence.Json(await store.ReadAsync(local)));
    }
    [Theory][InlineData(true)][InlineData(false)]
    public async Task TransferredApplicationHistoryBlocksDuplicateKeepWithoutManufacturingLocalReceipt(bool authoritative) {
        await using var f=new BrainstormingConversationTests.Fixture();var review=await BrainstormingKeepTests.Server(f,"insert");var session=(await new BrainstormingSessions(f.Db,"author",f.Data.Backend).ReadAsync(f.Data.Document,review.Scope.SessionId,default))!;
        var local=review.Scope with {Space=BrainstormingIdentitySpace.Local};var mapping=new BrainstormingMapping(1,local,review.Scope,session.Revision,DateTimeOffset.UtcNow);var journal=new BrainstormingKeepJournal(review,new(1,review.OperationId,review.Scope,review.Candidate.Id,BrainstormingKeepContract.Hash(review),BrainstormingApplicationState.Unknown,Guid.NewGuid(),0,"Lost response",DateTimeOffset.UtcNow));
        var copy=BrainstormingRecovery.Copy(new(1,1,BrainstormingPersistence.ReScope(session,local),"",mapping,null,null,[],[],Keeps:[journal]))!;
        var cache=new BrainstormingCache(1,1,BrainstormingPersistence.ReScope(session,local),"",mapping,null,null,[],[],Recovery:new(1,review.Scope,authoritative?[]:[copy],authoritative?[journal]:[],[]));BrainstormingPersistence.Validate(cache);
        var components=new Components();using var services=new ServiceCollection().AddLogging().AddSingleton<IComponentActivator>(components).BuildServiceProvider();await using var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());var prepares=0;
        await renderer.Dispatcher.InvokeAsync(async()=>{
            var html=await renderer.RenderComponentAsync<BrainstormingProposalPreview>(ParameterView.FromDictionary(new Dictionary<string,object?>{{"Cache",cache},{"DocumentId",local.DocumentId},{"PrepareKeep",(Func<Guid,bool,string,Task<BrainstormingKeepReview>>)((_,_,_)=>{prepares++;return Task.FromResult(review);})}}));
            Assert.DoesNotContain("Keep this version · review insertion",html.ToHtmlString());Assert.Contains(authoritative?"Server operation Unknown":"Device-reported application history",html.ToHtmlString());await Call(components.Preview,"ReviewKeep",false);Assert.Equal(0,prepares);
        });Assert.Empty(cache.Keeps??[]);Assert.Empty(await f.Db.BrainstormingKeeps.ToArrayAsync());Assert.Equal(review.Before.Pages.Single().Content,(await f.Db.Pages.SingleAsync()).Content);
    }
    [Fact]
    public async Task LongConversationLimitsAreMeasuredAndExcessPreservesOriginal() {
        var scope=BrainstormingContractTests.Fixture().Scope;var session=BrainstormingPersistence.Empty(scope);var turns=new List<BrainstormingTurn>();Guid? parent=null;
        for(var i=1;i<=500;i++){var id=Guid.NewGuid();turns.Add(new(1,id,scope,i,parent,Guid.NewGuid(),null,null,BrainstormingRole.User,BrainstormingTurnState.Complete,new string('x',1000),DateTimeOffset.UtcNow));parent=id;}
        session=session with {Turns=turns};var clock=Stopwatch.StartNew();var bytes=System.Text.Encoding.UTF8.GetBytes(BrainstormingPersistence.Json(session));for(var i=0;i<10;i++)BrainstormingValidation.ReadSession(bytes);clock.Stop();var store=new LocalBrainstormingStore(Root());var saved=await store.WriteAsync(new(1,1,session,"",null,null,null,[],[]),0,Guid.NewGuid());
        var extra=session with {Revision=2,Turns=[..turns,new(1,Guid.NewGuid(),scope,501,parent,Guid.NewGuid(),null,null,BrainstormingRole.User,BrainstormingTurnState.Complete,"Excess",DateTimeOffset.UtcNow)]};await Assert.ThrowsAsync<InvalidDataException>(()=>store.WriteAsync(saved with {Session=extra},saved.CacheRevision,Guid.NewGuid()));Assert.Equal(500,(await store.ReadAsync(scope))!.Session.Turns.Count);
        if(Environment.GetEnvironmentVariable("WRITERAPP_BRAINSTORMING_CONTEXT_HTML") is {Length:>0} path)await File.WriteAllTextAsync(Path.Combine(Path.GetDirectoryName(path)!,"recovery-limits.json"),JsonSerializer.Serialize(new {messages=500,charactersPerMessage=1000,bytes=bytes.Length,validationIterations=10,validationMs=clock.Elapsed.TotalMilliseconds,displayPage=40,maxSessionBytes=BrainstormingValidation.MaxSessionBytes,maxContextCharacters=BrainstormingValidation.MaxContextCharacters,maxDialogue=BrainstormingConversationContract.MaxDialogue}));
    }
}

