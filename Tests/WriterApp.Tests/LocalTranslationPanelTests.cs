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

public sealed class LocalTranslationPanelTests
{
    private sealed class Components : IComponentActivator {
        public LocalTranslationPanel Panel = null!;
        public IComponent CreateInstance(Type type) { var value=(IComponent)Activator.CreateInstance(type)!; if (value is LocalTranslationPanel p) Panel=p; return value; }
    }
    private sealed class Harness : IAsyncDisposable {
        public readonly TranslationTestFixture Fixture = new(); public readonly Components Components = new();
        public ServiceProvider Services = null!; public HtmlRenderer Renderer = null!; public HtmlRootComponent Root; public LocalDocument Source = null!;
        public int Before,After,Selection;
        public async Task Start() {
            Source=await Fixture.Create(); var repository=Fixture.Repository;
            Services=new ServiceCollection().AddLogging().AddSingleton(repository).AddSingleton(Fixture.Account).AddSingleton(Fixture.Ai).AddSingleton(Fixture.History)
                .AddSingleton(new LocalTranslationActions(repository,Fixture.History,Fixture.Ai))
                .AddSingleton(new DeviceSyncEngine(Fixture.Store,new(Fixture.Root+"/sync"),new EmptySync(),Fixture.Account,Fixture.Network,repository,new("Test",new("https://test.invalid/"))))
                .AddSingleton<IJSRuntime>(new Js()).AddSingleton<IComponentActivator>(Components).BuildServiceProvider();
            Renderer=new(Services,Services.GetRequiredService<ILoggerFactory>());
            Root=await Renderer.Dispatcher.InvokeAsync(() => Renderer.RenderComponentAsync<LocalTranslationPanel>(ParameterView.FromDictionary(new Dictionary<string,object?> {
                [nameof(LocalTranslationPanel.DocumentId)]=Source.DocumentId,[nameof(LocalTranslationPanel.SectionId)]=Source.Sections[0].SectionId,
                [nameof(LocalTranslationPanel.BeforeWork)]=(Func<Task<bool>>)(() => { Before++; return Task.FromResult(true); }),
                [nameof(LocalTranslationPanel.AfterWork)]=(Func<Task>)(() => { After++; return Task.CompletedTask; }),
                [nameof(LocalTranslationPanel.SelectionRequest)]=EventCallback.Factory.Create<TranslationSelectionOptions>(this,(TranslationSelectionOptions options) => {
                    Assert.Equal("en",options.TargetLanguage); Assert.Equal("auto",options.SourceLanguage); Assert.Equal("natural",options.Style); Selection++; })
            })));
        }
        public Task Event(string name, params object?[] args) => Renderer.Dispatcher.InvokeAsync(() =>
            EventCallback.Factory.Create(Components.Panel,(Func<Task>)(() =>
                typeof(LocalTranslationPanel).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(Components.Panel,args) as Task ?? Task.CompletedTask)).InvokeAsync());
        public Task<string> Html() => Renderer.Dispatcher.InvokeAsync(Root.ToHtmlString);
        public async ValueTask DisposeAsync() { if (Renderer is not null) await Renderer.DisposeAsync(); if (Services is not null) await Services.DisposeAsync(); Fixture.Dispose(); }
    }
    [Theory]
    [InlineData("section","replace")]
    [InlineData("document","replace")]
    [InlineData("section","duplicate-section")]
    [InlineData("document","duplicate-document")]
    public async Task ActualPanelReviewsEveryIntendedPageDismissesAndAppliesOnlyAfterApproval(string scope,string mode)
    {
        await using var h=new Harness(); await h.Start();
        await h.Event("Generate"); Assert.Equal(1,h.Selection); Assert.Equal(0,h.Fixture.Api.Calls);
        await h.Event("ScopeChanged",scope);
        typeof(LocalTranslationPanel).GetField("_mode",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(h.Components.Panel,mode);
        await h.Event("Generate"); string review=await h.Html();
        Assert.Contains("Approve &amp; apply translation",review); Assert.Contains("Source revision",review); Assert.Contains("Asa Japanese",review);
        Assert.Equal(scope=="document" ? 3 : 2,new AngleSharp.Html.Parser.HtmlParser().ParseDocument(review).QuerySelectorAll("h4").Length);
        Assert.Equal(LocalDocumentCodec.Encode(h.Source),LocalDocumentCodec.Encode((await h.Fixture.Repository.LoadAsync(h.Source.DocumentId))!));
        Assert.Equal("Reviewed",Assert.Single(await h.Fixture.History.HistoryAsync(h.Source.DocumentId)).Status);
        await h.Event("Dismiss"); Assert.DoesNotContain("Approve &amp; apply",await h.Html());
        await h.Event("Generate"); await h.Event("Apply");
        Assert.Contains("Translation saved locally",await h.Html());
        Assert.Single(await h.Fixture.History.HistoryAsync(h.Source.DocumentId),e => e.Status=="Applied"); Assert.Equal(h.Before,h.After);
        if (mode=="duplicate-document") Assert.Contains("Open translated copy",await h.Html());
        var evidence=Environment.GetEnvironmentVariable("WRITERAPP_P05_EVIDENCE");
        if (evidence is not null && scope=="document" && mode=="replace") await File.WriteAllTextAsync(Path.Combine(evidence,"translation-review.html"),
            "<!doctype html><html><head><meta charset='utf-8'><title>Translation component fixture</title><style>body{max-width:850px;margin:2rem auto;padding:1rem;font:16px system-ui}fieldset,label{display:block;margin:.5rem 0}button,select{margin:.3rem;padding:.5rem;max-width:100%}.translation-proposal-grid{display:grid;grid-template-columns:1fr 1fr;gap:1rem}.translation-proposal-text{white-space:pre-wrap;overflow-wrap:anywhere}@media(max-width:600px){.translation-proposal-grid{grid-template-columns:1fr}}</style></head><body>"+review+"</body></html>");
    }
    [Theory]
    [InlineData("unsupported")]
    [InlineData("partial")]
    [InlineData("stale")]
    [InlineData("account")]
    public async Task UnsupportedPartialStaleAndAccountFailuresPreserveWriting(string failure)
    {
        await using var h=new Harness(); await h.Start(); await h.Event("ScopeChanged","document");
        if (failure=="unsupported") h.Source=await h.Fixture.Repository.SaveAsync(h.Source with { Sections=h.Source.Sections.Select(s => s with { Pages=s.Pages.Select(p => p with { Content="<iframe></iframe>" }).ToArray() }).ToArray() });
        if (failure=="partial") h.Fixture.Api.Invalid="{}";
        await h.Event("Generate");
        if (failure is "account" or "stale") {
            Assert.Contains("Approve &amp; apply",await h.Html());
            if (failure=="account") await h.Renderer.Dispatcher.InvokeAsync(() => h.Fixture.Account.SignOutAsync());
            else h.Source=await h.Fixture.Repository.SaveAsync(h.Source with { Title="Later title" });
            await h.Event("Apply");
        }
        string html=await h.Html();
        if (failure=="account") Assert.DoesNotContain("Approve &amp; apply",html); else Assert.Contains("role=\"alert\"",html);
        Assert.Equal(LocalDocumentCodec.Encode(h.Source),LocalDocumentCodec.Encode((await h.Fixture.Repository.LoadAsync(h.Source.DocumentId))!));
        Assert.DoesNotContain(await h.Fixture.History.HistoryAsync(h.Source.DocumentId),e => e.Status=="Applied");
        if (failure=="unsupported") Assert.Equal(0,h.Fixture.Api.Calls);
    }
    [Fact]
    public async Task ScopeLanguagesStyleAndSharedClientChoicesUseTheSameCatalogueAndWireSettings()
    {
        await using var h=new Harness(); await h.Start(); await h.Event("ScopeChanged","section");
        await h.Event("SourceChanged","sv"); await h.Event("LanguageChanged","ja"); await h.Event("StyleChanged","literal");
        await h.Event("Generate");
        Assert.Equal("sv",h.Fixture.Api.Last!.Parameters!["source_language"]); Assert.Equal("ja",h.Fixture.Api.Last.Parameters["target_language"]);
        Assert.Equal("literal",h.Fixture.Api.Last.Parameters["style"]); Assert.Equal("translate.section",((DeviceAiProposal)typeof(LocalTranslationPanel).GetField("_proposal",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(h.Components.Panel)!).Prepared.Key);
        var root=await h.Renderer.Dispatcher.InvokeAsync(() => h.Renderer.RenderComponentAsync<WriterApp.UI.Shared.TranslationOptions>(ParameterView.FromDictionary(new Dictionary<string,object?> {
            [nameof(WriterApp.UI.Shared.TranslationOptions.Scope)]="section",[nameof(WriterApp.UI.Shared.TranslationOptions.FixedScope)]=true,
            [nameof(WriterApp.UI.Shared.TranslationOptions.SourceLanguage)]="sv",[nameof(WriterApp.UI.Shared.TranslationOptions.TargetLanguage)]="ja",
            [nameof(WriterApp.UI.Shared.TranslationOptions.Style)]="literal" })));
        string html=await h.Renderer.Dispatcher.InvokeAsync(root.ToHtmlString);
        Assert.Contains("Auto-detect",html); Assert.Contains("Literal",html); Assert.Contains("Find target language",html);
        var selects=new AngleSharp.Html.Parser.HtmlParser().ParseDocument(html).QuerySelectorAll("select");
        Assert.True(selects[0].HasAttribute("disabled"));
        Assert.Equal(WriterApp.Shared.Localization.TranslationLanguages.All.Count,selects[2].QuerySelectorAll("option").Length);
        var evidence=Environment.GetEnvironmentVariable("WRITERAPP_P05_EVIDENCE"); if (evidence is not null) await File.WriteAllTextAsync(Path.Combine(evidence,"client-translation-options.html"),html);
    }
    [Fact]
    public async Task CancellationShowsLoadingAndCannotPublishOrApplyPartialResult()
    {
        await using var h=new Harness(); await h.Start(); await h.Event("ScopeChanged","document");
        var started=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        h.Fixture.Api.Before=async ct => { started.SetResult(); await Task.Delay(Timeout.Infinite,ct); };
        var request=h.Event("Generate"); await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Contains("Cancel translation request",await h.Html());
        await h.Renderer.Dispatcher.InvokeAsync(() => ((CancellationTokenSource)typeof(LocalTranslationPanel).GetField("_cancel",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(h.Components.Panel)!).Cancel());
        await request; Assert.Contains("Translation canceled",await h.Html()); Assert.Empty(await h.Fixture.History.HistoryAsync(h.Source.DocumentId));
        Assert.Equal(LocalDocumentCodec.Encode(h.Source),LocalDocumentCodec.Encode((await h.Fixture.Repository.LoadAsync(h.Source.DocumentId))!));
    }
    private sealed class Js : IJSRuntime,IJSObjectReference {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public ValueTask<T> InvokeAsync<T>(string id,object?[]? args) => InvokeAsync<T>(id,default,args);
        public ValueTask<T> InvokeAsync<T>(string id,CancellationToken ct,object?[]? args) {
            ct.ThrowIfCancellationRequested();
            if (id=="import") return ValueTask.FromResult((T)(object)this);
            if (id=="captureTranslation") {
                string html=(string)args![0]!;
                if (html.Contains("iframe")) throw new JSException("Unsupported content; original retained.");
                return ValueTask.FromResult((T)(object)new TranslationPageCapture([new("0.0",html[3..^4])]));
            }
            if (id=="previewTranslation") return ValueTask.FromResult((T)(object)("<p>"+System.Net.WebUtility.HtmlEncode(((IReadOnlyList<TranslationRun>)args![2]!)[0].Text)+"</p>"));
            throw new NotSupportedException(id);
        }
    }
    private sealed class EmptySync : IDeviceSyncApi {
        public Task<string> GetOwnerAsync(CancellationToken ct) => Task.FromResult("account-1");
        public Task<SyncChanges> ChangesAsync(string? cursor,CancellationToken ct) => Task.FromResult(new SyncChanges([],"cursor",false));
        public Task<SyncSnapshot> DownloadAsync(Guid id,CancellationToken ct) => throw new NotSupportedException();
        public Task<SyncMutationResult> MutateAsync(Guid id,SyncMutation request,CancellationToken ct) => throw new NotSupportedException();
    }
}
