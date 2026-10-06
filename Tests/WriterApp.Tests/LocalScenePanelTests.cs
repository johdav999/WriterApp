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
using Xunit;

namespace WriterApp.Tests;
public sealed class LocalScenePanelTests
{
    private sealed class Components : IComponentActivator {
        public LocalAiPanel Panel=null!; public WriterApp.UI.Shared.SceneCoachingReview Review=null!;
        public IComponent CreateInstance(Type type) { var c=(IComponent)Activator.CreateInstance(type)!;if(c is LocalAiPanel p)Panel=p;if(c is WriterApp.UI.Shared.SceneCoachingReview r)Review=r;return c; }
    }
    private sealed class Api : IDeviceAiApi {
        public string Json="{\"summary\":\"New scene summary\",\"status\":\"Final\",\"povCharacterId\":\"character_1\",\"placeId\":\"foreign_id\"}";
        public AiActionExecuteRequestDto? Last;public string? Key;public Func<CancellationToken,Task>? Before;
        public Task<AiUsageStatusDto> GetUsageAsync(CancellationToken ct)=>Task.FromResult(new AiUsageStatusDto{AiEnabled=true,UiEnabled=true,QuotaRemaining=10,SupportsDocumentVersionChecks=true,SupportsCanonVersionChecks=true});
        public async Task<AiActionExecuteResponseDto> ExecuteAsync(string key,AiActionExecuteRequestDto request,CancellationToken ct){Key=key;Last=request;if(Before is not null)await Before(ct);return new(Guid.NewGuid(),null,Json,"",DateTimeOffset.UtcNow,key,SourceDocumentVersion:request.ExpectedDocumentVersion,SourceCanonVersions:request.ExpectedCanonVersions);}
    }
    private sealed class Harness : IAsyncDisposable {
        public readonly TranslationTestFixture Fixture=new();public readonly Components Components=new();public readonly Api Api=new();public LocalDocument Source=null!; public LocalDocumentRepository Repo=null!;
        private ServiceProvider Services=null!;public HtmlRenderer Renderer=null!;private HtmlRootComponent Root;public int Flushes,Adoptions;
        public async Task Start(bool fixedRequest=true) {
            Source=await Fixture.Create();var store=Fixture.Store;var repo=Repo=new LocalDocumentRepository(store);var history=Fixture.History;var host=new DeviceHostOptions("Test",new("https://test.invalid/"));
            var cache=new LocalBibleStore(Fixture.Root+"/canon");foreach(var pair in LocalSceneCoachingTests.Canon(Source).Snapshots)
                await cache.SaveAsync(new(1,LocalBibleStore.ScopeKey(host.ApiBaseAddress,Fixture.Account.AccountId!),Source.DocumentId,Source.ServerDocumentId!.Value,pair.Key,pair.Value,DateTimeOffset.UtcNow,"Ready"));
            var http=new HttpClient(new Http()) {BaseAddress=host.ApiBaseAddress};
            var ai=new DeviceAiService(Api,Fixture.Account,Fixture.Network);
            Services=new ServiceCollection().AddLogging().AddSingleton(repo).AddSingleton(Fixture.Account).AddSingleton(Fixture.Network).AddSingleton(ai).AddSingleton(history)
                .AddSingleton(new LocalAiHistoryActions(repo,history)).AddSingleton(new DevicePromptLibrary(http,Fixture.Account,history))
                .AddSingleton(new DeviceBibleService(new(http),cache,repo,Fixture.Account,Fixture.Network,host))
                .AddSingleton(new DeviceSyncEngine(store,new(Fixture.Root+"/sync"),new EmptySync(),Fixture.Account,Fixture.Network,repo,host))
                .AddSingleton<IJSRuntime>(new Js()).AddSingleton<IComponentActivator>(Components).BuildServiceProvider();
            Renderer=new(Services,Services.GetRequiredService<ILoggerFactory>());
            Root=await Renderer.Dispatcher.InvokeAsync(()=>Renderer.RenderComponentAsync<LocalAiPanel>(ParameterView.FromDictionary(new Dictionary<string,object?>{
                [nameof(LocalAiPanel.DocumentId)]=Source.DocumentId,[nameof(LocalAiPanel.SectionId)]=Source.Sections[0].SectionId,[nameof(LocalAiPanel.Mode)]="scene",[nameof(LocalAiPanel.FixedRequest)]=fixedRequest,
                [nameof(LocalAiPanel.BeforeWork)]=(Func<Task<bool>>)(()=>{Flushes++;return Task.FromResult(true);}),[nameof(LocalAiPanel.AfterWork)]=(Func<Task>)(()=>{Adoptions++;return Task.CompletedTask;})})));
        }
        public Task Event(string name,params object?[] args)=>Renderer.Dispatcher.InvokeAsync(()=>EventCallback.Factory.Create(Components.Panel,(Func<Task>)(()=>typeof(LocalAiPanel).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(Components.Panel,args) as Task??Task.CompletedTask)).InvokeAsync());
        public Task<string> Html()=>Renderer.Dispatcher.InvokeAsync(Root.ToHtmlString);
        public void Set(string field,object value)=>typeof(LocalAiPanel).GetField(field,BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(Components.Panel,value);
        public async ValueTask DisposeAsync(){if(Renderer is not null)await Renderer.DisposeAsync();if(Services is not null)await Services.DisposeAsync();Fixture.Dispose();}
    }
    [Theory][InlineData(true)][InlineData(false)]
    public async Task ExistingEditorAndStoryboardCoachReviewsChosenFieldsAndSavesOnlyApprovals(bool editor) {
        await using var h=new Harness();await h.Start(editor);
        if(editor)await h.Event("RunAi");else await h.Renderer.Dispatcher.InvokeAsync(()=>h.Components.Panel.SuggestSceneAsync("Refine intent",true));
        Assert.True(h.Api.Last is not null,$"Flushes={h.Flushes}, busy={h.Components.Panel.IsBusy}, error={typeof(LocalAiPanel).GetField("_error",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(h.Components.Panel)}");
        var html=await h.Html();Assert.True(html.Contains("Review scene fields"),html);Assert.Contains("Unresolved place ID",html);Assert.Contains("New scene summary",html);Assert.Contains("character_1",html);Assert.Contains("Label Character",html);Assert.DoesNotContain("Not cached.",html);Assert.DoesNotContain("change.Before",html);
        Assert.Equal(editor?"scene.suggest":"scene.refine",h.Api.Key);Assert.Equal(15,new AngleSharp.Html.Parser.HtmlParser().ParseDocument(html).QuerySelectorAll("select[aria-label='Scene coaching target'] option").Length-1);
        Assert.Equal(LocalDocumentCodec.Encode(h.Source),LocalDocumentCodec.Encode((await h.Repo.LoadAsync(h.Source.DocumentId))!));
        await h.Renderer.Dispatcher.InvokeAsync(()=>h.Components.Review.ApprovedChanged.InvokeAsync(new[]{SceneCoachingField.Status}));
        await h.Event("Apply");var saved=(await h.Repo.LoadAsync(h.Source.DocumentId))!;var scene=saved.Project!.Nodes.Single(n=>n.SectionId==h.Source.Sections[0].SectionId);Assert.Equal("Final",scene.Card!.Status);Assert.Null(scene.Card.Summary);Assert.Null(scene.Card.PovCharacterId);Assert.Equal(h.Flushes,h.Adoptions);
        Assert.Single(await h.Fixture.History.HistoryAsync(h.Source.DocumentId),e=>e.Version==4&&e.Status=="Applied");
        string? evidence=Environment.GetEnvironmentVariable("WRITERAPP_P07_EVIDENCE");if(evidence is not null)await File.WriteAllTextAsync(Path.Combine(evidence,editor?"editor-review.html":"storyboard-review.html"),html);
    }
    [Theory][InlineData("summary")][InlineData("status")][InlineData("povCharacterId")][InlineData("openQuestions")]
    public async Task ScopedControlsUseDedicatedInstructionsAndIgnoreUnrelatedFields(string field) {
        await using var h=new Harness();await h.Start();h.Set("_sceneField",field);if(field=="openQuestions")h.Api.Json="{\"openQuestions\":\"Who arrived?\",\"status\":\"Final\"}";
        await h.Event(field=="openQuestions"?"FindOpenQuestions":"RunAi");Assert.Equal(field,h.Api.Last!.Parameters!["focus_field"]);Assert.Contains("ONLY "+field,h.Api.Last.Parameters["instruction"]!.ToString());
        var review=await h.Html();Assert.Contains("Review scene fields",review);await h.Event("Apply");var saved=(await h.Repo.LoadAsync(h.Source.DocumentId))!;var card=saved.Project!.Nodes.Single(n=>n.SectionId==h.Source.Sections[0].SectionId).Card!;
        if(field!="status")Assert.Equal("Draft",card.Status);Assert.Equal(field=="openQuestions"?"scene.find-open-questions":"scene.suggest",h.Api.Key);
    }
    [Theory][InlineData("account")][InlineData("stale")][InlineData("dismiss")][InlineData("invalid")][InlineData("offline")]
    public async Task ReviewAccountSourceDismissMalformedAndDisconnectNeverApply(string failure) {
        await using var h=new Harness();await h.Start();h.Set("_sceneField","summary");if(failure=="invalid")h.Api.Json="broken";if(failure=="offline")h.Fixture.Network.SetOnline(false);
        await h.Event("RunAi");if(failure=="account")await h.Renderer.Dispatcher.InvokeAsync(()=>h.Fixture.Account.SignOutAsync());
        if(failure=="stale")h.Source=await h.Repo.SaveAsync(h.Source with{Title="Later author edit"});
        if(failure=="dismiss")h.Set("_proposal",null!);await h.Event("Apply");
        Assert.DoesNotContain(await h.Fixture.History.HistoryAsync(h.Source.DocumentId),e=>e.Status=="Applied");Assert.Equal(LocalDocumentCodec.Encode(h.Source),LocalDocumentCodec.Encode((await h.Repo.LoadAsync(h.Source.DocumentId))!));
        Assert.DoesNotContain("Review scene fields",await h.Html());
    }
    [Fact]
    public async Task CancelLoadingRetainsOriginalAndExposesCancelControl() {
        await using var h=new Harness();await h.Start();var started=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);h.Api.Before=async ct=>{started.SetResult();await Task.Delay(Timeout.Infinite,ct);};
        var run=h.Event("RunAi");await started.Task.WaitAsync(TimeSpan.FromSeconds(5));Assert.Contains("Cancel request",await h.Html());await h.Event("Cancel");await run;Assert.Empty(await h.Fixture.History.HistoryAsync(h.Source.DocumentId));Assert.Equal(LocalDocumentCodec.Encode(h.Source),LocalDocumentCodec.Encode((await h.Repo.LoadAsync(h.Source.DocumentId))!));
    }
    [Fact]
    public async Task HistoryLinkRedoExplainsUnconfirmedCanonAndPreservesUndoneFields() {
        await using var h=new Harness();await h.Start();h.Set("_sceneField","povCharacterId");await h.Event("RunAi");await h.Event("Apply");var entry=Assert.Single(await h.Fixture.History.HistoryAsync(h.Source.DocumentId));
        await h.Event("UndoHistory",entry.Id);var undone=(await h.Repo.LoadAsync(h.Source.DocumentId))!;
        await h.Event("RedoHistory",entry.Id);Assert.Contains("Refresh canon before reapplying",await h.Html());Assert.Equal(LocalDocumentCodec.Encode(undone),LocalDocumentCodec.Encode((await h.Repo.LoadAsync(h.Source.DocumentId))!));Assert.Equal("Undone",Assert.Single(await h.Fixture.History.HistoryAsync(h.Source.DocumentId)).Status);
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
