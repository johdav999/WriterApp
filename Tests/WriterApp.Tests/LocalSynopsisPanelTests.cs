using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.HtmlRendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using WriterApp.Application.AI;
using WriterApp.Application.Documents;
using WriterApp.Application.Usage;
using WriterApp.Device.Shared.Components;
using WriterApp.Device.Shared.Services;
using WriterApp.Device.Shared.Storage;
using WriterApp.Shared.Sync;
using WriterApp.UI.Shared;
using WriterApp.UI.Shared.Projects;
using Xunit;

namespace WriterApp.Tests;
public sealed class LocalSynopsisPanelTests
{
    private sealed class Components : IComponentActivator {
        public LocalAiPanel Panel=null!;
        public IComponent CreateInstance(Type type) { var c=(IComponent)Activator.CreateInstance(type)!;if(c is LocalAiPanel p)Panel=p;return c; }
    }
    private sealed class Harness : IAsyncDisposable {
        public readonly TranslationTestFixture Fixture=new();public readonly Components Components=new();public readonly LocalSynopsisCoachingTests.Api Api=new();public LocalDocument Source=null!; public LocalDocumentRepository Repo=null!;
        private ServiceProvider Services=null!;public HtmlRenderer Renderer=null!;private HtmlRootComponent Root;public int Flushes,Adoptions;
        public async Task Start(bool noScenes=false) {
            Source=await Fixture.Create();var store=Fixture.Store;var repo=Repo=new LocalDocumentRepository(store);var history=Fixture.History;var host=new DeviceHostOptions("Test",new("https://test.invalid/"));
            Source=LocalSynopsisCoachingTests.Source(Source);
            if(noScenes)Source=Source with{Sections=[],Project=Source.Project! with{Nodes=Source.Project!.Nodes.Where(n=>n.NodeType!="scene").ToArray()}};
            Source=await repo.SaveAsync(Source); Source=await store.ApplySyncAsync(Source with{SyncState=LocalSyncState.Synced},Source.LocalRevision,default,projects:true); var cache=new LocalBibleStore(Fixture.Root+"/canon");
            var http=new HttpClient(new Http()) {BaseAddress=host.ApiBaseAddress};
            var ai=new DeviceAiService(Api,Fixture.Account,Fixture.Network);
            Services=new ServiceCollection().AddLogging().AddSingleton(repo).AddSingleton(Fixture.Account).AddSingleton(Fixture.Network).AddSingleton(ai).AddSingleton(history)
                .AddSingleton(new LocalAiHistoryActions(repo,history)).AddSingleton(new DevicePromptLibrary(http,Fixture.Account,history))
                .AddSingleton(new DeviceBibleService(new(http),cache,repo,Fixture.Account,Fixture.Network,host))
                .AddSingleton(new DeviceSyncEngine(store,new(Fixture.Root+"/sync"),new EmptySync(),Fixture.Account,Fixture.Network,repo,host))
                .AddSingleton<IJSRuntime>(new Js()).AddSingleton<IComponentActivator>(Components).BuildServiceProvider();
            Renderer=new(Services,Services.GetRequiredService<ILoggerFactory>());
            Root=await Renderer.Dispatcher.InvokeAsync(()=>Renderer.RenderComponentAsync<LocalAiPanel>(ParameterView.FromDictionary(new Dictionary<string,object?>{
                [nameof(LocalAiPanel.DocumentId)]=Source.DocumentId,[nameof(LocalAiPanel.SectionId)]=Source.Sections.FirstOrDefault()?.SectionId,[nameof(LocalAiPanel.Mode)]="synopsis",[nameof(LocalAiPanel.FixedRequest)]=true,
                [nameof(LocalAiPanel.BeforeWork)]=(Func<Task<bool>>)(()=>{Flushes++;return Task.FromResult(true);}),[nameof(LocalAiPanel.AfterWork)]=(Func<Task>)(()=>{Adoptions++;return Task.CompletedTask;})})));
        }
        public Task Event(string name,params object?[] args)=>Renderer.Dispatcher.InvokeAsync(()=>EventCallback.Factory.Create(Components.Panel,(Func<Task>)(()=>typeof(LocalAiPanel).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(Components.Panel,args) as Task??Task.CompletedTask)).InvokeAsync());
        public Task<string> Html()=>Renderer.Dispatcher.InvokeAsync(Root.ToHtmlString);
        public async Task ExportFrame(string name) {
            string? evidence=Environment.GetEnvironmentVariable("WRITERAPP_P08_EVIDENCE");if(evidence is null)return;
            var html=await Html();
            var frame=await Renderer.Dispatcher.InvokeAsync(()=>Renderer.RenderComponentAsync<RightPanelShell>(ParameterView.FromDictionary(new Dictionary<string,object?> {
                [nameof(RightPanelShell.PinTrailingBody)]=true,
                [nameof(RightPanelShell.Header)]=(RenderFragment)(b=>b.AddContent(0,"Story · Synopsis")),
                [nameof(RightPanelShell.Body)]=(RenderFragment)(b=>{b.OpenComponent<SynopsisFields>(0);b.AddAttribute(1,"ValueModel",Source.Project!.Synopsis);b.CloseComponent();}),
                [nameof(RightPanelShell.TrailingBody)]=(RenderFragment)(b=>{b.OpenElement(0,"div");b.AddAttribute(1,"class","desktop-context-ai");b.AddMarkupContent(2,html);b.CloseElement();})
            })));
            await File.WriteAllTextAsync(Path.Combine(evidence,name+"-frame.html"),await Renderer.Dispatcher.InvokeAsync(frame.ToHtmlString));
        }
        public void Set(string field,object value)=>typeof(LocalAiPanel).GetField(field,BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(Components.Panel,value);
        public async ValueTask DisposeAsync(){if(Renderer is not null)await Renderer.DisposeAsync();if(Services is not null)await Services.DisposeAsync();Fixture.Dispose();}
    }
    [Theory][InlineData("evaluate")][InlineData("questions")][InlineData("suggest")]
    public async Task ActualCoachingControlsRenderDedicatedResultsAndPreserveWriting(string mode) {
        await using var h=new Harness();await h.Start(noScenes:true);h.Set("_synopsisMode",mode);h.Set("_instruction","My coaching notes");
        var initial=await h.Html();Assert.Contains("Scenes and manuscript pages are not required",initial);Assert.Contains("Synopsis coaching mode",initial);
        await h.Event("RunAi");Assert.Equal(mode,h.Api.Mode);Assert.Equal("My coaching notes",h.Api.Last!.UserNotes);
        var html=await h.Html();Assert.Contains(mode=="evaluate"?"Synopsis evaluation":mode=="questions"?"Guiding questions":"Review synopsis field suggestion",html);
        Assert.Contains("Synopsis analyzed",html);Assert.Equal(LocalDocumentCodec.Encode(h.Source),LocalDocumentCodec.Encode((await h.Repo.LoadAsync(h.Source.DocumentId))!));
        if(mode=="suggest") {Assert.Contains("Coaching commentary",html);Assert.Contains("New field text",html);Assert.Contains("Apply field suggestion",html);}
        else {Assert.DoesNotContain("Apply field suggestion",html);await h.Event("Apply");Assert.DoesNotContain(await h.Fixture.History.HistoryAsync(h.Source.DocumentId),e=>e.Status=="Applied");}
        string? evidence=Environment.GetEnvironmentVariable("WRITERAPP_P08_EVIDENCE");if(evidence is not null)await File.WriteAllTextAsync(Path.Combine(evidence,mode+"-review.html"),html);
        await h.ExportFrame(mode);
        Assert.Equal(h.Flushes,h.Adoptions);
    }
    [Theory][InlineData("evaluate")][InlineData("questions")][InlineData("suggest")]
    public async Task LongReviewRetainsCompleteTextWithoutMutatingSavedSynopsis(string mode) {
        await using var h=new Harness();await h.Start();h.Set("_synopsisMode",mode);
        h.Api.Feedback=string.Join("\n",Enumerable.Range(1,45).Select(i=>$"Coaching point {i}: consider the consequences for Åsa and her family."));
        h.Api.Proposed=string.Join("\n",Enumerable.Range(1,30).Select(i=>$"Suggested line {i}: Åsa risks losing her home."));
        await h.Event("RunAi");var html=await h.Html();
        Assert.Contains("Coaching point 45",html);if(mode=="suggest")Assert.Contains("Suggested line 30",html);
        Assert.Equal(LocalDocumentCodec.Encode(h.Source),LocalDocumentCodec.Encode((await h.Repo.LoadAsync(h.Source.DocumentId))!));
        await h.ExportFrame(mode+"-long");
    }
    [Theory][MemberData(nameof(LocalSynopsisCoachingTests.Fields),MemberType=typeof(LocalSynopsisCoachingTests))]
    public async Task ApprovedSuggestionIsPinnedToReviewedFieldAndHistoryCanUndoAfterReopen(string field) {
        await using var h=new Harness();await h.Start();h.Set("_field",field);await h.Event("RunAi");
        h.Set("_field",field=="notes"?"logline":"notes");await h.Event("Apply");var saved=(await h.Repo.LoadAsync(h.Source.DocumentId))!;
        Assert.Equal("New field text Åsa 日本語",WriterApp.Shared.SynopsisCoaching.Value(saved.Project!.Synopsis!,field));
        foreach(var other in WriterApp.Shared.SynopsisCoaching.Fields.Where(f=>f.Key!=field))Assert.Equal(WriterApp.Shared.SynopsisCoaching.Value(h.Source.Project!.Synopsis!,other.Key),WriterApp.Shared.SynopsisCoaching.Value(saved.Project.Synopsis!,other.Key));
        var entry=Assert.Single(await h.Fixture.History.HistoryAsync(h.Source.DocumentId));Assert.Equal(5,entry.Version);Assert.Equal("Applied",entry.Status);await h.Event("UndoHistory",entry.Id);
        Assert.Equal(h.Source.Project!.Synopsis,(await h.Repo.LoadAsync(h.Source.DocumentId))!.Project!.Synopsis);await h.Event("RedoHistory",entry.Id);Assert.Equal(saved.Project.Synopsis,(await h.Repo.LoadAsync(h.Source.DocumentId))!.Project!.Synopsis);
    }
    [Theory][InlineData("account")][InlineData("stale")][InlineData("dismiss")][InlineData("invalid")][InlineData("offline")][InlineData("quota")]
    public async Task ActualApplyHandlerRejectsAccountSourceAndMalformedResponses(string failure) {
        await using var h=new Harness();await h.Start();if(failure=="invalid")h.Api.Failure="source";if(failure=="offline")h.Fixture.Network.SetOnline(false);if(failure=="quota")h.Api.Quota=0;
        await h.Event("RunAi");if(failure=="account")await h.Renderer.Dispatcher.InvokeAsync(()=>h.Fixture.Account.SignOutAsync());
        if(failure=="stale")h.Source=await h.Repo.SaveAsync(h.Source with{Project=h.Source.Project! with{Synopsis=h.Source.Project!.Synopsis! with{Notes="Later author notes"}}});
        if(failure=="dismiss")h.Set("_proposal",null!);await h.Event("Apply");
        Assert.DoesNotContain(await h.Fixture.History.HistoryAsync(h.Source.DocumentId),e=>e.Status=="Applied");Assert.Equal(LocalDocumentCodec.Encode(h.Source),LocalDocumentCodec.Encode((await h.Repo.LoadAsync(h.Source.DocumentId))!));
        Assert.DoesNotContain("Review synopsis field suggestion",await h.Html());
    }
    [Fact]
    public async Task LoadingAndCancelRetainNoApplicableResult() {
        await using var h=new Harness();await h.Start();var started=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        h.Api.Before=async ct=>{started.SetResult();await Task.Delay(Timeout.Infinite,ct);};var run=h.Event("RunAi");await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Contains("Cancel request",await h.Html());await h.Event("Cancel");await run;Assert.Empty(await h.Fixture.History.HistoryAsync(h.Source.DocumentId));
        Assert.Equal(LocalDocumentCodec.Encode(h.Source),LocalDocumentCodec.Encode((await h.Repo.LoadAsync(h.Source.DocumentId))!));
    }
    private sealed class Http : HttpMessageHandler {protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r,CancellationToken ct)=>Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.NotFound));}
    private sealed class Js : IJSRuntime,IJSObjectReference {
        public ValueTask<T> InvokeAsync<T>(string id,object?[]? args)=>InvokeAsync<T>(id,default,args);
        public ValueTask<T> InvokeAsync<T>(string id,CancellationToken ct,object?[]? args)=>ValueTask.FromResult(id=="import"?(T)(object)this:(T)(object)System.Text.RegularExpressions.Regex.Replace(args![0]!.ToString()!,"<[^>]+>",""));
        public ValueTask DisposeAsync()=>ValueTask.CompletedTask;
    }
    private sealed class EmptySync : IDeviceSyncApi {
        public Task<string> GetOwnerAsync(CancellationToken ct)=>Task.FromResult("account-1");
        public Task<SyncChanges> ChangesAsync(string? cursor,CancellationToken ct)=>Task.FromResult(new SyncChanges([],"cursor",false));
        public Task<SyncSnapshot> DownloadAsync(Guid id,CancellationToken ct)=>throw new NotSupportedException();public Task<SyncMutationResult> MutateAsync(Guid id,SyncMutation request,CancellationToken ct)=>throw new NotSupportedException();
    }
}
