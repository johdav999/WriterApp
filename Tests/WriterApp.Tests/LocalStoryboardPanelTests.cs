using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using WriterApp.Application.AI;
using WriterApp.Application.Usage;
using WriterApp.Device.Shared.Components;
using WriterApp.Device.Shared.Services;
using WriterApp.Device.Shared.Storage;
using WriterApp.Shared.Sync;
using Xunit;

namespace WriterApp.Tests;

public sealed class LocalStoryboardPanelTests
{
    private sealed class Api : IDeviceAiApi
    {
        public string Output="{\"findings\":[]}";
        public AiActionExecuteRequestDto? Request;
        public Task<AiUsageStatusDto> GetUsageAsync(CancellationToken ct) => Task.FromResult(new AiUsageStatusDto { AiEnabled=true,UiEnabled=true,QuotaRemaining=10,SupportsDocumentVersionChecks=true });
        public Task<AiActionExecuteResponseDto> ExecuteAsync(string key,AiActionExecuteRequestDto request,CancellationToken ct)
        {
            Assert.Equal("storyboard.check-subplot-continuity",key); Request=request;
            return Task.FromResult(new AiActionExecuteResponseDto(Guid.NewGuid(),null,Output,"Continuity review",DateTimeOffset.UtcNow,key,SourceDocumentVersion:request.ExpectedDocumentVersion));
        }
    }
    private sealed class Components : IComponentActivator
    {
        public LocalAiPanel Panel=null!;
        public IComponent CreateInstance(Type type) { var c=(IComponent)Activator.CreateInstance(type)!;if(c is LocalAiPanel p)Panel=p;return c; }
    }

    [Theory]
    [InlineData("{\"findings\":[]}","More story context needed","empty-panel")]
    [InlineData("broken json","Unable to read this report","invalid-panel")]
    [InlineData("{\"assessment\":\"issues_found\",\"summary\":\"A story thread needs attention.\",\"findings\":[{\"subplotName\":\"Family secret\",\"issueType\":\"introduced_not_developed\",\"explanation\":\"The letter is introduced without a later development in the scene plan.\",\"affectedScenes\":[\"Homecoming / The letter\"],\"recommendation\":\"Let Mara decide whether to share the letter.\"}]}","Suggested next step","findings-panel")]
    public async Task ActualRunHandlerRendersStructuredReviewAndKeepsManuscriptUnchanged(string response,string expected,string evidenceName)
    {
        using var fixture=new TranslationTestFixture();var source=await fixture.Create();var repo=fixture.Repository;
        var api=new Api{Output=response};var components=new Components();var host=new DeviceHostOptions("Test",new("https://test.invalid/"));
        using var http=new HttpClient(new Http()){BaseAddress=host.ApiBaseAddress};
        using var services=new ServiceCollection().AddLogging().AddSingleton(repo).AddSingleton(fixture.Account).AddSingleton(fixture.Network)
            .AddSingleton(new DeviceAiService(api,fixture.Account,fixture.Network)).AddSingleton(fixture.History)
            .AddSingleton(new LocalAiHistoryActions(repo,fixture.History)).AddSingleton(new DevicePromptLibrary(http,fixture.Account,fixture.History))
            .AddSingleton(new DeviceBibleService(new(http),new(fixture.Root+"/canon"),repo,fixture.Account,fixture.Network,host))
            .AddSingleton(new DeviceSyncEngine(fixture.Store,new(fixture.Root+"/sync"),new EmptySync(),fixture.Account,fixture.Network,repo,host))
            .AddSingleton<IJSRuntime>(new Js()).AddSingleton<IComponentActivator>(components).BuildServiceProvider();
        await using var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
        var root=await renderer.Dispatcher.InvokeAsync(()=>renderer.RenderComponentAsync<LocalAiPanel>(ParameterView.FromDictionary(new Dictionary<string,object?> {
            [nameof(LocalAiPanel.DocumentId)]=source.DocumentId,[nameof(LocalAiPanel.SectionId)]=source.Sections[0].SectionId,
            [nameof(LocalAiPanel.Mode)]="storyboard",[nameof(LocalAiPanel.FixedRequest)]=true
        })));
        await renderer.Dispatcher.InvokeAsync(()=>EventCallback.Factory.Create(components.Panel,(Func<Task>)(()=>
            (Task)typeof(LocalAiPanel).GetMethod("RunAi",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(components.Panel,null)!)).InvokeAsync());
        string html=await renderer.Dispatcher.InvokeAsync(root.ToHtmlString);
        Assert.Contains(expected,html);Assert.Contains("Focus for this check (optional)",html);Assert.Contains("Entire storyboard",html);
        Assert.DoesNotContain("cloud version",html);Assert.DoesNotContain("ai-preview-columns",html);
        Assert.NotNull(api.Request);Assert.Contains("chapters",api.Request.Parameters!["storyboard_context"]!.ToString());
        Assert.Equal(LocalDocumentCodec.Encode(source),LocalDocumentCodec.Encode((await repo.LoadAsync(source.DocumentId))!));
        Assert.Equal(response,Assert.Single(await fixture.History.HistoryAsync(source.DocumentId)).Proposed);
        if(Environment.GetEnvironmentVariable("WRITERAPP_SUBPLOT_EVIDENCE") is { } evidence)
            await File.WriteAllTextAsync(Path.Combine(evidence,evidenceName+".html"),html);
    }
    private sealed class Http : HttpMessageHandler { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r,CancellationToken ct)=>Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.NotFound)); }
    private sealed class Js : IJSRuntime {
        public ValueTask<T> InvokeAsync<T>(string id,object?[]? args)=>ValueTask.FromResult(default(T)!);
        public ValueTask<T> InvokeAsync<T>(string id,CancellationToken ct,object?[]? args)=>ValueTask.FromResult(default(T)!);
    }
    private sealed class EmptySync : IDeviceSyncApi {
        public Task<string> GetOwnerAsync(CancellationToken ct)=>Task.FromResult("account-1");
        public Task<SyncChanges> ChangesAsync(string? cursor,CancellationToken ct)=>Task.FromResult(new SyncChanges([],"cursor",false));
        public Task<SyncSnapshot> DownloadAsync(Guid id,CancellationToken ct)=>throw new NotSupportedException();
        public Task<SyncMutationResult> MutateAsync(Guid id,SyncMutation request,CancellationToken ct)=>throw new NotSupportedException();
    }
}
