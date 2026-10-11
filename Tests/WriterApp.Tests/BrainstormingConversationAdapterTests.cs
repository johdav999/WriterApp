using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;
using WriterApp.AI.Abstractions;
using WriterApp.Application.AI;
using WriterApp.Application.Continuity;
using WriterApp.Application.Documents;
using WriterApp.Application.Security;
using WriterApp.Application.Search;
using WriterApp.Application.Subscriptions;
using WriterApp.Application.Usage;
using WriterApp.Client.Components.Editor;
using WriterApp.Client.Pages;
using WriterApp.Client.Services;
using WriterApp.Controllers;
using WriterApp.Data;
using WriterApp.Data.Subscriptions;
using WriterApp.Device.Shared.Components;
using WriterApp.Device.Shared.Pages;
using WriterApp.Device.Shared.Services;
using WriterApp.Device.Shared.Storage;
using WriterApp.Shared;
using WriterApp.Shared.Brainstorming;
using WriterApp.UI.Shared;
using Xunit;

namespace WriterApp.Tests;

public sealed partial class BrainstormingConversationAdapterTests
{
    private sealed class Auth(IOptionsMonitor<AuthenticationSchemeOptions> options,ILoggerFactory logger,System.Text.Encodings.Web.UrlEncoder encoder):AuthenticationHandler<AuthenticationSchemeOptions>(options,logger,encoder) {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()=>Task.FromResult(!Request.Headers.TryGetValue("X-Test-Identity",out var id)?AuthenticateResult.NoResult():AuthenticateResult.Success(new(new(new ClaimsIdentity([new Claim("oid",id.ToString())],"test")),"test")));
    }
    private static async Task<IHost> Host(BrainstormingConversationTests.Fixture f) {
        var host=new HostBuilder().ConfigureWebHost(web=>web.UseTestServer().ConfigureServices(services=> {
            services.AddAuthentication("test").AddScheme<AuthenticationSchemeOptions,Auth>("test",_=>{});services.AddAuthorization();services.AddSingleton<IUserIdResolver,UserIdResolver>();services.AddScoped(_=>new AppDbContext(f.Data.Options));
            services.AddSingleton<IAiRouter>(f.Provider);services.AddSingleton<IAiUsagePolicy>(f.Provider);services.AddSingleton<IAiQuotaService>(f.Provider);services.AddSingleton<IUsageMeter>(f.Provider);services.AddSingleton<IBibleStore>(f.Provider);services.AddSingleton<IEntitlementService>(f.Provider);
            services.AddSingleton(f.Stops);services.AddSingleton<IOptions<WriterAiOptions>>(Options.Create(new WriterAiOptions {Enabled=true,Streaming=new(){Enabled=true}}));services.AddControllers().AddApplicationPart(typeof(BrainstormingConversationController).Assembly);
            services.AddSingleton<IUserEntitlementStore,SyncPlans>();services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
            services.AddScoped<DocumentSyncService>();services.AddScoped<ProjectDeletionService>();services.AddSingleton<ISearchIndexBackfillQueue,SearchIndexBackfillQueue>();
        }).Configure(app=>{app.UseRouting();app.UseAuthentication();app.UseAuthorization();app.UseEndpoints(e=>{
            e.MapControllers();e.MapGet("api/auth/me",async context=>await context.Response.WriteAsJsonAsync(new AuthMeDto{IsAuthenticated=context.User.Identity?.IsAuthenticated==true,UserId=context.User.FindFirst("oid")?.Value,IsPaidAccessActive=true}));
        });})).Build();await host.StartAsync();return host;
    }
    [Fact]
    public async Task AuthenticatedProductionHttpStreamAndInspectionUseMigrationsAndNeverRedispatch() {
        await using var f=new BrainstormingConversationTests.Fixture();await f.Start(true);using var host=await Host(f);using var http=host.GetTestClient();http.BaseAddress=new("https://test.invalid/");
        Assert.Equal(HttpStatusCode.Unauthorized,(await http.PostAsJsonAsync("api/brainstorming/conversation/stream",f.Send)).StatusCode);
        http.DefaultRequestHeaders.Add("X-Test-Identity","author");var transport=new BrainstormingConversationHttp(http,_=>new(()=>{}));var events=new List<BrainstormingConversationEvent>();await foreach(var e in transport.SendAsync(f.Send))events.Add(e);
        Assert.Equal(BrainstormingEventKind.Completed,events[^1].Kind);var read=await transport.InspectAsync(f.Send.Scope,f.Send.OperationId);Assert.Equal(BrainstormingPersistence.Json(events),BrainstormingPersistence.Json(read!.Events));
        await foreach(var _ in transport.SendAsync(f.Send)){}Assert.Equal(1,f.Provider.Calls);Assert.Equal(1,f.Provider.Charges);
        Assert.Null(await transport.InspectAsync(f.Send.Scope,Guid.NewGuid()));
        await Assert.ThrowsAsync<HttpRequestException>(()=>transport.InspectAsync(f.Send.Scope with {DocumentId=Guid.NewGuid()},f.Send.OperationId));
        http.DefaultRequestHeaders.Remove("X-Test-Identity");http.DefaultRequestHeaders.Add("X-Test-Identity","other");await Assert.ThrowsAsync<HttpRequestException>(()=>transport.InspectAsync(f.Send.Scope,f.Send.OperationId));
    }
    [Theory]
    [InlineData("story",false)][InlineData("context",false)][InlineData("stream",false)][InlineData("inspect",false)][InlineData("stop",false)]
    [InlineData("story",true)][InlineData("context",true)][InlineData("stream",true)][InlineData("inspect",true)][InlineData("stop",true)]
    public async Task ActualOwnedRoutesSignalLostAccessWithoutProviderDispatch(string action,bool revokeDocument) {
        await using var f=new BrainstormingConversationTests.Fixture();await f.Start(true);
        var before=(await f.Db.Pages.SingleAsync()).Content;
        if(revokeDocument){(await f.Db.Documents.SingleAsync()).OwnerUserId="other";await f.Db.SaveChangesAsync();}
        using var host=await Host(f);using var http=host.GetTestClient();http.BaseAddress=new("https://test.invalid/");http.DefaultRequestHeaders.Add("X-Test-Identity",revokeDocument?"author":"other");
        var revoked=0;var transport=new BrainstormingConversationHttp(http,_=>new(()=>{},AccessRevoked:()=>revoked++));
        await Assert.ThrowsAsync<HttpRequestException>(async()=>{
            switch(action) {
                case "story":await transport.ReadStoryAsync(f.Send.Scope);break;
                case "context":await transport.PrepareAsync(new(1,f.Send.Scope,new(SectionId:f.Data.Section),[],[],60_000));break;
                case "stream":await foreach(var item in transport.SendAsync(f.Send)){}break;
                case "inspect":await transport.InspectAsync(f.Send.Scope,f.Send.OperationId);break;
                case "stop":await transport.StopAsync(f.Send.Scope,f.Send.OperationId);break;
            }
        });
        Assert.Equal(1,revoked);Assert.Equal(0,f.Provider.Calls);Assert.Equal(0,f.Provider.Charges);
        Assert.Equal(before,(await f.Db.Pages.SingleAsync()).Content);Assert.Empty(await f.Db.AiActionHistoryEntries.ToArrayAsync());
    }
    private static void Set(object value,string field,object? data) => BrainstormingKeepHostTests.Set(value,field,data);
    private static Task Invoke(object value,string method)=>BrainstormingKeepHostTests.Call(value,method);
    private sealed class Components:IComponentActivator {public BrainstormingConversationPanel? Panel;public BrainstormingWorkspace? Workspace;public IComponent CreateInstance(Type type){var result=(IComponent)Activator.CreateInstance(type)!;if(result is BrainstormingConversationPanel panel)Panel=panel;if(result is BrainstormingWorkspace workspace)Workspace=workspace;return result;}}
    [Fact]
    public async Task NavigationCancelsOldSendWithoutLockingOrDisposingNewWorkspaceCancellation() {
        var scope=new BrainstormingScope(1,Guid.NewGuid(),Guid.NewGuid(),null,BrainstormingIdentitySpace.Local,BrainstormingValidation.Fingerprint("https://test.invalid/"),BrainstormingValidation.Fingerprint("author"),null);
        var store=new LocalBrainstormingStore(Path.Combine(Path.GetTempPath(),"BrainstormingNavigation",Guid.NewGuid().ToString()));
        await store.WriteAsync(new BrainstormingCache(1,1,BrainstormingPersistence.Empty(scope),"Explore",null,null,null,[],[]),0,Guid.NewGuid());
        var started=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var nextCancellation=new CancellationTokenSource();
        var components=new Components();using var services=new ServiceCollection().AddLogging().AddSingleton<IComponentActivator>(components).BuildServiceProvider();await using var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async()=>await renderer.RenderComponentAsync<BrainstormingConversationPanel>(ParameterView.FromDictionary(new Dictionary<string,object?>{
            {"Scope",scope},{"Ready",true},{"Store",store},{"BeforeSend",(Func<Task>)(async()=>{started.SetResult();await release.Task;})}})));
        var sending=renderer.Dispatcher.InvokeAsync(()=>Invoke(components.Panel!,"Send"));await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var next=scope with{DocumentId=Guid.NewGuid(),SessionId=Guid.NewGuid()};
        try {
            await renderer.Dispatcher.InvokeAsync(async()=>{
                var old=(CancellationTokenSource)typeof(BrainstormingConversationPanel).GetField("_cancel",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(components.Panel)!;
                Set(components.Panel!,"Scope",next);await Invoke(components.Panel!,"OnParametersSetAsync");Assert.True(old.IsCancellationRequested);
                Assert.False((bool)typeof(BrainstormingConversationPanel).GetField("_busy",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(components.Panel)!);
                Set(components.Panel!,"_cancel",nextCancellation);
            });
        } finally {release.TrySetResult();}
        await sending;Assert.False(nextCancellation.IsCancellationRequested);nextCancellation.Cancel();
        Assert.Same(nextCancellation,typeof(BrainstormingConversationPanel).GetField("_cancel",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(components.Panel));
        Assert.Null(typeof(BrainstormingConversationPanel).GetField("_cache",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(components.Panel));
        Assert.Equal("Explore",(await store.ReadAsync(scope))!.Composer);
    }
    private static async Task ExercisePanel(IBrainstormingCacheStore store,BrainstormingScope scope,BrainstormingConversationWorkflow workflow,Func<Task> before,string surface) {
        var components=new Components();var services=new ServiceCollection().AddLogging().AddSingleton<IComponentActivator>(components).BuildServiceProvider();await using var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async()=>{
            var html=await renderer.RenderComponentAsync<BrainstormingConversationPanel>(ParameterView.FromDictionary(new Dictionary<string,object?>{{"Scope",scope},{"Ready",true},{"Store",store},{"Workflow",workflow},{"BeforeSend",before}}));
            Set(components.Panel!,"_composer","Give three openings. <img src=x onerror=evil()>");await Invoke(components.Panel!,"Send");
            var cache=(await store.ReadAsync(scope))!;Assert.Null(cache.PendingGeneration);Assert.Equal(2,cache.Session.Turns.Count);Assert.Equal(BrainstormingTurnState.Complete,cache.Session.Turns[^1].State);Assert.Empty(cache.Session.Drafts);Assert.Empty(cache.Composer);
            // Reflection bypasses Blazor's event dispatcher. Render its normal post-event
            // update after the production handler has completed, not its last stream preview.
            typeof(ComponentBase).GetMethod("StateHasChanged",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(components.Panel,[]);
            var markup=html.ToHtmlString();Assert.DoesNotContain("<img",markup);Assert.Contains("&lt;img",markup);
            if(Environment.GetEnvironmentVariable("WRITERAPP_BRAINSTORMING_CONTEXT_HTML") is {Length:>0} path)await File.WriteAllTextAsync(Path.Combine(Path.GetDirectoryName(path)!,"conversation-"+surface+".html"),markup);
        });
    }
    private sealed class Js: IJSRuntime,IJSObjectReference {
        private readonly LocalBrainstormingStore store=new(Path.Combine(Path.GetTempPath(),"BrainstormingWebUi",Guid.NewGuid().ToString()));private BrainstormingScope? scope;private string? account;public string Html="";public long Version;
        public ValueTask<T> InvokeAsync<T>(string name,object?[]? args)=>InvokeAsync<T>(name,default,args);
        public async ValueTask<T> InvokeAsync<T>(string name,CancellationToken ct,object?[]? args){args??=[];object? result=null;
            switch(name){case "import":result=this;break;case "snapshot":result=JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(new{html=Html,version=Version}),BrainstormingValidation.Json);break;
                case "qualityPlainText":case "tiptapEditor.brainstormingPlainText":result=(await new BrainstormingServerProjector().ProjectAsync(new(Guid.NewGuid(),Guid.NewGuid(),0,"",(string)args[0]!,"Html","v1",BrainstormingFreshness.Saved),ct)).PlainText;break;
                case "rememberIdentity":account=(string?)args[1];break;case "reviewIdentity":result=account;break;case "isOffline":result=false;break;
                case "setEditable":break;
                case "previewBrainstormingKeep":result=args[2];break;
                case "setContent":Html=(string)args[0]!;Version++;break;
                case "read":if(scope is not null && await store.ReadAsync(scope,ct) is {} cache)result=BrainstormingPersistence.Json(cache);break;
                case "write":var incoming=JsonSerializer.Deserialize<BrainstormingCache>((string)args[3]!,BrainstormingValidation.Json)!;scope=incoming.Session.Scope;var saved=await store.WriteAsync(incoming,(long)args[4]!, (Guid)args[5]!,ct);result=JsonSerializer.SerializeToElement(new{conflict=false,json=BrainstormingPersistence.Json(saved)});break;
                default:throw new InvalidOperationException("Unexpected test interop "+name);}
            return result is null?default!:(T)result;
        }
        public ValueTask DisposeAsync()=>ValueTask.CompletedTask;
    }
    [Fact]
    public async Task SharedStopInspectionAndExplicitRetryUseRealAuthenticatedTransport() {
        await using var f=new BrainstormingConversationTests.Fixture();await f.Start();f.Provider.Mode="wait";using var host=await Host(f);using var http=host.GetTestClient();http.BaseAddress=new("https://test.invalid/");http.DefaultRequestHeaders.Add("X-Test-Identity","author");
        var js=new Js();var auth=new AuthStateService(http,NullLogger<AuthStateService>.Instance,new(),new());await using var files=new WebBrainstormingStore(js);var sessions=new WebBrainstormingSessions(files,http,auth);var scope=await sessions.ScopeAsync(f.Data.Document,null);
        await sessions.Sessions.CreateAsync(scope);await sessions.Sessions.RememberPreparationAsync(scope,new(1,new(1,f.Target,null,[],[],[]),f.Context with{Scope=scope},[]));await sessions.MapAsync(scope);
        var components=new Components();using var services=new ServiceCollection().AddLogging().AddSingleton<IComponentActivator>(components).BuildServiceProvider();await using var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async()=>await renderer.RenderComponentAsync<BrainstormingConversationPanel>(ParameterView.FromDictionary(new Dictionary<string,object?>{{"Scope",scope},{"Ready",true},{"Store",sessions},{"Workflow",sessions.ConversationWorkflow}})));
        Set(components.Panel!,"_composer","Opening ideas\nNo writing changes.");var sending=renderer.Dispatcher.InvokeAsync(()=>Invoke(components.Panel!,"Send"));
        try {
            var deadline=DateTimeOffset.UtcNow.AddSeconds(5);while(f.Provider.Calls==0 && DateTimeOffset.UtcNow<deadline)await Task.Delay(5);Assert.Equal(1,f.Provider.Calls);
            await renderer.Dispatcher.InvokeAsync(()=>Invoke(components.Panel!,"Stop"));await sending.WaitAsync(TimeSpan.FromSeconds(5));
            var recovered=(await sessions.ReadAsync(scope))!;Assert.Null(recovered.PendingGeneration);
            Assert.Equal(BrainstormingGenerationState.Cancelled,recovered.LastGeneration!.State);
            Assert.Equal("Opening ideas\nNo writing changes.",recovered.Composer);Assert.Equal(2,recovered.Session.Turns.Count);
            var outcome=await sessions.Conversation.InspectAsync(recovered.LastGeneration.Send.Scope,recovered.LastGeneration.Send.OperationId);
            Assert.Equal(BrainstormingGenerationState.Cancelled,outcome!.State);
            await renderer.Dispatcher.InvokeAsync(()=>Invoke(components.Panel!,"Inspect"));Assert.Null((await sessions.ReadAsync(scope))!.PendingGeneration);Assert.Equal(1,f.Provider.Calls);
            f.Provider.Mode="complete";await renderer.Dispatcher.InvokeAsync(()=>Invoke(components.Panel!,"Send"));var cache=(await sessions.ReadAsync(scope))!;
            Assert.Equal(2,f.Provider.Calls);Assert.Equal(2,f.Provider.Charges);Assert.Equal(4,cache.Session.Turns.Count);Assert.Equal(2,cache.Session.Requests.Select(r=>r.OperationId).Distinct().Count());Assert.Empty(cache.Session.Drafts);
        }finally {components.Panel!.Suspend();await sending.WaitAsync(TimeSpan.FromSeconds(5));}
    }
    [Fact]
    public async Task WebProductionEditorSendGuardAndSharedSendHandlerReachAuthenticatedStreamAndPersistReload() {
        await using var f=new BrainstormingConversationTests.Fixture();await f.Start();using var host=await Host(f);using var http=host.GetTestClient();http.BaseAddress=new("https://test.invalid/");http.DefaultRequestHeaders.Add("X-Test-Identity","author");
        var js=new Js();var auth=new AuthStateService(http,NullLogger<AuthStateService>.Instance,new(),new());await using var files=new WebBrainstormingStore(js);var sessions=new WebBrainstormingSessions(files,http,auth);
        var scope=await sessions.ScopeAsync(f.Data.Document,null);await sessions.Sessions.CreateAsync(scope);await sessions.Sessions.RememberPreparationAsync(scope,new(1,new(1,f.Target,null,[],[],[f.Data.Section]),f.Context with {Scope=scope},[]));
        var editor=new DocumentEditor();var page=new PageEditor();Set(page,"_content",(await f.Db.Pages.SingleAsync()).Content);Set(editor,"DocumentId",f.Data.Document);Set(editor,"_pageEditor",page);Set(editor,"_activePage",new PageDto(f.Data.Page,f.Data.Document,f.Data.Section,"Page","",0,DateTimeOffset.UtcNow,DateTimeOffset.UtcNow));Set(editor,"BrainstormSessions",sessions);Set(editor,"BrainstormContext",new WebBrainstormingContext(http,new(http,auth),sessions,new(js)));Set(editor,"_brainstormContextScope",scope);
        var untouched=await page.GetContentAsync();typeof(DocumentEditor).GetMethod("ToggleBrainstorm",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(editor,[]);
        Assert.Equal(untouched,await page.GetContentAsync());Assert.Same(page,typeof(DocumentEditor).GetField("_pageEditor",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(editor));
        var createdScope=await (Task<BrainstormingScope>)typeof(DocumentEditor).GetMethod("CreateBrainstormScope",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(editor,[])!;Assert.Equal(f.Data.Document,createdScope.DocumentId);Assert.NotEqual(scope.SessionId,createdScope.SessionId);
        await ExercisePanel(sessions,scope,sessions.ConversationWorkflow,()=>Invoke(editor,"BeforeBrainstormingSend"),"web");Assert.Equal(1,f.Provider.Calls);
        Set(page,"_content","<p>New pending writing</p>");await Assert.ThrowsAsync<InvalidDataException>(()=>Invoke(editor,"BeforeBrainstormingSend"));Assert.Equal(1,f.Provider.Calls);
        await (Task)typeof(DocumentEditor).GetMethod("NavigateBrainstormTarget",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(editor,[f.Target with{DocumentId=Guid.NewGuid()}])!;
        Assert.Equal("<p>New pending writing</p>",await page.GetContentAsync());Assert.Contains("pending writing is retained",(string)typeof(DocumentEditor).GetField("_brainstormNavigationError",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(editor)!);
        var reopened=new WebBrainstormingSessions(files,http,auth);Assert.Equal(2,(await reopened.ReadAsync(scope))!.Session.Turns.Count);
    }
    private sealed class Identity(string accountId="author"):IDeviceIdentityClient {public bool IsConfigured=>true;public Task<DeviceAccessToken?> AcquireAsync(bool interactive,CancellationToken ct)=>Task.FromResult<DeviceAccessToken?>(new("synthetic-test",DateTimeOffset.UtcNow.AddHours(1),"Author",accountId));public Task SignOutAsync()=>Task.CompletedTask;}
    private sealed class Available:IBrainstormingCapabilityService {public Task<BrainstormingCapabilities> ReadAsync(CancellationToken ct=default)=>Task.FromResult(BrainstormingCapabilities.Current);}
    private sealed class SyncPlans:IUserEntitlementStore {public Task<UserEntitlement> GetOrCreateAsync(string user,CancellationToken ct=default)=>Task.FromResult(new UserEntitlement{UserId=user,PlanKey="standard",SubscriptionStatus="active"});}
    #pragma warning disable BL0006 // Inspect compiled production parent parameters, rather than supplying a correct string by hand.
    private static string ProductionSyncDestination(LocalEditorSession session,DeviceBrainstormingSessions sessions)
    {
        var parent=new DocumentWorkspace();Set(parent,"_session",session);Set(parent,"BrainstormSessions",sessions);Set(parent,"_brainstormVisited",true);
        using var outer=new RenderTreeBuilder();typeof(DocumentWorkspace).GetMethod("BuildRenderTree",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(parent,[outer]);
        var frames=outer.GetFrames();var fragment=Assert.Single(frames.Array.Take(frames.Count),f=>f.AttributeName=="Brainstorming").AttributeValue as RenderFragment;
        Assert.NotNull(fragment);using var child=new RenderTreeBuilder();fragment(child);var parameters=child.GetFrames();
        var writingVersion=Assert.IsType<Func<string?>>(Assert.Single(parameters.Array.Take(parameters.Count),f=>f.AttributeName=="CurrentWritingVersion").AttributeValue);
        Assert.Equal(DeviceSyncMapping.Fingerprint(session.Snapshot()),writingVersion());
        return Assert.IsType<string>(Assert.Single(parameters.Array.Take(parameters.Count),f=>f.AttributeName=="SynchronizationDestination").AttributeValue);
    }
    #pragma warning restore BL0006
    [Fact]
    public async Task DesktopParentBindsActualBackendDestinationForPreparationStatus()
    {
        var root=Path.Combine(Path.GetTempPath(),"BrainstormingDestination",Guid.NewGuid().ToString());var repository=new LocalDocumentRepository(new FileLocalDocumentStore(Path.Combine(root,"documents")));
        var document=await repository.CreateAsync("Destination");var backend=new Uri("https://test.invalid/");var account=new DeviceAccountService(new Identity());await account.SignInAsync();
        var sessions=new DeviceBrainstormingSessions(new LocalBrainstormingStore(Path.Combine(root,"sessions")),account,new("Test",backend),new DeviceConnectivity(),repository,new HttpClient{BaseAddress=backend});
        Assert.Equal(backend.AbsoluteUri,ProductionSyncDestination(new LocalEditorSession(repository,document),sessions));
    }

    [Theory][InlineData("signout")][InlineData("reauthenticate")][InlineData("revoke")][InlineData("reject")]
    public async Task MountedDesktopPreparationAndSendSurviveRealAuthenticatedHandlerTokenAcquisition(string invalidation) {
        await using var f=new BrainstormingConversationTests.Fixture();await f.Start(true);using var host=await Host(f);
        f.Provider.Wire=BrainstormingPersistence.Json(new BrainstormingAnswer("An editable scene is ready.",1,BrainstormingIntent.Draft,new("Anna lifted the ice core from the drill.","Arctic field research",[]),[],[]));
        var account=new DeviceAccountService(new Identity("author.native-tenant"));await account.SignInAsync();var backend=new Uri("https://test.invalid/");
        using var http=new HttpClient(new DeviceAuthenticatedHandler(account,backend){InnerHandler=host.GetTestServer().CreateHandler()}){BaseAddress=backend};
        http.DefaultRequestHeaders.Add("X-Test-Identity","author"); // Synthetic server identity; real device bearer/token handler is retained.
        var root=Path.Combine(Path.GetTempPath(),"BrainstormingAuthenticatedUi",Guid.NewGuid().ToString());var files=new FileLocalDocumentStore(Path.Combine(root,"documents"));var repository=new LocalDocumentRepository(files);
        var initial=await repository.CreateAsync("Retained manuscript");var section=initial.Sections[0];var page=section.Pages[0];
        var local=await files.ApplySyncAsync(initial with{ServerDocumentId=f.Data.Document,ServerVersion="v1",SyncState=LocalSyncState.Synced,
            Sections=[section with{ServerSectionId=f.Data.Section,Title="Section",Pages=[page with{ServerPageId=f.Data.Page,Title="",Content=(await f.Db.Pages.SingleAsync()).Content}]}]},initial.LocalRevision,default);
        var sessions=new DeviceBrainstormingSessions(new LocalBrainstormingStore(Path.Combine(root,"sessions")),account,new("Windows authenticated test",backend),new DeviceConnectivity(),repository,http);
        var js=new Js{Html=local.Sections[0].Pages[0].Content};var context=new DeviceBrainstormingContext(repository,new(new(http),new(Path.Combine(root,"canon")),repository,account,new DeviceConnectivity(),new("Windows test",backend)),account,sessions,new(js));
        var editorSession=new LocalEditorSession(repository,local);var richEditor=new DeviceTextEditor();Set(richEditor,"Content",js.Html);Set(richEditor,"Format",LocalContentFormat.Html);Set(richEditor,"_editor",js);Set(richEditor,"ContentChanged",EventCallback.Factory.Create<string>(new object(),text=>editorSession.Edit(page.PageId,text)));
        var destination=ProductionSyncDestination(editorSession,sessions);Assert.Equal(backend.AbsoluteUri,destination);
        Directory.CreateDirectory(Path.Combine(root,"sync"));var journal=new DeviceSyncJournal(Path.Combine(root,"sync"));var key=journal.AccountKey(backend.AbsoluteUri,"author");await journal.BindAsync(local.DocumentId,key,default);
        await journal.SaveAsync(key,new SyncJournal{Entries=[new SyncEntry{LocalId=local.DocumentId,ServerId=f.Data.Document,Version="v1",BaseFingerprint=DeviceSyncMapping.WritingFingerprint(local),BaseContentFingerprint=DeviceSyncMapping.ContentFingerprint(local)}]},default);
        using var sync=new DeviceSyncEngine(files,journal,new DeviceSyncApi(http),account,new DeviceConnectivity(),repository,new("Windows test",backend));
        sync.Start(); // The production sync panel initializes account state before authenticated sync.
        using var autosave=new LocalAutosaveCoordinator(editorSession,new LocalRecoveryStore(Path.Combine(root,"recovery")),()=>Task.CompletedTask);
        var documentHost=new BrainstormingKeepHostTests.MountedDevice();Set(documentHost,"DocumentId",local.DocumentId);Set(documentHost,"_session",editorSession);Set(documentHost,"_editor",richEditor);Set(documentHost,"_selectedPage",page.PageId);Set(documentHost,"BrainstormSessions",sessions);Set(documentHost,"BrainstormContext",context);Set(documentHost,"Sync",sync);Set(documentHost,"Documents",repository);Set(documentHost,"_autosave",autosave);
        var history=new LocalAiStore(Path.Combine(root,"history"));Set(documentHost,"AiHistory",history);Set(documentHost,"BrainstormKeeps",new LocalBrainstormingKeeps(repository,history,sessions,account,new("Windows test",backend)));Set(documentHost,"BrainstormKeepJS",js);
        using var mountedHost=new BrainstormingKeepHostTests.Mount(new ServiceCollection().BuildServiceProvider());await mountedHost.Dispatcher.InvokeAsync(()=>mountedHost.Attach(documentHost));
        js.Html="<p>New pending Åsa 日本語 🧭 manuscript</p>";js.Version=1;
        int invalidations=0;Action changed=()=>invalidations++;sessions.Changed+=changed;
        BrainstormingScope? selected=null;var components=new Components();using var services=new ServiceCollection().AddLogging().AddSingleton<IComponentActivator>(components).BuildServiceProvider();await using var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async()=>{
            var html=await renderer.RenderComponentAsync<BrainstormingWorkspace>(ParameterView.FromDictionary(new Dictionary<string,object?>{
                {"Active",true},{"DocumentId",local.DocumentId},{"SectionId",section.SectionId},{"Store",sessions},{"Sessions",sessions.Sessions},{"ReviewIdentity",sessions},{"Capabilities",new Available()},{"Workflow",sessions.ConversationWorkflow},{"SynchronizationDestination",destination},
                {"ScopeFactory",(Func<Task<BrainstormingScope>>)(()=>sessions.ScopeAsync(local.DocumentId))},
                {"SessionChanged",EventCallback.Factory.Create<BrainstormingScope?>(new object(),scope=>{selected=scope;typeof(DocumentWorkspace).GetMethod("SelectBrainstormSession",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(documentHost,[scope]);})},
                {"BeforeSend",(Func<Task>)(()=>Invoke(documentHost,"BeforeBrainstormingSendAsync"))},
                {"CurrentWritingVersion",(Func<string?>)(()=>DeviceSyncMapping.Fingerprint(editorSession.Snapshot()))},
                {"PrepareServer",(Func<BrainstormingTargetQuery,IReadOnlyList<BrainstormingContextChoice>,IReadOnlyList<Guid>,int,Task<BrainstormingPreparation>>)((q,c,s,b)=>mountedHost.Dispatcher.InvokeAsync(()=>(Task<BrainstormingPreparation>)typeof(DocumentWorkspace).GetMethod("PrepareServerBrainstormingContextAsync",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(documentHost,[q,c,s,b])!))}}));
            typeof(ComponentBase).GetMethod("StateHasChanged",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(components.Workspace,[]);
            var original=selected!;var panel=components.Panel!;var opened=DateTimeOffset.UtcNow.AddSeconds(5);
            while((bool)typeof(BrainstormingConversationPanel).GetField("_opening",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(panel)! && DateTimeOffset.UtcNow<opened)await Task.Delay(5);
            Assert.False((bool)typeof(BrainstormingConversationPanel).GetField("_opening",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(panel)!);
            Set(panel,"_composer","Explore Åsa's arrival\nKeep this message");await Invoke(panel,"SaveComposer");
            await (Task)typeof(BrainstormingWorkspace).GetMethod("OnAfterRenderAsync",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(components.Workspace,[false])!;
            Assert.DoesNotContain("Save, sync &amp; prepare context",html.ToHtmlString());Assert.Equal(0,f.Provider.Calls);
            Assert.Equal(js.Html,(await repository.LoadAsync(local.DocumentId))!.Sections[0].Pages[0].Content);
            typeof(ComponentBase).GetMethod("StateHasChanged",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(components.Workspace,[]);
            Assert.Equal(original,selected);Assert.Same(panel,components.Panel);Assert.True(panel.Ready,html.ToHtmlString()+"\n"+sync.Message);Assert.DoesNotContain("Start a conversation",html.ToHtmlString());
            Assert.DoesNotContain("Confirm Save, sync",html.ToHtmlString());
            Assert.Equal("Explore Åsa's arrival\nKeep this message",(await sessions.ReadAsync(original))!.Composer);Assert.Equal(0,f.Provider.Calls);
            await Invoke(panel,"Send");Assert.Equal(original,selected);Assert.Equal(1,f.Provider.Calls);Assert.Equal(2,(await sessions.ReadAsync(original))!.Session.Turns.Count);
            Assert.Equal(js.Html,(await repository.LoadAsync(local.DocumentId))!.Sections[0].Pages[0].Content);
            Assert.Equal(js.Html,(await f.Db.Pages.AsNoTracking().SingleAsync()).Content);Assert.False(editorSession.IsDirty);Assert.Equal(LocalSyncState.Synced,editorSession.Document.SyncState);
            Assert.Equal(0,invalidations);
            var firstCache=(await sessions.ReadAsync(original))!;var firstDraft=Assert.Single(firstCache.Session.Drafts);var firstTarget=firstCache.Session.Targets.Single(t=>t.Id==firstDraft.TargetId);
            var keep=BrainstormingKeepContract.Review(original,firstDraft,firstTarget,LocalBrainstormingWriting.Capture(editorSession.Document));
            await mountedHost.Dispatcher.InvokeAsync(()=>(Task<BrainstormingCache>)typeof(DocumentWorkspace).GetMethod("ApplyBrainstormKeepAsync",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(documentHost,[keep])!);
            Assert.Contains(firstDraft.PlainText,editorSession.Document.Sections[0].Pages[0].Content);
            Set(panel,"_composer","Add the approaching patrol and Anna's dialogue to the scene.");await Invoke(panel,"SaveComposer");
            await Invoke(components.Workspace!,"OnParametersSetAsync");typeof(ComponentBase).GetMethod("StateHasChanged",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(components.Workspace,[]);
            Assert.False(panel.Ready);Assert.Contains("Story context needs review",html.ToHtmlString());Assert.DoesNotContain("Prepare context for sending",html.ToHtmlString());
            await Invoke(panel,"Send");Assert.Equal(1,f.Provider.Calls);Assert.Equal(2,(await sessions.ReadAsync(original))!.Session.Turns.Count);
            await Invoke(panel,"PrepareForSend");typeof(ComponentBase).GetMethod("StateHasChanged",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(components.Workspace,[]);
            Assert.True(panel.Ready,html.ToHtmlString()+"\n"+sync.Message);Assert.Equal(original,selected);Assert.Same(panel,components.Panel);
            Assert.False((await sessions.ReadAsync(original))!.DraftReview!.UseForNextRequest);
            f.Provider.Wire=BrainstormingPersistence.Json(new BrainstormingAnswer("A new proposal uses the current manuscript scene.",1,BrainstormingIntent.Draft,new("A distant patrol approached. ‘Careful,’ Anna told the team.","Continue the kept ice research scene",[]),[],[]));
            await Invoke(panel,"Send");var second=(await sessions.ReadAsync(original))!;
            Assert.True(f.Provider.Calls==2,html.ToHtmlString());Assert.Equal(4,second.Session.Turns.Count);Assert.Equal(2,second.Session.Drafts.Count);Assert.Null(second.Session.Drafts[^1].ParentDraftId);Assert.Empty(second.Composer);
            Assert.Equal(BrainstormingPersistence.Json(firstDraft),BrainstormingPersistence.Json(second.Session.Drafts[0]));Assert.NotEqual(firstDraft.TargetId,second.Session.Drafts[^1].TargetId);
            Assert.Single(await history.HistoryAsync(local.DocumentId));Assert.Equal(0,invalidations);
            Assert.Equal(keep.AfterHtml,editorSession.Document.Sections[0].Pages[0].Content);Assert.Equal(keep.AfterHtml,(await f.Db.Pages.AsNoTracking().SingleAsync()).Content);
            switch(invalidation) {
                case "signout":await account.SignOutAsync();break;
                case "reauthenticate":await account.SignInAsync();break;
                case "reject":account.RejectSession(account.Generation);break;
                case "revoke":
                    (await f.Db.Documents.SingleAsync()).OwnerUserId="other";await f.Db.SaveChangesAsync();
                    var server=(await sessions.ReadAsync(original))!.Mapping!.Server;
                    await Assert.ThrowsAsync<HttpRequestException>(()=>sessions.Conversation.ReadStoryAsync(server));
                    await account.GetTokenAsync(default); // A later same-account acquisition must not repeat access-loss invalidation.
                    break;
            }
            Assert.Null(selected);Assert.Equal(1,invalidations);Assert.Contains("Start a conversation",html.ToHtmlString());
        });
        sessions.Changed-=changed;
    }
    [Fact]
    public async Task DeviceProductionServerReviewAndSendHandlersVerifyExplicitMappingsAndRetainPendingWriting() {
        await using var f=new BrainstormingConversationTests.Fixture();await f.Start();using var host=await Host(f);using var http=host.GetTestClient();http.BaseAddress=new("https://test.invalid/");http.DefaultRequestHeaders.Add("X-Test-Identity","author");
        var root=Path.Combine(Path.GetTempPath(),"BrainstormingNativeUi",Guid.NewGuid().ToString());var repository=new LocalDocumentRepository(new FileLocalDocumentStore(Path.Combine(root,"documents")));var initial=await repository.CreateAsync("Standalone manuscript");var section=initial.Sections[0];var page=section.Pages[0];
        var local=initial with {ServerDocumentId=f.Data.Document,ServerVersion="v1",SyncState=LocalSyncState.Synced,Sections=[section with {ServerSectionId=f.Data.Section,Title="Section",Pages=[page with {ServerPageId=f.Data.Page,Title="",Content=(await f.Db.Pages.SingleAsync()).Content}]}]};local=await new FileLocalDocumentStore(Path.Combine(root,"documents")).ApplySyncAsync(local,initial.LocalRevision,default);
        var account=new DeviceAccountService(new Identity());await account.SignInAsync();var network=new DeviceConnectivity();var options=new DeviceHostOptions("Windows test fixture",http.BaseAddress);var files=new LocalBrainstormingStore(Path.Combine(root,"sessions"));var sessions=new DeviceBrainstormingSessions(files,account,options,network,repository,http);var js=new Js {Html=local.Sections[0].Pages[0].Content};
        var context=new DeviceBrainstormingContext(repository,new(new(http),new(Path.Combine(root,"canon")),repository,account,network,options),account,sessions,new(js));var localEditor=new LocalEditorSession(repository,local);var richEditor=new DeviceTextEditor();Set(richEditor,"Content",js.Html);Set(richEditor,"Format",LocalContentFormat.Html);Set(richEditor,"_editor",js);Set(richEditor,"ContentChanged",EventCallback.Factory.Create<string>(new object(),html=>localEditor.Edit(page.PageId,html)));
        var workspace=new DocumentWorkspace();Set(workspace,"DocumentId",local.DocumentId);Set(workspace,"_session",localEditor);Set(workspace,"_editor",richEditor);Set(workspace,"_selectedPage",page.PageId);Set(workspace,"BrainstormSessions",sessions);Set(workspace,"BrainstormContext",context);
        var prepared=await (Task<BrainstormingPreparation>)typeof(DocumentWorkspace).GetMethod("PrepareServerBrainstormingContextAsync",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(workspace,[new BrainstormingTargetQuery(SectionId:section.SectionId),Array.Empty<BrainstormingContextChoice>(),Array.Empty<Guid>(),60_000])!;
        var scope=prepared.Context!.Scope;Assert.Equal(page.PageId,prepared.Resolution.Target!.PageId);Assert.Empty(prepared.Context.Policy!.Limitation);
        await ExercisePanel(sessions,scope,sessions.ConversationWorkflow,()=>Invoke(workspace,"BeforeBrainstormingSendAsync"),"device");Assert.Equal(1,f.Provider.Calls);Assert.Equal(2,(await new LocalBrainstormingStore(Path.Combine(root,"sessions")).ReadAsync(scope))!.Session.Turns.Count);
        localEditor.Edit(page.PageId,"<p>Keep unsaved local prose</p>");await Assert.ThrowsAsync<InvalidDataException>(()=>Invoke(workspace,"BeforeBrainstormingSendAsync"));Assert.Contains("unsaved",localEditor.Snapshot().Sections[0].Pages[0].Content);Assert.Equal(js.Html,(await repository.LoadAsync(local.DocumentId))!.Sections[0].Pages[0].Content);
        js.Html=localEditor.Snapshot().Sections[0].Pages[0].Content;await Invoke(workspace,"ToggleBrainstormAsync");await Invoke(workspace,"ToggleBrainstormAsync");Assert.True(localEditor.IsDirty);Assert.Contains("unsaved",localEditor.Snapshot().Sections[0].Pages[0].Content);
        Assert.Same(richEditor,typeof(DocumentWorkspace).GetField("_editor",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(workspace));Assert.DoesNotContain("unsaved",(await repository.LoadAsync(local.DocumentId))!.Sections[0].Pages[0].Content);
        var createdScope=await (Task<BrainstormingScope>)typeof(DocumentWorkspace).GetMethod("CreateBrainstormScopeAsync",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(workspace,[])!;Assert.Equal(local.DocumentId,createdScope.DocumentId);Assert.NotEqual(scope.SessionId,createdScope.SessionId);
        await (Task)typeof(DocumentWorkspace).GetMethod("NavigateBrainstormTargetAsync",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(workspace,[prepared.Resolution.Target!])!;
        Assert.True(localEditor.IsDirty);Assert.Same(richEditor,typeof(DocumentWorkspace).GetField("_editor",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(workspace));
        await (Task)typeof(DocumentWorkspace).GetMethod("NavigateBrainstormTargetAsync",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(workspace,[prepared.Resolution.Target! with{DocumentId=Guid.NewGuid()}])!;
        Assert.Contains("another manuscript",(string)typeof(DocumentWorkspace).GetField("_brainstormNavigationError",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(workspace)!);Assert.Contains("unsaved",localEditor.Snapshot().Sections[0].Pages[0].Content);
    }
}
