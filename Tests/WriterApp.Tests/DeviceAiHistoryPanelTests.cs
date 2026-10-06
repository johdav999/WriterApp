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
using WriterApp.Shared.Sync;
using Xunit;

namespace WriterApp.Tests;

public sealed class DeviceAiHistoryPanelTests
{
    private sealed class Components:IComponentActivator {
        public LocalAiPanel Panel=null!;
        public IComponent CreateInstance(Type type) { var value=(IComponent)Activator.CreateInstance(type)!;if(value is LocalAiPanel panel)Panel=panel;return value; }
    }
    private sealed class Harness:IAsyncDisposable {
        public HistoryTestFixture Fixture=new();public Components Components=new();public LocalAiHistory Entry=null!;public bool BlockSave, SaveRequested;
        private ServiceProvider Services=null!;private HtmlRenderer Renderer=null!;private HtmlRootComponent Root;
        public async Task Start() {
            await Fixture.Start();Entry=Fixture.Entry("Applied");await Fixture.Book.Repository.SaveAsync(Entry.After!);await Fixture.Store.SaveHistoryAsync(Entry);
            await Fixture.AddProposal(Guid.NewGuid(),original:"<script>cloud original</script>",proposed:"Cloud comparison sentinel");
            var f=Fixture;var host=new DeviceHostOptions("Test",f.Http.BaseAddress!);var repo=f.Book.Repository;
            Services=new ServiceCollection().AddLogging().AddSingleton(repo).AddSingleton(f.Account).AddSingleton(f.Store)
                .AddSingleton(new DeviceAiService(new LocalWritingTests.Api(),f.Account,f.Book.Network,host))
                .AddSingleton(new LocalAiHistoryActions(repo,f.Store,f.Account,host)).AddSingleton(new DevicePromptLibrary(f.Http,f.Account,f.Store)).AddSingleton(f.Service)
                .AddSingleton(new DeviceBibleService(new(f.Http),new(f.Book.Root+"/canon"),repo,f.Account,f.Book.Network,host))
                .AddSingleton(new DeviceSyncEngine(f.Book.Store,new(f.Book.Root+"/sync"),new EmptySync(),f.Account,f.Book.Network,repo,host))
                .AddSingleton<IJSRuntime>(new Js()).AddSingleton<IComponentActivator>(Components).BuildServiceProvider();
            Renderer=new(Services,Services.GetRequiredService<ILoggerFactory>());
            Root=await Renderer.Dispatcher.InvokeAsync(()=>Renderer.RenderComponentAsync<LocalAiPanel>(ParameterView.FromDictionary(new Dictionary<string,object?>{
                [nameof(LocalAiPanel.DocumentId)]=Entry.DocumentId,[nameof(LocalAiPanel.SectionId)]=f.Source.Sections[0].SectionId,[nameof(LocalAiPanel.Mode)]="history",
                [nameof(LocalAiPanel.BeforeWork)]=(Func<Task<bool>>)(()=>{SaveRequested=true;return Task.FromResult(!BlockSave);})})));
        }
        public Task Event(string method,params object?[] args)=>Renderer.Dispatcher.InvokeAsync(()=>EventCallback.Factory.Create(Components.Panel,
            (Func<Task>)(()=>typeof(LocalAiPanel).GetMethod(method,BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(Components.Panel,args) as Task??Task.CompletedTask)).InvokeAsync());
        public Task<string> Html()=>Renderer.Dispatcher.InvokeAsync(Root.ToHtmlString);
        public Task RendererSwitch()=>Renderer.Dispatcher.InvokeAsync(async()=>{Fixture.Identity.Id="other";await Fixture.Account.SignInAsync();});
        public async Task Evidence(string fixture){string? dir=Environment.GetEnvironmentVariable("WRITERAPP_P10_EVIDENCE");if(dir is not null)await File.WriteAllTextAsync(Path.Combine(dir,fixture+".html"),await Html());}
        public async ValueTask DisposeAsync(){if(Renderer is not null)await Renderer.DisposeAsync();if(Services is not null)await Services.DisposeAsync();Fixture.Dispose();}
    }
    private sealed class Js:IJSRuntime {
        public ValueTask<T> InvokeAsync<T>(string id,object?[]? args)=>throw new InvalidOperationException("History inspection must not invoke the editor.");
        public ValueTask<T> InvokeAsync<T>(string id,CancellationToken ct,object?[]? args)=>InvokeAsync<T>(id,args);
    }
    private sealed class EmptySync:IDeviceSyncApi {
        public Task<string> GetOwnerAsync(CancellationToken ct)=>Task.FromResult("account-1");
        public Task<SyncChanges> ChangesAsync(string? cursor,CancellationToken ct)=>Task.FromResult(new SyncChanges([],"cursor",false));
        public Task<SyncSnapshot> DownloadAsync(Guid id,CancellationToken ct)=>throw new NotSupportedException();
        public Task<SyncMutationResult> MutateAsync(Guid id,SyncMutation request,CancellationToken ct)=>throw new NotSupportedException();
    }
    [Fact] public async Task StaleEntryCanBeRemovedWithoutSavingWritingAndStaysRemovedAfterRefreshAndRestart() {
        await using var h = new Harness(); await h.Start();
        var current = (await h.Fixture.Book.Repository.LoadAsync(h.Entry.DocumentId))!;
        await h.Fixture.Book.Repository.SaveAsync(current with { Sections = current.Sections.Select((s, i) => i == 0
            ? s with { Pages = s.Pages.Select((p, j) => j == 0 ? p with { Content = "<p>Later writing</p>" } : p).ToArray() } : s).ToArray() });
        await h.Event("LoadCloudHistory");
        var before = LocalDocumentCodec.Encode((await h.Fixture.Book.Repository.LoadAsync(h.Entry.DocumentId))!);
        Assert.Contains("changed since the AI action", await h.Html());
        Assert.Contains("Remove from history", await h.Html());
        h.BlockSave = true; h.SaveRequested = false;
        await h.Event("DismissHistory", h.Entry.Id);
        Assert.False(h.SaveRequested);
        Assert.Equal(before, LocalDocumentCodec.Encode((await h.Fixture.Book.Repository.LoadAsync(h.Entry.DocumentId))!));
        Assert.Equal(1, (await h.Html()).Split("class=\"history-card\"").Length - 1);
        Assert.Contains("Your writing is unchanged", await h.Html());
        Assert.Equal("Applied", Assert.Single(await h.Fixture.Store.HistoryAsync(h.Entry.DocumentId)).Status);
        Assert.Equal(1, await h.Fixture.Service.PendingAsync(h.Entry.DocumentId));
        h.BlockSave = false;
        await h.Event("LoadCloudHistory"); await h.Event("RefreshHistory");
        Assert.Equal(1, (await h.Html()).Split("class=\"history-card\"").Length - 1);
        var restart = new LocalAiStore(h.Fixture.Book.Root + "/ai");
        var dismissed = await restart.DismissedHistoryAsync(h.Entry.DocumentId, h.Fixture.Scope);
        Assert.Contains(h.Entry.Id, dismissed);
        var cache = await h.Fixture.Service.CachedAsync(h.Fixture.Source);
        var combined = h.Fixture.Service.Combine(h.Fixture.Source, [], await restart.HistoryAsync(h.Entry.DocumentId), cache, dismissed);
        Assert.DoesNotContain(combined, item => item.Id == h.Entry.Id || item.Id == h.Fixture.Proposal);
        await h.Event("DeliverHistory"); Assert.Equal(0, await h.Fixture.Service.PendingAsync(h.Entry.DocumentId));
    }
    [Fact] public async Task CloudOnlyDismissalPersistsOfflineAndIsScopedToAccountAndDocument() {
        await using var h = new Harness(); await h.Start(); await h.Event("LoadCloudHistory");
        var cache = (await h.Fixture.Service.CachedAsync(h.Fixture.Source))!;
        var cloud = cache.Snapshot.Entries.Single(e => e.ProposalId != h.Fixture.Proposal);
        await h.Event("DismissHistory", cloud.ProposalId);
        Assert.DoesNotContain("Cloud comparison sentinel", await h.Html());
        h.Fixture.Book.Network.SetOnline(false); await h.Event("RefreshHistory");
        Assert.DoesNotContain("Cloud comparison sentinel", await h.Html());
        var restart = new LocalAiStore(h.Fixture.Book.Root + "/ai");
        Assert.Contains(cloud.ProposalId, await restart.DismissedHistoryAsync(h.Entry.DocumentId, h.Fixture.Scope));
        Assert.DoesNotContain(cloud.ProposalId, await restart.DismissedHistoryAsync(h.Entry.DocumentId, new string('a', 64)));
        Assert.Empty(await restart.DismissedHistoryAsync(Guid.NewGuid(), h.Fixture.Scope));
        Assert.Contains(cloud.ProposalId, (await h.Fixture.Service.CachedAsync(h.Fixture.Source))!.Snapshot.Entries.Select(e => e.ProposalId));
        Assert.Single(await restart.HistoryAsync(h.Entry.DocumentId));
    }
    [Fact] public async Task InterruptedOperationCannotBeDismissed() {
        await using var h = new Harness(); await h.Start();
        await h.Fixture.Store.SaveHistoryAsync(h.Entry with { Status = "Undoing" }); await h.Event("RefreshHistory");
        Assert.DoesNotContain("Remove from history", await h.Html());
        await h.Event("DismissHistory", h.Entry.Id);
        Assert.Contains("Finish the interrupted change", await h.Html());
        Assert.Empty(await h.Fixture.Store.DismissedHistoryAsync(h.Entry.DocumentId, h.Fixture.Scope));
    }
    [Fact] public async Task ActualHistoryReconcilesCloudRowsReportsSavedOutcomesAndUsesLocalUndoRecoveryOnly() {
        await using var h=new Harness();await h.Start();await h.Event("LoadCloudHistory");var html=await h.Html();
        Assert.Contains("Local + cloud",System.Net.WebUtility.HtmlDecode(html));Assert.Contains("Cloud comparison sentinel",html);Assert.DoesNotContain("<script>cloud original</script>",html);
        Assert.Equal(2,html.Split("class=\"history-card\"").Length-1);Assert.Equal(1,html.Split("Recover a copy").Length-1);await h.Evidence("history-combined");
        await h.Event("DeliverHistory");Assert.Equal(0,await h.Fixture.Service.PendingAsync(h.Entry.DocumentId));
        await h.Event("UndoHistory",h.Entry.Id);Assert.Equal("Undone",Assert.Single(await h.Fixture.Store.HistoryAsync(h.Entry.DocumentId)).Status);
        await h.Event("DeliverHistory");await h.Event("LoadCloudHistory");Assert.Equal("Undone",(await h.Fixture.Service.CachedAsync(h.Fixture.Source))!.Snapshot.Entries.Single(e=>e.ProposalId==h.Fixture.Proposal).State);
        await h.Event("RedoHistory",h.Entry.Id);Assert.Equal("<p>Elin</p>",(await h.Fixture.Book.Repository.LoadAsync(h.Entry.DocumentId))!.Sections[0].Pages[0].Content);
        await h.Event("Recover",h.Entry.Id);var documents=(await h.Fixture.Book.Repository.ListAsync()).Documents;Assert.Equal(2,documents.Count);Assert.Equal(h.Fixture.Source.Sections[0].Pages[0].Content,documents.Single(d=>d.DocumentId!=h.Entry.DocumentId).Sections[0].Pages[0].Content);
        h.Fixture.Book.Network.SetOnline(false);await h.Event("RefreshHistory");Assert.Contains("Cloud comparison sentinel",await h.Html());await h.Evidence("history-offline");
    }
    [Fact] public async Task ActualHistoryClearsPrivateCloudAndLocalLinkedRowsOnAccountChangeAndRefusesStaleRecovery() {
        await using var h=new Harness();await h.Start();await h.Event("LoadCloudHistory");Assert.Contains("Cloud comparison sentinel",await h.Html());
        await h.RendererSwitch();
        Assert.DoesNotContain("Cloud comparison sentinel",await h.Html());Assert.DoesNotContain("Local + cloud",System.Net.WebUtility.HtmlDecode(await h.Html()));
        await h.Event("Recover",h.Entry.Id);Assert.Contains("original account",await h.Html());Assert.Single((await h.Fixture.Book.Repository.ListAsync()).Documents);await h.Evidence("history-switched");
    }
    [Fact] public async Task ActualHistoryShowsRetainedReportingFailureWithoutChangingCompletedLocalSave() {
        await using var h=new Harness();await h.Start();h.Fixture.Handler.Unavailable=true;await h.Event("DeliverHistory");
        Assert.Contains("pending events are retained",await h.Html());Assert.Equal("Applied",Assert.Single(await h.Fixture.Store.HistoryAsync(h.Entry.DocumentId)).Status);Assert.Equal(1,await h.Fixture.Service.PendingAsync(h.Entry.DocumentId));await h.Evidence("history-error");
        h.Fixture.Handler.Unavailable=false;await h.Event("DeliverHistory");Assert.Equal(0,await h.Fixture.Service.PendingAsync(h.Entry.DocumentId));
    }
}
