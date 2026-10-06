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

public sealed partial class LocalWritingPanelTests
{
    private sealed class Components : IComponentActivator {
        public LocalWritingPanel Panel = null!;
        public WriterApp.UI.Shared.WritingOptions Options = null!;
        public IComponent CreateInstance(Type type) { var value=(IComponent)Activator.CreateInstance(type)!; if (value is LocalWritingPanel p) Panel=p; if (value is WriterApp.UI.Shared.WritingOptions options) Options=options; return value; }
    }
    private sealed class Harness : IAsyncDisposable {
        public readonly TranslationTestFixture Fixture=new();public readonly Components Components=new(); public readonly LocalWritingTests.Api Api=new();
        public ServiceProvider Services=null!;public HtmlRenderer Renderer=null!;public HtmlRootComponent Root;public LocalDocument Source=null!;public QualityEditorSource Selection=null!;
        public int Before,After;
        public readonly Js Interop = new();
        public async Task Start(bool practice = false) {
            Source=await Fixture.Create();
            if (practice) Source=await Fixture.Store.ApplySyncAsync(Source with { ExtensionData=new() { ["desktopAiPracticeGuide"]=System.Text.Json.JsonSerializer.SerializeToElement(Guid.NewGuid().ToString("D")) } },Source.LocalRevision,default,projects:true);
            var p=Source.Sections[0].Pages[0];var text=p.Content[3..^4];Selection=new(Source,p.PageId,new(p.Content,text,text,0,text.Length,1,text.Length+1,0));
            var repository=Fixture.Repository;var ai=new DeviceAiService(Api,Fixture.Account,Fixture.Network);
            Services=new ServiceCollection().AddLogging().AddSingleton(repository).AddSingleton(Fixture.Account).AddSingleton(ai).AddSingleton(Fixture.History).AddSingleton<IDeviceAiApi>(Api)
                .AddSingleton(new LocalWritingActions(repository,Fixture.History,ai,Api))
                .AddSingleton(new DevicePromptLibrary(new HttpClient { BaseAddress = new("https://test.invalid/") }, Fixture.Account, Fixture.History))
                .AddSingleton(new DeviceSyncEngine(Fixture.Store,new(Fixture.Root+"/sync"),new EmptySync(),Fixture.Account,Fixture.Network,repository,new("Test",new("https://test.invalid/"))))
                .AddSingleton<IJSRuntime>(Interop).AddSingleton<IComponentActivator>(Components).BuildServiceProvider();
            Renderer=new(Services,Services.GetRequiredService<ILoggerFactory>());
            Root=await Renderer.Dispatcher.InvokeAsync(()=>Renderer.RenderComponentAsync<LocalWritingPanel>(ParameterView.FromDictionary(new Dictionary<string,object?>{
                [nameof(LocalWritingPanel.DocumentId)]=Source.DocumentId,[nameof(LocalWritingPanel.SectionId)]=Source.Sections[0].SectionId,
                [nameof(LocalWritingPanel.BeforeWork)]=(Func<Task<bool>>)(()=>{Before++;return Task.FromResult(true);}),
                [nameof(LocalWritingPanel.AfterWork)]=(Func<Task>)(()=>{After++;return Task.CompletedTask;}),
                [nameof(LocalWritingPanel.Capture)]=(Func<CancellationToken,Task<QualityEditorSource>>)(_=>Task.FromResult(Selection))})));
        }
        public Task Event(string name,params object?[] args)=>Renderer.Dispatcher.InvokeAsync(()=>EventCallback.Factory.Create(Components.Panel,(Func<Task>)(()=>
            typeof(LocalWritingPanel).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(Components.Panel,args) as Task??Task.CompletedTask)).InvokeAsync());
        public void Set(string name,object value)=>typeof(LocalWritingPanel).GetField(name,BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(Components.Panel,value);
        public Task<string> Html()=>Renderer.Dispatcher.InvokeAsync(Root.ToHtmlString);
        public async ValueTask DisposeAsync(){
            if(Renderer is not null)await Renderer.DisposeAsync();
            if(Services is not null){Fixture.Network.SetOnline(false);var sync=Services.GetRequiredService<DeviceSyncEngine>();sync.Dispose();await sync.SyncAsync();await Services.DisposeAsync();}
            Fixture.Dispose();
        }
    }
    [Fact]
    public async Task SignedOutActionsStayDisabledAndSignInRefreshesAvailabilityWithoutReopening() {
        await using var h = new Harness(); await h.Start();
        await h.Renderer.Dispatcher.InvokeAsync(() => h.Fixture.Account.SignOutAsync());
        await (Task)typeof(LocalWritingPanel).GetField("_availabilityRefresh",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(h.Components.Panel)!;
        var signedOut = new AngleSharp.Html.Parser.HtmlParser().ParseDocument(await h.Html());
        Assert.All(signedOut.QuerySelectorAll(".writing-operations button, .writing-rewrite"), b=>Assert.True(b.HasAttribute("disabled")));
        await h.Renderer.Dispatcher.InvokeAsync(() => h.Fixture.Account.SignInAsync());
        await (Task)typeof(LocalWritingPanel).GetField("_availabilityRefresh",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(h.Components.Panel)!;
        var signedIn = new AngleSharp.Html.Parser.HtmlParser().ParseDocument(await h.Html());
        Assert.All(signedIn.QuerySelectorAll(".writing-operations button, .writing-rewrite"), b=>Assert.False(b.HasAttribute("disabled")));
        Assert.Equal(0,h.Api.Calls);
    }
    [Fact]
    public async Task LateAvailabilityFromPreviousAccountCannotEnableDeniedActions() {
        await using var h = new Harness(); await h.Start();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        h.Api.AvailabilityBefore = async _ => { h.Api.AvailabilityBefore = null; entered.SetResult(); await release.Task; };
        var pending = h.Event("Refresh"); await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await h.Renderer.Dispatcher.InvokeAsync(() => h.Fixture.Account.SignOutAsync());
        h.Api.Available = false;
        await h.Renderer.Dispatcher.InvokeAsync(() => h.Fixture.Account.SignInAsync());
        await (Task)typeof(LocalWritingPanel).GetField("_availabilityRefresh",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(h.Components.Panel)!;
        release.SetResult(); await pending;
        var html = new AngleSharp.Html.Parser.HtmlParser().ParseDocument(await h.Html());
        Assert.All(html.QuerySelectorAll(".writing-operations button, .writing-rewrite"), b=>Assert.True(b.HasAttribute("disabled")));
        Assert.Equal(0,h.Api.Calls);
    }
    [Fact]
    public async Task LabelAloneCannotRequestServerDemoAndNormalWritingRetainsExplicitApplyUndo() {
        await using var h=new Harness();await h.Start(practice:true);Assert.Equal(0,h.Api.Calls);
        var original=LocalDocumentCodec.Encode(h.Source);
        h.Set("_scope",WritingScope.Section);await h.Event("Generate",OnboardingAiDemoRequest.ActionKey);
        Assert.False(OnboardingAiDemoRequest.IsRequested(OnboardingAiDemoRequest.ActionKey,h.Api.Last!.Parameters));
        Assert.Equal(original,LocalDocumentCodec.Encode((await h.Fixture.Repository.LoadAsync(h.Source.DocumentId))!));
        await h.Event("Dismiss");Assert.DoesNotContain("Review writing proposal",await h.Html());
        h.Set("_scope",WritingScope.Selection);await h.Event("Generate","rewrite.selection");
        Assert.False(h.Api.Last!.Parameters!.ContainsKey(OnboardingAiDemoRequest.ParameterKey));
        await h.Event("Apply");var entry=Assert.Single(await h.Fixture.History.HistoryAsync(h.Source.DocumentId),e=>e.Status=="Applied");
        await new LocalAiHistoryActions(h.Fixture.Repository,h.Fixture.History).ChangeAsync(h.Source.DocumentId,entry.Id,false);
        Assert.Equal(h.Source.Sections.SelectMany(s=>s.Pages).Select(p=>p.Content),(await h.Fixture.Repository.LoadAsync(h.Source.DocumentId))!.Sections.SelectMany(s=>s.Pages).Select(p=>p.Content));
    }
    [Theory][InlineData(WritingScope.Selection,"rewrite.selection")][InlineData(WritingScope.Selection,"change_tone.selection")][InlineData(WritingScope.Selection,"show_dont_tell.selection")]
    [InlineData(WritingScope.Section,"expand.section")][InlineData(WritingScope.Section,"tighten.section")][InlineData(WritingScope.Section,"change_tone.section")][InlineData(WritingScope.Section,"show_dont_tell.section")]
    [InlineData(WritingScope.Continuation,"propose.next-paragraph")]
    public async Task ActualPanelUsesDedicatedTargetsReviewsDismissesAndAppliesWithDurableHistory(WritingScope scope,string key) {
        await using var h=new Harness();await h.Start();h.Set("_scope",scope);await h.Event("SettingsChanged",new WritingSettings("Friendly","Longer",false));
        await h.Event("Generate",key);var review=await h.Html();Assert.Contains("Review writing proposal",review);Assert.Contains("Approve &amp;",review);
        Assert.Equal("Friendly",h.Api.Last!.Parameters!["tone"]);Assert.Equal(scope==WritingScope.Section ? 2:1,new AngleSharp.Html.Parser.HtmlParser().ParseDocument(review).QuerySelectorAll("h4").Length);
        Assert.Equal(LocalDocumentCodec.Encode(h.Source),LocalDocumentCodec.Encode((await h.Fixture.Repository.LoadAsync(h.Source.DocumentId))!));
        await h.Event("Dismiss");Assert.DoesNotContain("Review writing proposal",await h.Html());await h.Event("Generate",key);await h.Event("Apply");Assert.Contains("Writing saved locally",await h.Html());Assert.Equal(h.Before,h.After);
        var saved=(await h.Fixture.Repository.LoadAsync(h.Source.DocumentId))!;
        if(scope==WritingScope.Continuation){Assert.Equal(h.Source.Sections[0].Pages[0].Content,saved.Sections[0].Pages[0].Content);Assert.StartsWith(h.Source.Sections[0].Pages[^1].Content,saved.Sections[0].Pages[^1].Content);Assert.Contains(h.Api.Paragraph,saved.Sections[0].Pages[^1].Content);}
        Assert.Single(await h.Fixture.History.HistoryAsync(h.Source.DocumentId),e=>e.Status=="Applied");
        var evidence=Environment.GetEnvironmentVariable("WRITERAPP_P06_EVIDENCE");if(evidence is not null && (scope==WritingScope.Continuation||key=="expand.section"))await File.WriteAllTextAsync(Path.Combine(evidence,scope.ToString().ToLowerInvariant()+"-review.html"),review);
    }
    [Theory][InlineData("stale-selection")][InlineData("stale-source")][InlineData("account")][InlineData("partial")][InlineData("unsupported")][InlineData("missing-action")][InlineData("stale-capture")]
    public async Task PanelExplainsFailuresWithoutChangingSource(string failure) {
        await using var h=new Harness();await h.Start();h.Set("_scope",failure is "stale-selection" or "stale-capture" ? WritingScope.Selection:WritingScope.Section);
        if(failure=="stale-capture")h.Selection=h.Selection with{Editor=h.Selection.Editor with{PlainText="Stale full-page context"}};
        if(failure=="partial")h.Api.Invalid="{}";if(failure=="missing-action")h.Api.Available=false;
        if(failure=="unsupported")h.Source=await h.Fixture.Repository.SaveAsync(h.Source with{Sections=h.Source.Sections.Select(s=>s with{Pages=s.Pages.Select(p=>p with{Content="<iframe></iframe>"}).ToArray()}).ToArray()});
        await h.Event("Generate",failure is "stale-selection" or "stale-capture" ? "rewrite.selection":"expand.section");
        if(failure is "stale-selection" or "stale-source" or "account") {
            Assert.Contains("Approve &amp;",await h.Html());
            if(failure=="stale-selection")h.Selection=h.Selection with{Editor=h.Selection.Editor with{From=2}};
            if(failure=="stale-source")h.Source=await h.Fixture.Repository.SaveAsync(h.Source with{Title="Later"});
            if(failure=="account")await h.Renderer.Dispatcher.InvokeAsync(()=>h.Fixture.Account.SignOutAsync());
            await h.Event("Apply");
        }
        Assert.DoesNotContain(await h.Fixture.History.HistoryAsync(h.Source.DocumentId),e=>e.Status=="Applied");Assert.Equal(LocalDocumentCodec.Encode(h.Source),LocalDocumentCodec.Encode((await h.Fixture.Repository.LoadAsync(h.Source.DocumentId))!));
        if(failure is "unsupported" or "missing-action" or "stale-capture")Assert.Equal(0,h.Api.Calls);
        if(failure=="account")Assert.DoesNotContain("Approve &amp;",await h.Html());else Assert.Contains("role=\"alert\"",await h.Html());
    }
    [Fact]
    public async Task CancellationAndDisabledAvailabilityAreActionable() {
        await using var h=new Harness();await h.Start();h.Set("_scope",WritingScope.Section);h.Api.Available=false;await h.Event("Refresh");
        var html=await h.Html();Assert.Contains("required plan",html);Assert.All(new AngleSharp.Html.Parser.HtmlParser().ParseDocument(html).QuerySelectorAll("button").Where(b=>b.TextContent!="Refresh writing availability"),b=>Assert.True(b.HasAttribute("disabled")));
        h.Api.Available=true;var started=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);h.Api.Before=async ct=>{started.SetResult();await Task.Delay(Timeout.Infinite,ct);};
        var request=h.Event("Generate","expand.section");await started.Task.WaitAsync(TimeSpan.FromSeconds(5));Assert.Contains("Cancel writing request",await h.Html());
        await h.Renderer.Dispatcher.InvokeAsync(()=>((CancellationTokenSource)typeof(LocalWritingPanel).GetField("_cancel",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(h.Components.Panel)!).Cancel());await request;
        Assert.Contains("Writing request canceled",await h.Html());Assert.Empty(await h.Fixture.History.HistoryAsync(h.Source.DocumentId));
    }
    [Theory][InlineData("Neutral","Same")][InlineData("Formal","Longer")][InlineData("Casual","Shorter")]
    [InlineData("Friendly","Same")][InlineData("Technical","Same")][InlineData("Executive","Same")]
    public async Task RewriteSettingsPreviewTheChosenToneAndLengthWithoutDuplicatePresetControls(string tone,string length) {
        await using var h=new Harness();await h.Start();
        await h.Renderer.Dispatcher.InvokeAsync(()=>EventCallback.Factory.Create(h.Components.Options,(Func<Task>)(()=>
            (Task)typeof(WriterApp.UI.Shared.WritingOptions).GetMethod("Change",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(h.Components.Options,[new WritingSettings(tone,length)])!)).InvokeAsync());
        var html = new AngleSharp.Html.Parser.HtmlParser().ParseDocument(await h.Html());
        var button = Assert.Single(html.QuerySelectorAll("button.writing-rewrite"));
        Assert.Equal("Preview rewrite", button.TextContent); Assert.False(button.HasAttribute("disabled"));
        Assert.Null(html.QuerySelector("select[aria-label='Rewrite preset']"));
        Assert.DoesNotContain("Fix grammar", html.Body!.TextContent);
        Assert.DoesNotContain(html.QuerySelectorAll("button"), b => b.TextContent == "Rewrite selection");
        var evidence = Environment.GetEnvironmentVariable("WRITERAPP_REWRITE_EVIDENCE");
        if (evidence is not null && tone == "Formal") await File.WriteAllTextAsync(Path.Combine(evidence, "rewrite-settings.html"), await h.Html());
        if (Environment.GetEnvironmentVariable("WRITERAPP_P13_EVIDENCE") is { } toneEvidence && tone == "Executive")
            await File.WriteAllTextAsync(Path.Combine(toneEvidence, "desktop-executive.html"), await h.Html());
        await h.Event("PreviewAction");Assert.Equal("rewrite.selection",h.Api.LastKey);Assert.Equal(tone,h.Api.Last!.Parameters!["tone"]);Assert.Equal(length,h.Api.Last.Parameters["length"]);Assert.Equal(true,h.Api.Last.Parameters["preserve_terms"]);
        if(tone=="Executive")Assert.Equal("Rewrite (Executive)",h.Api.Last.Parameters["instruction"]);
        Assert.Contains("Review writing proposal", await h.Html());
        Assert.Equal(LocalDocumentCodec.Encode(h.Source),LocalDocumentCodec.Encode((await h.Fixture.Repository.LoadAsync(h.Source.DocumentId))!));
    }
    private sealed class Js : IJSRuntime,IJSObjectReference {
        public string? RangeError;
        public string? Copied;
        public ValueTask DisposeAsync()=>ValueTask.CompletedTask;
        public ValueTask<T> InvokeAsync<T>(string id,object?[]? args)=>InvokeAsync<T>(id,default,args);
        public ValueTask<T> InvokeAsync<T>(string id,CancellationToken ct,object?[]? args){ct.ThrowIfCancellationRequested();
            if(id=="import")return ValueTask.FromResult((T)(object)this);
            if(id=="navigator.clipboard.writeText") { Copied=(string)args![0]!; return ValueTask.FromResult(default(T)!); }
            string html=(string)args![0]!;if(html.Contains("iframe"))throw new JSException("Unsupported writing content.");
            if(id=="captureTranslation")return ValueTask.FromResult((T)(object)new TranslationPageCapture([new("0.0",html[3..^4])]));
            if(id=="previewTranslation")return ValueTask.FromResult((T)(object)("<p>"+System.Net.WebUtility.HtmlEncode(((IReadOnlyList<TranslationRun>)args[2]!)[0].Text)+"</p>"));
            if(id is "consistencyPlainText" or "qualityPlainText")return ValueTask.FromResult((T)(object)html[3..^4]);
            if(id=="previewContinuation")return ValueTask.FromResult((T)(object)(html+"<p>"+System.Net.WebUtility.HtmlEncode((string)args[2]!)+"</p>"));
            if(id=="previewQualityRevision")return ValueTask.FromResult((T)(object)("<p>"+System.Net.WebUtility.HtmlEncode((string)args[5]!)+"</p>"));
            if(id=="validateQualityRange") {
                if (RangeError is not null) throw new JSException(RangeError);
                return ValueTask.FromResult(default(T)!);
            }
            throw new NotSupportedException(id);
        }
    }
    private sealed class EmptySync : IDeviceSyncApi {
        public Task<string> GetOwnerAsync(CancellationToken ct)=>Task.FromResult("account-1");
        public Task<SyncChanges> ChangesAsync(string? cursor,CancellationToken ct)=>Task.FromResult(new SyncChanges([],"cursor",false));
        public Task<SyncSnapshot> DownloadAsync(Guid id,CancellationToken ct)=>throw new NotSupportedException();
        public Task<SyncMutationResult> MutateAsync(Guid id,SyncMutation request,CancellationToken ct)=>throw new NotSupportedException();
    }
}
