using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.HtmlRendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using WriterApp.Device.Shared.Components;
using WriterApp.Device.Shared.Services;
using WriterApp.Device.Shared.Storage;
using WriterApp.Shared;
using WriterApp.Shared.Sync;
using Xunit;
namespace WriterApp.Tests;
public sealed class LocalPromptPanelTests
{
    private sealed class Components:IComponentActivator {
        public LocalPromptPanel Panel=null!;public LocalWritingPanel Writer=null!;public WriterApp.UI.Shared.PromptPresetEditor Editor=null!;
        public IComponent CreateInstance(Type type){var value=(IComponent)Activator.CreateInstance(type)!;if(value is LocalPromptPanel p)Panel=p;if(value is LocalWritingPanel w)Writer=w;if(value is WriterApp.UI.Shared.PromptPresetEditor e)Editor=e;return value;}
    }
    private sealed class Harness:IAsyncDisposable {
        public TranslationTestFixture Book=new();public PresetTransferFixture Cloud=new();public Components Components=new();public LocalWritingTests.Api Api=new();
        public LocalDocument Source=null!;public LocalAiStore Store=null!;public DevicePromptLibrary Library=null!;public ServiceProvider Services=null!;public HtmlRenderer Renderer=null!;public HtmlRootComponent Root;
        public async Task Start(){Source=await Book.Create();await Cloud.SignIn();Store=Book.History;Library=new(Cloud.Http,Book.Account,Store);var repository=Book.Repository;var ai=new DeviceAiService(Api,Book.Account,Book.Network);var page=Source.Sections[0].Pages[0];var text=page.Content[3..^4];
            Services=new ServiceCollection().AddLogging().AddSingleton(repository).AddSingleton(Book.Account).AddSingleton(ai).AddSingleton(Store).AddSingleton(Library).AddSingleton<IDeviceAiApi>(Api)
                .AddSingleton(new LocalWritingActions(repository,Store,ai,Api)).AddSingleton(new DeviceSyncEngine(Book.Store,new(Book.Root+"/sync"),new EmptySync(),Book.Account,Book.Network,repository,new("Test",new("https://test.invalid/"))))
                .AddSingleton<IJSRuntime>(new Js()).AddSingleton<IComponentActivator>(Components).BuildServiceProvider();Renderer=new(Services,Services.GetRequiredService<ILoggerFactory>());
            Root=await Renderer.Dispatcher.InvokeAsync(()=>Renderer.RenderComponentAsync<LocalPromptPanel>(ParameterView.FromDictionary(new Dictionary<string,object?>{
                [nameof(LocalPromptPanel.DocumentId)]=Source.DocumentId,[nameof(LocalPromptPanel.SectionId)]=Source.Sections[0].SectionId,
                [nameof(LocalPromptPanel.CloudProjectId)]=Source.ServerProjectId,
                [nameof(LocalPromptPanel.Capture)]=(Func<CancellationToken,Task<QualityEditorSource>>)(_=>Task.FromResult(new QualityEditorSource(Source,page.PageId,new(page.Content,text,text,0,text.Length,1,text.Length+1,0))))})));
        }
        public Task Event(object component,string method,params object?[] args)=>Renderer.Dispatcher.InvokeAsync(()=>EventCallback.Factory.Create(component,(Func<Task>)(()=>component.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(component,args) as Task??Task.CompletedTask)).InvokeAsync());
        public Task Event(string method,params object?[] args)=>Event(Components.Panel,method,args);
        public Task<string> Html()=>Renderer.Dispatcher.InvokeAsync(Root.ToHtmlString);
        public async Task Evidence(string name){foreach(var variable in new[]{"WRITERAPP_P09_EVIDENCE","WRITERAPP_P12_EVIDENCE"})if(Environment.GetEnvironmentVariable(variable) is { } dir)await File.WriteAllTextAsync(Path.Combine(dir,name+".html"),await Html());}
        public async ValueTask DisposeAsync(){if(Renderer is not null)await Renderer.DisposeAsync();if(Services is not null)await Services.DisposeAsync();Book.Dispose();Cloud.Dispose();}
    }
    [Fact]
    public async Task LibraryDisposalWaitsForStartedAccountReloadToReleaseTheStore()
    {
        await using var h = new Harness(); await h.Start();
        var gate = new PanelReadGateStore(h.Book.Root);
        typeof(LocalPromptPanel).GetProperty("Documents", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(h.Components.Panel, new LocalDocumentRepository(gate));
        Task? disposing = null;
        try {
            await h.Renderer.Dispatcher.InvokeAsync(() => h.Book.Account.SignOutAsync());
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            disposing = h.Renderer.DisposeAsync().AsTask();
            Assert.False(disposing.IsCompleted);
        }
        finally { gate.Release.TrySetResult(); if (disposing is not null) await disposing.WaitAsync(TimeSpan.FromSeconds(5)); }
        Assert.Equal(LocalDocumentCodec.Encode(h.Source), LocalDocumentCodec.Encode((await h.Book.Repository.LoadAsync(h.Source.DocumentId))!));
        using var unlocked = new FileStream(Path.Combine(h.Book.Root, ".store.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    }
    [Fact]
    public async Task ActualLibraryCreatesEditsPinsDeletesOfflineAndSharedEditorPreservesTypedChoices() {
        await using var h=new Harness();await h.Start();await h.Renderer.Dispatcher.InvokeAsync(()=>h.Book.Account.SignOutAsync());await h.Event("New");
        await h.Event("DraftChanged",ReusablePromptTransferTests.Custom("Offline preset") with{Pinned=false,Scope=WritingScope.Selection});await h.Event("Save");
        var local=Assert.Single(await h.Store.PresetsAsync(null));Assert.Contains("Offline preset",await h.Html());Assert.Contains("Sign in to transfer",await h.Html());
        await h.Event("Edit",local.Id);await h.Event(h.Components.Editor,"Scope",new ChangeEventArgs{Value="Section"});await h.Event(h.Components.Editor,"Set","tone","cinematic");await h.Event("Save");
        local=Assert.Single(await h.Store.PresetsAsync(null));Assert.Equal(WritingScope.Section,local.Scope);Assert.Equal("cinematic",ReusablePrompts.Primitive(local.Parameters!["tone"]));
        await h.Event("Pin",local.Id);Assert.Contains("Run pinned Offline preset",await h.Html());await h.Event("Edit",local.Id);await h.Evidence("library-editor");
        await h.Event("CancelEdit");await h.Event("DeleteLocal",local.Id);Assert.Empty(await h.Store.PresetsAsync(null));Assert.Contains("Cloud copies remain",await h.Html());
        await h.Evidence("library-offline");
        await h.Event("New");await h.Event(h.Components.Editor,"Kind",new ChangeEventArgs{Value="builtin"});await h.Event(h.Components.Editor,"Set","tone","Formal");await h.Event(h.Components.Editor,"Set","length","Shorter");await h.Event(h.Components.Editor,"Set","preserve_terms",false);await h.Evidence("library-builtin-editor");await h.Event("Save");
        var builtin=Assert.Single(await h.Store.PresetsAsync(null));Assert.Equal("rewrite.selection",builtin.BuiltinActionId);Assert.Equal("Formal",ReusablePrompts.Primitive(builtin.Parameters!["tone"]));Assert.Equal("Shorter",ReusablePrompts.Primitive(builtin.Parameters["length"]));Assert.Equal(false,ReusablePrompts.Primitive(builtin.Parameters["preserve_terms"]));
    }
    [Fact]
    public async Task ActualLibraryShowsRetryAndConflictChoicesAndKeepsBothAuthoredVersions() {
        await using var h=new Harness();await h.Start();await h.Event("New");await h.Event("DraftChanged",ReusablePromptTransferTests.Custom("Library preset") with{Pinned=false});await h.Event("Save");var local=Assert.Single(await h.Store.PresetsAsync(h.Library.Scope));
        h.Cloud.Handler.LoseNext=true;await h.Event("Copy",local.Id);Assert.Contains("Send / retry retained transfer",await h.Html());Assert.Contains("role=\"alert\"",await h.Html());var q=Assert.Single(await h.Store.TransfersAsync(h.Library.Scope!));await h.Evidence("library-pending-failed");
        await h.Event("Send",q.Id);await h.Event("Edit",local.Id);await h.Event("DraftChanged",LocalAiStore.Definition(local) with{Name="Library divergence"});await h.Event("Save");await h.Cloud.ChangeCloud(q.Request.PresetId,false);await h.Event("Copy",local.Id);
        var html=await h.Html();Assert.Contains("Resolve preset conflict",html);Assert.Contains("Cloud divergence",html);Assert.Contains("Library divergence",html);await h.Evidence("library-conflict");
        var conflict=(await h.Store.TransfersAsync(h.Library.Scope!)).Single(t=>t.Status=="Conflict");await h.Event("KeepBoth",conflict.Id);var fork=(await h.Store.TransfersAsync(h.Library.Scope!)).Single(t=>t.Status=="Pending");await h.Event("Send",fork.Id);await h.Event("RefreshCloud");
        Assert.Equal(2,(await h.Store.CachedPromptsAsync(h.Library.Scope!))!.Presets.Count);Assert.Contains("Library divergence",await h.Html());
        await h.Evidence("library-confirmed");
        await h.Event("Import",q.Request.PresetId);Assert.Contains(await h.Store.PresetsAsync(h.Library.Scope),p=>p.OriginCloudId==q.Request.PresetId);
        await h.Evidence("library-imported");
        await h.Renderer.Dispatcher.InvokeAsync(()=>h.Book.Account.SignOutAsync());Assert.DoesNotContain("Cloud divergence",await h.Html());Assert.Null(await h.Store.CachedPromptsAsync(WriterApp.Device.Shared.Storage.LocalBibleStore.ScopeKey(new("https://other.invalid/"),"other")));
    }
    [Theory][InlineData(WritingScope.Selection)][InlineData(WritingScope.Section)]
    public async Task ActualLibraryRunRoutesToWritingReviewAndApplyInsteadOfAnalysis(WritingScope scope) {
        await using var h=new Harness();await h.Start();await h.Event("New");await h.Event("DraftChanged",ReusablePromptTransferTests.Custom("Run target") with{Scope=scope,Pinned=false});await h.Event("Save");var local=Assert.Single(await h.Store.PresetsAsync(null));
        await h.Event("Run",local.Id);Assert.Contains("Review writing proposal",await h.Html());Assert.Contains("Approve &amp; apply revision",await h.Html());Assert.DoesNotContain("analysis-only",await h.Html());
        await h.Evidence("library-"+scope.ToString().ToLowerInvariant()+"-review");
        await h.Event(h.Components.Writer,"Apply");Assert.Equal("Applied",Assert.Single(await h.Store.HistoryAsync(h.Source.DocumentId)).Status);Assert.Contains("Elin",(await h.Book.Repository.LoadAsync(h.Source.DocumentId))!.Sections[0].Pages[0].Content);
    }
    [Fact]
    public async Task ProjectIdentityAcknowledgementUpdatesPresetAvailabilityWithoutReopeningLibrary() {
        await using var h=new Harness();await h.Start();await h.Store.SavePresetAsync(ReusablePromptTransferTests.Custom("Bound preset") with{Pinned=false,ProjectId=h.Source.ServerProjectId},null,null,h.Library.Scope);await h.Event("RefreshCloud");
        Assert.DoesNotContain("different cloud project",await h.Html());
        await h.Renderer.Dispatcher.InvokeAsync(()=>h.Components.Panel.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string,object?>{[nameof(LocalPromptPanel.CloudProjectId)]=Guid.NewGuid()})));
        Assert.Contains("different cloud project",await h.Html());
        await h.Renderer.Dispatcher.InvokeAsync(()=>h.Components.Panel.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string,object?>{[nameof(LocalPromptPanel.CloudProjectId)]=h.Source.ServerProjectId})));
        Assert.DoesNotContain("different cloud project",await h.Html());Assert.Equal(0,h.Api.Calls);
    }
    [Fact]
    public async Task SameAccountTokenRefreshDoesNotCancelTransfersOrWritingReviews() {
        await using var h=new Harness();await h.Start();await h.Event("New");await h.Event("DraftChanged",ReusablePromptTransferTests.Custom() with{Pinned=false});await h.Event("Save");var local=Assert.Single(await h.Store.PresetsAsync(null));
        h.Cloud.Handler.After=async()=>{await h.Book.Account.GetTokenAsync(default);};await h.Event("Copy",local.Id);Assert.Equal("Completed",Assert.Single(await h.Store.TransfersAsync(h.Library.Scope!)).Status);Assert.DoesNotContain("Operation canceled",await h.Html());
        h.Api.Before=async ct=>{await h.Book.Account.GetTokenAsync(ct);};await h.Event("Run",local.Id);Assert.Contains("Review writing proposal",await h.Html());
        await h.Renderer.Dispatcher.InvokeAsync(async()=>{await h.Book.Account.GetTokenAsync(default);});Assert.Contains("Review writing proposal",await h.Html());await h.Event(h.Components.Writer,"Apply");Assert.Equal("Applied",Assert.Single(await h.Store.HistoryAsync(h.Source.DocumentId)).Status);
    }
    private sealed class Js:IJSRuntime,IJSObjectReference {
        public ValueTask DisposeAsync()=>ValueTask.CompletedTask;
        public ValueTask<T> InvokeAsync<T>(string id,object?[]? args)=>InvokeAsync<T>(id,default,args);
        public ValueTask<T> InvokeAsync<T>(string id,CancellationToken ct,object?[]? args){ct.ThrowIfCancellationRequested();if(id=="import")return ValueTask.FromResult((T)(object)this);string html=(string)args![0]!;
            if(id=="captureTranslation")return ValueTask.FromResult((T)(object)new TranslationPageCapture([new("0.0",html[3..^4])]));
            if(id=="previewTranslation")return ValueTask.FromResult((T)(object)("<p>"+System.Net.WebUtility.HtmlEncode(((IReadOnlyList<TranslationRun>)args[2]!)[0].Text)+"</p>"));
            if(id=="qualityPlainText")return ValueTask.FromResult((T)(object)html[3..^4]);if(id=="validateQualityRange")return ValueTask.FromResult(default(T)!);
            if(id=="previewQualityRevision")return ValueTask.FromResult((T)(object)("<p>"+System.Net.WebUtility.HtmlEncode((string)args[5]!)+"</p>"));throw new NotSupportedException(id);
        }
    }
    private sealed class EmptySync:IDeviceSyncApi {
        public Task<string> GetOwnerAsync(CancellationToken ct)=>Task.FromResult("account-1");public Task<SyncChanges> ChangesAsync(string? cursor,CancellationToken ct)=>Task.FromResult(new SyncChanges([],"cursor",false));
        public Task<SyncSnapshot> DownloadAsync(Guid id,CancellationToken ct)=>throw new NotSupportedException();public Task<SyncMutationResult> MutateAsync(Guid id,SyncMutation request,CancellationToken ct)=>throw new NotSupportedException();
    }
}
