using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.HtmlRendering;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using WriterApp.Application.Covers;
using WriterApp.Application.Documents;
using WriterApp.Application.Security;
using WriterApp.Client.Components.Covers;
using WriterApp.Client.Services;
using WriterApp.Client.State;
using WriterApp.Shared;
using Xunit;

namespace WriterApp.Tests;

public sealed partial class DesktopAiWebPersistenceTests
{
    private sealed class CoverJs:IJSRuntime {
        public Dictionary<string,string> Values=new();public bool Full;
        public ValueTask<T> InvokeAsync<T>(string name,object?[]? args)=>InvokeAsync<T>(name,default,args);
        public ValueTask<T> InvokeAsync<T>(string name,CancellationToken ct,object?[]? args) {
            ct.ThrowIfCancellationRequested();object? value=null;
            if(name=="localStorage.getItem") {Values.TryGetValue((string)args![0]!,out var text);value=text;}
            if(name=="localStorage.setItem") {if(Full)throw new JSException("Cache quota exhausted");Values[(string)args![0]!]=(string)args[1]!;}
            if(name=="localStorage.removeItem")Values.Remove((string)args![0]!);
            return ValueTask.FromResult(value is T result ? result : default!);
        }
    }
    private sealed class CoverComponents:IComponentActivator {
        public ProjectCoverStudio Page=null!;public IComponent CreateInstance(Type t){var c=(IComponent)Activator.CreateInstance(t)!;if(c is ProjectCoverStudio p)Page=p;return c;}
    }
    private sealed class CoverNavigation:NavigationManager {public CoverNavigation()=>Initialize("http://web.test/","http://web.test/project");protected override void NavigateToCore(string uri,bool forceLoad){} }
    private sealed class CoverWebWire(HttpClient upstream):HttpMessageHandler {
        public bool Offline,LostAck;public Func<Task>? After;public int Edits;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct) {
            if(Offline)throw new HttpRequestException("Offline: cached review remains available.");
            var path=request.RequestUri!.PathAndQuery;bool edit=path=="/api/covers/edit";
            if(edit)Edits++;
            using var copy=new HttpRequestMessage(request.Method,path){Content=request.Content is null ? null : new ByteArrayContent(await request.Content.ReadAsByteArrayAsync(ct))};
            if(copy.Content is not null)copy.Content.Headers.ContentType=request.Content!.Headers.ContentType;
            var response=await upstream.SendAsync(copy,ct);
            if(edit && After is not null)await After();
            if(LostAck && path=="/api/covers/edit-save") {LostAck=false;response.Dispose();throw new HttpRequestException("Lost save acknowledgement");}
            return response;
        }
    }
    private const BindingFlags CoverPrivate=BindingFlags.NonPublic|BindingFlags.Instance;
    private sealed class CoverWebHarness:IAsyncDisposable {
        public TranslationFixture Fixture=new();public CoverTestFixture.Images Provider=new(){Values=[CoverTestFixture.Image]};
        public CoverJs Js=new();public CoverComponents Components=new();public CoverWebWire Wire=null!;public HttpClient Http=null!;public AuthStateService Auth=null!;public AuthMeStateService Me=null!;
        private ServiceProvider Services=null!;private HtmlRenderer Renderer=null!;private HtmlRootComponent Root;
        public async Task Start() {
            await Fixture.Start();Fixture.Http.Dispose();Fixture.Server.Dispose();
            Fixture.Server=Host($"Data Source={Path.Combine(Fixture.Root,"web.db")};Pooling=False",s=>{
                s.AddScoped<IDeletedUserIdentityService,DeletedUserIdentityService>();s.AddSingleton<ICoverAssetNetwork>(new RemoteCoverNetwork());s.Configure<CoverAssetOptions>(_=>{});
                s.AddScoped<TrustedCoverFetcher>();s.AddScoped<CoverAssetService>();s.AddSingleton<ICoverImageService>(Provider);
            });Fixture.Http=Fixture.Server.GetTestClient();Fixture.Http.DefaultRequestHeaders.Add("X-Test-Owner","ordinary-author");
            Wire=new(Fixture.Http);Http=new(Wire){BaseAddress=new("http://backend.test/")};
            var deleted=new DeletedAccountStateService();var duplicate=new DuplicateAccountStateService();
            Auth=new(Http,NullLogger<AuthStateService>.Instance,deleted,duplicate);SetAuth("ordinary-author");Me=new(Http,deleted,duplicate);
            typeof(AuthMeStateService).GetProperty(nameof(AuthMeStateService.EffectivePlanKey))!.SetValue(Me,"Professional");
            typeof(AuthMeStateService).GetProperty(nameof(AuthMeStateService.IsAuthenticated))!.SetValue(Me,true);
            var navigation=new CoverNavigation();
            var assembly=typeof(ProjectCoverStudio).Assembly;var apiType=assembly.GetType("WriterApp.Client.Services.CoverApiClient")!;var featureType=assembly.GetType("WriterApp.Client.Services.FeatureAccessService")!;
            var featureLogger=Activator.CreateInstance(typeof(NullLogger<>).MakeGenericType(featureType))!;
            Services=new ServiceCollection().AddLogging().AddSingleton(apiType,Activator.CreateInstance(apiType,Http)!).AddSingleton(Me).AddSingleton(Auth)
                .AddSingleton(featureType,Activator.CreateInstance(featureType,Me,navigation,featureLogger)!).AddSingleton<NavigationManager>(navigation)
                .AddSingleton<IJSRuntime>(Js).AddSingleton<IComponentActivator>(Components).BuildServiceProvider();
            Renderer=new(Services,Services.GetRequiredService<ILoggerFactory>());
            var project=new ProjectDto(Fixture.Project,"Åsa 日本語",null,null,null,null,null,default,default,0,Fixture.Document,Fixture.Document,1);
            await using(var db=new WriterApp.Data.AppDbContext(Fixture.Options))project=project with{MetadataRevision=(await db.Projects.SingleAsync()).MetadataRevision};
            Root=await Renderer.Dispatcher.InvokeAsync(()=>Renderer.RenderComponentAsync<ProjectCoverStudio>(ParameterView.FromDictionary(new Dictionary<string,object?>{[nameof(ProjectCoverStudio.Project)]=project})));
        }
        public void SetAuth(string owner) {
            typeof(AuthStateService).GetField("_cached",CoverPrivate)!.SetValue(Auth,new AuthState(true,"test",owner,new Dictionary<string,string>()));
            typeof(AuthStateService).GetField("_cachedAtUtc",CoverPrivate)!.SetValue(Auth,DateTimeOffset.UtcNow);
            typeof(AuthStateService).GetField("_hasResolvedState",CoverPrivate)!.SetValue(Auth,true);
        }
        public async Task Reopen() {
            await Renderer.DisposeAsync();Renderer=new(Services,Services.GetRequiredService<ILoggerFactory>());
            await using var db=new WriterApp.Data.AppDbContext(Fixture.Options);var saved=await db.Projects.SingleAsync();
            var project=new ProjectDto(Fixture.Project,"Åsa 日本語",null,null,null,null,saved.CoverImageUrl,default,default,0,Fixture.Document,Fixture.Document,saved.MetadataRevision);
            Root=await Renderer.Dispatcher.InvokeAsync(()=>Renderer.RenderComponentAsync<ProjectCoverStudio>(ParameterView.FromDictionary(new Dictionary<string,object?>{[nameof(ProjectCoverStudio.Project)]=project})));
        }
        public Task Event(string method,params object?[] args)=>Renderer.Dispatcher.InvokeAsync(()=>EventCallback.Factory.Create(Components.Page,
            (Func<Task>)(()=>typeof(ProjectCoverStudio).GetMethod(method,CoverPrivate)!.Invoke(Components.Page,args) as Task ?? Task.CompletedTask)).InvokeAsync());
        public Task<string> Html()=>Renderer.Dispatcher.InvokeAsync(Root.ToHtmlString);
        public async Task Evidence(string name) {var root=Environment.GetEnvironmentVariable("WRITERAPP_P18_EVIDENCE");if(root is not null)await File.WriteAllTextAsync(Path.Combine(root,"p18",name+".html"),await Html());}
        public CoverEditPreview? Preview=>(CoverEditPreview?)typeof(ProjectCoverStudio).GetField("_editPreview",CoverPrivate)!.GetValue(Components.Page);
        public async ValueTask DisposeAsync(){if(Renderer is not null)await Renderer.DisposeAsync();if(Services is not null)await Services.DisposeAsync();Http?.Dispose();await Fixture.DisposeAsync();}
    }
    [Theory][InlineData("variation")][InlineData("darker")][InlineData("brighter")][InlineData("cinematic")][InlineData("minimal")]
    public async Task ActualWebCoverEditReviewExplicitSelectionSaveAndOriginalRecovery(string operation) {
        await using var h=new CoverWebHarness();await h.Start();await h.Event("GenerateCoverConceptsAsync");await h.Event("RefreshEditsAsync");await h.Event("EditAsync",operation);
        Assert.NotNull(h.Preview);Assert.Equal(operation,h.Provider.Operation);Assert.Contains("Original is concept 1",await h.Html());await h.Evidence("web-"+operation);
        await using(var db=new WriterApp.Data.AppDbContext(h.Fixture.Options))Assert.Null((await db.Projects.SingleAsync()).CoverImageUrl);
        await h.Event("SelectEditAsync",1);await h.Event("SaveProjectCoverAsync");Assert.Contains("Cover save confirmed",await h.Html());Assert.Equal("committed",h.Preview!.Receipt!.State);
        await h.Event("RestoreEditAsync");Assert.Contains("Previous project cover restored",await h.Html());
        await using var verify=new WriterApp.Data.AppDbContext(h.Fixture.Options);Assert.Null((await verify.Projects.SingleAsync()).CoverImageUrl);Assert.Equal(h.Fixture.Html,(await verify.Pages.ToListAsync()).OrderBy(p=>Array.IndexOf(h.Fixture.Pages,p.Id)).Select(p=>p.Content));
    }
    [Fact] public async Task ActualWebLostAcknowledgementReopenOfflineReconcileAndNoDuplicateSave() {
        await using var h=new CoverWebHarness();await h.Start();await h.Event("GenerateCoverConceptsAsync");await h.Event("EditAsync","darker");await h.Event("SelectEditAsync",1);h.Wire.LostAck=true;
        await h.Event("SaveProjectCoverAsync");Assert.Contains("Check approved save",await h.Html());Assert.True(h.Preview!.SaveApproved);
        h.Wire.Offline=true;await h.Reopen();await h.Event("RefreshEditsAsync");Assert.Contains("Original is concept 1",await h.Html());await h.Evidence("web-offline");
        h.Wire.Offline=false;await h.Event("ReconcileEditAsync");Assert.Equal("committed",h.Preview.Receipt!.State);
        await using var db=new WriterApp.Data.AppDbContext(h.Fixture.Options);Assert.Single(await db.CoverEditSaves.ToListAsync());Assert.Equal(CoverTestFixture.SecondImage,(await db.Projects.SingleAsync()).CoverImageUrl);
    }
    [Theory][InlineData("unsupported")][InlineData("cancel")][InlineData("account")][InlineData("backend")][InlineData("metadata")][InlineData("invalid")][InlineData("cache-full")]
    public async Task ActualWebEditFailureOrLateContextNeverInstallsOrSavesAResult(string mode) {
        await using var h=new CoverWebHarness();await h.Start();await h.Event("GenerateCoverConceptsAsync");
        if(mode=="unsupported")h.Provider.Unsupported=true;
        if(mode=="cancel")h.Provider.During=_=>h.Event("CancelEdit");
        if(mode=="account")h.Wire.After=async()=>{h.SetAuth("other-owner");await h.Event("HandleAuthMeChanged");};
        if(mode=="backend")h.Wire.After=()=>{var property=typeof(ProjectCoverStudio).GetProperty("CoverApiClient",CoverPrivate)!;property.SetValue(h.Components.Page,Activator.CreateInstance(property.PropertyType,new HttpClient{BaseAddress=new("http://other-backend/")}));return Task.CompletedTask;};
        if(mode=="metadata")h.Provider.During=async _=>{await using var db=new WriterApp.Data.AppDbContext(h.Fixture.Options);await db.Projects.ExecuteUpdateAsync(s=>s.SetProperty(p=>p.MetadataRevision,99));};
        if(mode=="invalid")h.Provider.Edited="data:image/png;base64,YWJj";
        if(mode=="cache-full")h.Js.Full=true;
        await h.Event("EditAsync","minimal");Assert.Null(h.Preview);
        await using var verify=new WriterApp.Data.AppDbContext(h.Fixture.Options);Assert.Null((await verify.Projects.SingleAsync()).CoverImageUrl);
        if(mode=="unsupported")Assert.Equal(0,h.Provider.Edits);
    }
    [Fact] public async Task ActualWebSaveConflictPreservesReviewAndOtherCover() {
        await using var h=new CoverWebHarness();await h.Start();await h.Event("GenerateCoverConceptsAsync");await h.Event("EditAsync","brighter");await h.Event("SelectEditAsync",1);
        await using(var db=new WriterApp.Data.AppDbContext(h.Fixture.Options))await db.Projects.ExecuteUpdateAsync(s=>s.SetProperty(p=>p.CoverImageUrl,CoverTestFixture.Image).SetProperty(p=>p.MetadataRevision,99));
        await h.Event("SaveProjectCoverAsync");Assert.Null(h.Preview!.Receipt);
        await using var verify=new WriterApp.Data.AppDbContext(h.Fixture.Options);Assert.Equal(CoverTestFixture.Image,(await verify.Projects.SingleAsync()).CoverImageUrl);
    }
    [Fact] public async Task ActualCoverHttpRequiresAuthenticationOwnershipAndExactSourceHashBeforeProvider() {
        await using var h=new CoverWebHarness();await h.Start();await h.Event("GenerateCoverConceptsAsync");await h.Event("EditAsync","cinematic");
        var input=h.Preview!.Edit.Request;int calls=h.Provider.Edits;
        using var anonymous=h.Fixture.Server.GetTestClient();Assert.Equal(HttpStatusCode.Unauthorized,(await anonymous.PostAsJsonAsync("api/covers/edit",input)).StatusCode);
        using var other=h.Fixture.Server.GetTestClient();other.DefaultRequestHeaders.Add("X-Test-Owner","other-owner");Assert.Equal(HttpStatusCode.NotFound,(await other.PostAsJsonAsync("api/covers/edit",input)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await h.Fixture.Http.PostAsJsonAsync("api/covers/edit",input with{ContentHash=new string('0',64)})).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await h.Fixture.Http.PostAsJsonAsync("api/covers/edit",input with{AssetId=Guid.NewGuid()})).StatusCode);
        Assert.Equal(calls,h.Provider.Edits);
    }
    [Fact] public async Task ActualWebPriorCoverRecoverySurvivesNewPreviewDismissAndFreshComponent() {
        await using var h=new CoverWebHarness();await h.Start();await h.Event("GenerateCoverConceptsAsync");await h.Event("EditAsync","darker");await h.Event("SelectEditAsync",1);await h.Event("SaveProjectCoverAsync");
        var saved=h.Preview!.Receipt!;await h.Event("DismissEditAsync");Assert.Null(h.Preview);
        await h.Event("GenerateCoverConceptsAsync");await h.Event("EditAsync","minimal");Assert.NotNull(h.Preview);Assert.Null(h.Preview.Receipt);
        await h.Reopen();await h.Event("RefreshEditsAsync");await h.Event("RestoreEditAsync");
        Assert.Contains("Previous project cover restored",await h.Html());await using var db=new WriterApp.Data.AppDbContext(h.Fixture.Options);Assert.Null((await db.Projects.SingleAsync()).CoverImageUrl);
        Assert.Equal("restored",(await db.CoverEditSaves.SingleAsync()).State);Assert.Equal(saved.OperationId,(await db.CoverEditSaves.SingleAsync()).Id);
    }
}
