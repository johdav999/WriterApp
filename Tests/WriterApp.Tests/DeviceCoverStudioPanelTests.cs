using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.HtmlRendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WriterApp.Application.AI;
using WriterApp.Application.Usage;
using WriterApp.Device.Shared.Pages;
using WriterApp.Device.Shared.Services;
using WriterApp.Device.Shared.Storage;
using Xunit;

namespace WriterApp.Tests;

public sealed partial class DeviceCoverStudioPanelTests
{
    private sealed class Components:IComponentActivator {
        public CoverStudio Page=null!;
        public IComponent CreateInstance(Type type) { var value=(IComponent)Activator.CreateInstance(type)!;if(value is CoverStudio page)Page=page;return value; }
    }
    private sealed class Usage:IDeviceAiApi {
        public bool Free;
        public Task<AiUsageStatusDto> GetUsageAsync(CancellationToken ct)=>Task.FromResult(new AiUsageStatusDto{PlanKey=Free?"Free":"Professional",UiEnabled=true,AiEnabled=true,QuotaRemaining=10});
        public Task<AiActionExecuteResponseDto> ExecuteAsync(string key,AiActionExecuteRequestDto request,CancellationToken ct)=>throw new NotSupportedException();
    }
    private sealed class Harness:IAsyncDisposable {
        public CoverTestFixture Fixture=new();public Components Components=new();public Usage Usage=new();
        private ServiceProvider Services=null!;private HtmlRenderer Renderer=null!;private HtmlRootComponent Root;
        public async Task Start(bool remote=false) {
            await Fixture.Start();if(remote)await RemoteCoverAssetTests.SetRemote(Fixture);Services=new ServiceCollection().AddLogging().AddSingleton(Fixture.Documents).AddSingleton(Fixture.Account)
                .AddSingleton(Fixture.Network).AddSingleton(Fixture.Studio).AddSingleton<IDeviceAiApi>(Usage).AddSingleton<IComponentActivator>(Components).BuildServiceProvider();
            Renderer=new(Services,Services.GetRequiredService<ILoggerFactory>());
            Root=await Renderer.Dispatcher.InvokeAsync(()=>Renderer.RenderComponentAsync<CoverStudio>(ParameterView.FromDictionary(new Dictionary<string,object?>{[nameof(CoverStudio.DocumentId)]=Fixture.Source.DocumentId})));
        }
        public Task Event(string method,params object?[] args)=>Renderer.Dispatcher.InvokeAsync(()=>EventCallback.Factory.Create(Components.Page,
            (Func<Task>)(()=>typeof(CoverStudio).GetMethod(method,BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(Components.Page,args) as Task??Task.CompletedTask)).InvokeAsync());
        public Task<string> Html()=>Renderer.Dispatcher.InvokeAsync(Root.ToHtmlString);
        public Task Offline()=>Renderer.Dispatcher.InvokeAsync(()=>Fixture.Network.SetOnline(false));
        public Task SignOut()=>Renderer.Dispatcher.InvokeAsync(()=>Fixture.Account.SignOutAsync());
        public async Task Evidence(string fixture) { string? dir=Environment.GetEnvironmentVariable("WRITERAPP_P11_EVIDENCE");if(dir is not null)await File.WriteAllTextAsync(Path.Combine(dir,fixture+".html"),await Html()); }
        public async ValueTask DisposeAsync(){if(Renderer is not null)await Renderer.DisposeAsync();if(Services is not null)await Services.DisposeAsync();Fixture.Dispose();}
    }
    [Fact] public async Task ActualPageGeneratesSelectsSavesRecoversAndShowsOfflineCachedConcepts() {
        await using var h=new Harness();await h.Start();Assert.Contains("No concepts yet",await h.Html());await h.Evidence("cover-empty");
        await h.Event("Generate");Assert.Equal(1,h.Fixture.Provider.Calls);Assert.Contains("Select cover concept 2",await h.Html());await h.Evidence("cover-concepts");
        await h.Event("Select",1);await h.Event("Save");Assert.Contains("Project cover saved locally",await h.Html());Assert.Contains("Current saved project cover",await h.Html());await h.Evidence("cover-saved");
        var saved=(await h.Fixture.Documents.LoadAsync(h.Fixture.Source.DocumentId))!;Assert.Equal(1,(await h.Fixture.Cache.ReadAsync(h.Fixture.Studio.Scope!,saved))!.Selected);
        await h.Offline();Assert.Contains("different source revision",await h.Html());await h.Evidence("cover-offline");
        await h.Event("Restore");Assert.Null((await h.Fixture.Documents.LoadAsync(saved.DocumentId))!.Project!.CoverImageUrl);Assert.Contains("Previous project cover restored",await h.Html());
    }
    [Fact] public async Task ActualPageShowsPlanAndProviderFailureWithoutChangingCoverAndClearsDraftOnSignOut() {
        await using var h=new Harness();await h.Start();h.Usage.Free=true;await h.Event("Generate");Assert.Contains("Professional plan and available AI quota",await h.Html());Assert.Equal(0,h.Fixture.Provider.Calls);await h.Evidence("cover-plan");
        h.Usage.Free=false;await h.Event("Generate");h.Fixture.Api.Failure=503;await h.Event("Generate");Assert.Contains("previous cover and cached concepts are preserved",await h.Html());Assert.Contains("Select cover concept 2",await h.Html());await h.Evidence("cover-error");
        await h.SignOut();Assert.DoesNotContain("Select cover concept 2",await h.Html());await h.Evidence("cover-signed-out");Assert.Null((await h.Fixture.Documents.LoadAsync(h.Fixture.Source.DocumentId))!.Project!.CoverImageUrl);
    }
    [Fact] public async Task ActualPageShowsLoadingAndCancelPreservesTheSavedCover() {
        await using var h=new Harness();await h.Start();var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        h.Fixture.Provider.During=async ct=>{entered.TrySetResult();await Task.Delay(Timeout.Infinite,ct);};
        var generating=h.Event("Generate");await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Contains("Working",await h.Html());await h.Evidence("cover-loading");await h.Event("Cancel");await generating;
        Assert.Contains("Canceled",await h.Html());Assert.Null(await h.Fixture.Studio.CachedAsync(h.Fixture.Source));Assert.Null((await h.Fixture.Documents.LoadAsync(h.Fixture.Source.DocumentId))!.Project!.CoverImageUrl);
    }
}
