using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;
using WriterApp.Application.Security;
using WriterApp.Client.Pages;
using WriterApp.Client.Services;
using WriterApp.Controllers;
using WriterApp.Data;
using WriterApp.Device.Shared.Components;
using WriterApp.Device.Shared.Pages;
using WriterApp.Device.Shared.Services;
using WriterApp.Device.Shared.Storage;
using WriterApp.Shared.Brainstorming;
using WriterApp.UI.Shared;
using Xunit;

namespace WriterApp.Tests;

public sealed class BrainstormingKeepHostTests
{
    private sealed class Auth(IOptionsMonitor<AuthenticationSchemeOptions> options,ILoggerFactory logger,System.Text.Encodings.Web.UrlEncoder encoder):AuthenticationHandler<AuthenticationSchemeOptions>(options,logger,encoder) {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()=>Task.FromResult(!Request.Headers.TryGetValue("X-Test-Identity",out var id)?AuthenticateResult.NoResult():AuthenticateResult.Success(new(new(new ClaimsIdentity([new Claim("oid",id.ToString())],"test")),"test")));
    }
    internal static async Task<IHost> Host(BrainstormingConversationTests.Fixture f,bool conversation=false,WriterApp.AI.Abstractions.IAiRouter? speech=null) {
        var host=new HostBuilder().ConfigureWebHost(web=>web.UseTestServer().ConfigureServices(s=>{
            s.AddAuthentication("test").AddScheme<AuthenticationSchemeOptions,Auth>("test",_=>{});s.AddAuthorization();s.AddSingleton<IUserIdResolver,UserIdResolver>();s.AddScoped(_=>new AppDbContext(f.Data.Options));s.AddControllers().AddApplicationPart(typeof(BrainstormingKeepsController).Assembly);
            if(conversation){s.AddSingleton<WriterApp.AI.Abstractions.IAiRouter>(speech??f.Provider);s.AddSingleton<WriterApp.AI.Abstractions.IAiUsagePolicy>(f.Provider);s.AddSingleton<WriterApp.Application.Usage.IAiQuotaService>(f.Provider);s.AddSingleton<WriterApp.Application.Usage.IUsageMeter>(f.Provider);s.AddSingleton<WriterApp.Application.Continuity.IBibleStore>(f.Provider);s.AddSingleton<WriterApp.Application.Subscriptions.IEntitlementService>(f.Provider);s.AddSingleton(f.Stops);s.Configure<WriterApp.AI.Abstractions.WriterAiOptions>(o=>{o.Enabled=true;o.Streaming.Enabled=true;o.Speech.Enabled=speech is not null;o.Speech.Model="gpt-4o-mini-transcribe";});}
        }).Configure(app=>{app.UseRouting();app.UseAuthentication();app.UseAuthorization();app.UseEndpoints(e=>{e.MapControllers();e.MapGet("api/auth/me",()=>new AuthMeDto{IsAuthenticated=true,UserId="author"});});})).Build();await host.StartAsync();return host;
    }
    internal sealed class Js:IJSRuntime,IJSObjectReference {
        private readonly LocalBrainstormingStore files=new(Path.Combine(Path.GetTempPath(),"P07-web-interop",Guid.NewGuid().ToString()));private BrainstormingScope? scope;public string Html="<p></p>";
        public ValueTask<T> InvokeAsync<T>(string name,object?[]? args)=>InvokeAsync<T>(name,default,args);
        public async ValueTask<T> InvokeAsync<T>(string name,CancellationToken ct,object?[]? args){args??=[];object? value=null;
            switch(name){
                case "import":value=this;break;case "isOffline":value=false;break;case "rememberIdentity":break;
                case "read":value=scope is null?null:BrainstormingPersistence.Json(await files.ReadAsync(scope,ct));if(value is "null")value=null;break;
                case "write":var next=JsonSerializer.Deserialize<BrainstormingCache>((string)args[3]!,BrainstormingValidation.Json)!;scope=next.Session.Scope;value=JsonSerializer.SerializeToElement(new{conflict=false,json=BrainstormingPersistence.Json(await files.WriteAsync(next,(long)args[4]!, (Guid)args[5]!,ct))});break;
                case "tiptapEditor.previewBrainstormingKeep":case "previewBrainstormingKeep":value=args[2];break;
                case "snapshot":value=JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(new{html=Html,version=0}));break;
                case "setEditable":break;default:throw new InvalidOperationException("Unexpected interop fixture: "+name);
            }return value is null?default!:(T)value;
        }
        public ValueTask DisposeAsync()=>ValueTask.CompletedTask;
    }
    private sealed class Components:IComponentActivator {public BrainstormingProposalPreview Preview=default!;public IComponent CreateInstance(Type type){var c=(IComponent)Activator.CreateInstance(type)!;if(c is BrainstormingProposalPreview p)Preview=p;return c;}}
    internal sealed class MountedDevice:DocumentWorkspace,IDisposable {protected override void BuildRenderTree(RenderTreeBuilder b){} void IDisposable.Dispose(){} }
    internal sealed class MountedWeb:DocumentEditor {public int Reloads;protected override Task ReloadTranslatedDocumentAsync(){Reloads++;return Task.CompletedTask;}}
    #pragma warning disable BL0006 // Test-only renderer mounts the production host handlers without a native window.
    internal sealed class Mount(IServiceProvider services):Renderer(services,NullLoggerFactory.Instance) {
        public override Dispatcher Dispatcher { get; }=Dispatcher.CreateDefault();public int Attach(IComponent c)=>AssignRootComponentId(c);
        protected override void HandleException(Exception e)=>throw e;protected override Task UpdateDisplayAsync(in RenderBatch batch)=>Task.CompletedTask;
    }
    private static MemberInfo Member(object value,string name){for(var t=value.GetType();t is not null;t=t.BaseType){var f=t.GetMember(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.DeclaredOnly).FirstOrDefault();if(f is not null)return f;}throw new Exception(name);}
    internal static void Set(object value,string name,object? data){switch(Member(value,name)){case FieldInfo f:f.SetValue(value,data);break;case PropertyInfo p:p.SetValue(value,data);break;}}
    internal static Task<T> Call<T>(object value,string name,params object?[] args)=>(Task<T>)((MethodInfo)Member(value,name)).Invoke(value,args)!;
    internal static Task Call(object value,string name,params object?[] args)=>(Task)((MethodInfo)Member(value,name)).Invoke(value,args)!;
    [Fact]
    public async Task AuthenticatedKeepRouteUsesOwnedReceiptsAndRejectsForeignInspection()
    {
        await using var f=new BrainstormingConversationTests.Fixture();var r=await BrainstormingKeepTests.Server(f,"insert");using var host=await Host(f);using var http=host.GetTestClient();http.BaseAddress=new("https://test.invalid/");var path=$"api/brainstorming/{r.Scope.SessionId}/keeps/{r.OperationId}?documentId={r.Scope.DocumentId}";
        Assert.Equal(HttpStatusCode.Unauthorized,(await http.PostAsJsonAsync(path,r,BrainstormingValidation.Json)).StatusCode);http.DefaultRequestHeaders.Add("X-Test-Identity","other");Assert.Equal(HttpStatusCode.NotFound,(await http.PostAsJsonAsync(path,r,BrainstormingValidation.Json)).StatusCode);
        http.DefaultRequestHeaders.Remove("X-Test-Identity");http.DefaultRequestHeaders.Add("X-Test-Identity","author");var adapter=new BrainstormingKeepHttp(http,_=>new(()=>{}));Assert.Equal(BrainstormingApplicationState.Failed,(await adapter.InspectAsync(r)).State);var receipt=await adapter.ApplyAsync(r);Assert.Equal(BrainstormingApplicationState.Committed,receipt.State);Assert.Equal(receipt,await adapter.InspectAsync(r));
        http.DefaultRequestHeaders.Remove("X-Test-Identity");http.DefaultRequestHeaders.Add("X-Test-Identity","other");Assert.Equal(HttpStatusCode.NotFound,(await http.GetAsync(path)).StatusCode);
    }
    [Theory][InlineData("Web",false)][InlineData("Device",false)][InlineData("Web",true)][InlineData("Device",true)]
    public async Task SharedReviewAndApproveReachBothProductionHostHandlers(string surface,bool spoken)
    {
        var js=new Js();var components=new Components();using var services=new ServiceCollection().AddLogging().AddSingleton<IBrainstormingSpeechOutput,UnsupportedBrainstormingSpeechOutput>().AddSingleton<BrainstormingVoiceCoordinator>().AddSingleton<IComponentActivator>(components).BuildServiceProvider();await using var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
        BrainstormingCache cache;BrainstormingSessionService sessions;Func<Guid,bool,string,Task<BrainstormingKeepReview>> prepare;Func<BrainstormingKeepReview,Task<BrainstormingCache>> apply;Func<Task> verify;
        BrainstormingConversationTests.Fixture? fixture=null;IHost? server=null;HttpClient? http=null;Mount? mount=null;LocalAutosaveCoordinator? autosave=null;
        if(surface=="Web") {
            fixture=new();var r=await BrainstormingKeepTests.Server(fixture,"insert");server=await Host(fixture);http=server.GetTestClient();http.BaseAddress=new("https://test.invalid/");http.DefaultRequestHeaders.Add("X-Test-Identity","author");var auth=new AuthStateService(http,NullLogger<AuthStateService>.Instance,new(),new());var files=new WebBrainstormingStore(js);var adapter=new WebBrainstormingSessions(files,http,auth);
            var cloud=(await new WriterApp.Application.AI.BrainstormingSessions(fixture.Db,"author",fixture.Data.Backend).ReadAsync(r.Scope.DocumentId,r.Scope.SessionId,default))!;var local=r.Scope with {Space=BrainstormingIdentitySpace.Local};
            cache=await adapter.WriteAsync(new(1,1,BrainstormingPersistence.ReScope(cloud,local),"",new(1,local,r.Scope,1,DateTimeOffset.UtcNow),null,new(1,Guid.NewGuid(),r.Scope,cloud.Revision,BrainstormingPersistence.Hash(cloud),DateTimeOffset.UtcNow),[],[]),0,Guid.NewGuid());sessions=adapter.Sessions;
            var editor=new MountedWeb();Set(editor,"DocumentId",local.DocumentId);Set(editor,"_brainstormContextScope",local);Set(editor,"BrainstormSessions",adapter);Set(editor,"BrainstormContext",new WebBrainstormingContext(http,null!,adapter,new(js)));Set(editor,"JSRuntime",js);
            prepare=(id,replace,title)=>Call<BrainstormingKeepReview>(editor,"PrepareBrainstormKeep",id,replace,title);apply=review=>Call<BrainstormingCache>(editor,"ApplyBrainstormKeep",review);
            verify=async()=>{Assert.Equal(1,editor.Reloads);Assert.Single(await fixture.Db.AiActionHistoryEntries.AsNoTracking().ToArrayAsync());};
        }else {
            var root=Path.Combine(Path.GetTempPath(),"P07-mounted-device",Guid.NewGuid().ToString());var docs=new LocalDocumentRepository(new FileLocalDocumentStore(Path.Combine(root,"docs")));var d=await docs.CreateAsync("Book");var account=new DeviceAccountService(new UnconfiguredDeviceIdentityClient());var host=new DeviceHostOptions("Fixture",new("https://test.invalid/"));http=new(){BaseAddress=host.ApiBaseAddress};var network=new DeviceConnectivity();network.SetOnline(false);var files=new LocalBrainstormingStore(Path.Combine(root,"sessions"));var adapter=new DeviceBrainstormingSessions(files,account,host,network,docs,http);var scope=await adapter.ScopeAsync(d.DocumentId);var conversation=BrainstormingKeepTests.LocalSession(d,scope,false);
            cache=await adapter.WriteAsync(new(1,1,conversation,"",null,null,null,[],[]),0,Guid.NewGuid());sessions=adapter.Sessions;var history=new LocalAiStore(Path.Combine(root,"history"));var keeps=new LocalBrainstormingKeeps(docs,history,adapter,account,host);var session=new LocalEditorSession(docs,d);
            var editor=new MountedDevice();mount=new(services);mount.Attach(editor);autosave=new(session,new LocalRecoveryStore(Path.Combine(root,"recovery")),()=>Task.CompletedTask);var text=new DeviceTextEditor();Set(text,"_editor",js);
            Set(editor,"DocumentId",d.DocumentId);Set(editor,"_session",session);Set(editor,"_selectedPage",d.Sections[0].Pages[0].PageId);Set(editor,"_editor",text);Set(editor,"_autosave",autosave);Set(editor,"Documents",docs);Set(editor,"AiHistory",history);Set(editor,"BrainstormSessions",adapter);Set(editor,"BrainstormKeeps",keeps);Set(editor,"BrainstormKeepJS",js);Set(editor,"_brainstormContextScope",scope);
            prepare=(id,replace,title)=>mount.Dispatcher.InvokeAsync(()=>Call<BrainstormingKeepReview>(editor,"PrepareBrainstormKeepAsync",id,replace,title));
            apply=review=>mount.Dispatcher.InvokeAsync(async()=>{Set(editor,"_editor",null);return await Call<BrainstormingCache>(editor,"ApplyBrainstormKeepAsync",review);});
            verify=async()=>{Assert.Equal("Applied",(await history.HistoryAsync(d.DocumentId)).Single().Status);Assert.Equal(conversation.Drafts[0].PlainText,BrainstormingDraftContent.PlainText((await docs.LoadAsync(d.DocumentId))!.Sections[0].Pages[0].Content));};
        }
        try {await renderer.Dispatcher.InvokeAsync(async()=>{
            var html=await renderer.RenderComponentAsync<BrainstormingProposalPreview>(ParameterView.FromDictionary(new Dictionary<string,object?>{{"Cache",cache},{"DocumentId",cache.Session.Scope.DocumentId},{"ActiveSectionId",cache.Session.Targets.Single(t=>t.Id==BrainstormingDrafts.Candidate(cache)!.TargetId).SectionId},{"Sessions",sessions},{"PrepareKeep",prepare},{"ApplyKeep",apply},{"EditorHost",surface}}));
            await Call(components.Preview,"ReviewKeep",false);((MethodInfo)Member(components.Preview,"StateHasChanged")).Invoke(components.Preview,null);Assert.Contains("Add here",html.ToHtmlString());Assert.Contains("Hon tvivlade",html.ToHtmlString());
            if(Environment.GetEnvironmentVariable("WRITERAPP_BRAINSTORMING_CONTEXT_HTML") is {Length:>0} path)await File.WriteAllTextAsync(Path.Combine(Path.GetDirectoryName(path)!,"keep-review-"+surface.ToLowerInvariant()+".html"),html.ToHtmlString());
            if(spoken){var voice=services.GetRequiredService<BrainstormingVoiceCoordinator>();var tickets=voice.CaptureReviews(cache.Session.Scope);Assert.Single(tickets);Assert.True(await voice.ApproveAsync(cache.Session.Scope,BrainstormingVoiceCommands.Classify("Keep this version."),BrainstormingRecognitionQuality.Clear,tickets));Assert.False(await voice.ApproveAsync(cache.Session.Scope,BrainstormingVoiceCommand.Keep,BrainstormingRecognitionQuality.Clear,tickets));}else await Call(components.Preview,"ConfirmKeep");((MethodInfo)Member(components.Preview,"StateHasChanged")).Invoke(components.Preview,null);Assert.Contains("Committed",html.ToHtmlString());Assert.Contains("Inspect application",html.ToHtmlString());Assert.Contains("Undo kept version",html.ToHtmlString());await verify();
        });}finally{autosave?.Dispose();if(fixture is not null)await fixture.DisposeAsync();server?.Dispose();http?.Dispose();}
    }
    private sealed class LostCommit(HttpMessageHandler inner):DelegatingHandler(inner) {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct) {var response=await base.SendAsync(request,ct);if(request.RequestUri!.AbsolutePath.EndsWith("/commit",StringComparison.Ordinal)){response.Dispose();throw new HttpRequestException("Fixture lost committed response");}return response;}
    }
    [Fact]
    public async Task LostServerCommitResponseIsInspectedWithoutReplayingInsertion()
    {
        await using var f=new BrainstormingConversationTests.Fixture();var r=await BrainstormingKeepTests.Server(f,"insert");using var host=await Host(f);using var http=new HttpClient(new LostCommit(host.GetTestServer().CreateHandler())){BaseAddress=new("https://test.invalid/")};http.DefaultRequestHeaders.Add("X-Test-Identity","author");
        var cloud=(await new WriterApp.Application.AI.BrainstormingSessions(f.Db,"author",f.Data.Backend).ReadAsync(r.Scope.DocumentId,r.Scope.SessionId,default))!;var local=r.Scope with {Space=BrainstormingIdentitySpace.Local};var files=new LocalBrainstormingStore(Path.Combine(Path.GetTempPath(),"P07-lost-commit",Guid.NewGuid().ToString()));
        await files.WriteAsync(new(1,1,BrainstormingPersistence.ReScope(cloud,local),"",new(1,local,r.Scope,1,DateTimeOffset.UtcNow),null,null,[],[]),0,Guid.NewGuid());var flow=new BrainstormingKeepWorkflow(files);var api=new BrainstormingKeepHttp(http,_=>new(()=>{}));
        await Assert.ThrowsAsync<HttpRequestException>(()=>flow.ApplyAsync(local,r,review=>api.ApplyAsync(review)));
        Assert.Equal(BrainstormingApplicationState.Unknown,(await files.ReadAsync(local))!.Keeps!.Single().Receipt!.State);
        var inspected=await api.InspectAsync(r);Assert.Equal(BrainstormingApplicationState.Committed,inspected.State);await flow.RememberAsync(local,r,inspected);
        await flow.ApplyAsync(local,r,_=>throw new Exception("Confirmed operation must not dispatch again"));
        Assert.Equal(r.AfterHtml,(await f.Db.Pages.AsNoTracking().SingleAsync()).Content);Assert.Single(await f.Db.AiActionHistoryEntries.AsNoTracking().ToArrayAsync());
    }
}

