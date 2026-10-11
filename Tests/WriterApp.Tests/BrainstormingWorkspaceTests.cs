using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using WriterApp.Application.AI;
using WriterApp.Device.Shared.Storage;
using WriterApp.Shared.Brainstorming;
using WriterApp.UI.Shared;
using Xunit;

namespace WriterApp.Tests;

public sealed class BrainstormingWorkspaceTests
{
    private sealed class InterruptedDelivery(BrainstormingConversationTests.Fixture fixture):IBrainstormingConversation {
        public int Sends,Inspections,Stops,RunningInspections;public bool Wait,Unavailable;
        public TaskCompletionSource Accepted=new(TaskCreationOptions.RunContinuationsAsynchronously),Release=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async IAsyncEnumerable<BrainstormingConversationEvent> SendAsync(BrainstormingSend send,[System.Runtime.CompilerServices.EnumeratorCancellation]CancellationToken ct=default) {
            Sends++;var events=await fixture.Run(send);yield return events[0];Accepted.TrySetResult();
            if(Wait)await Release.Task.WaitAsync(ct);
            throw new IOException("Final stream event lost.");
        }
        public async Task<BrainstormingGeneration?> InspectAsync(BrainstormingScope scope,Guid operation,CancellationToken ct=default) {
            Inspections++;if(Unavailable)throw new HttpRequestException("Offline");
            var ledger=await fixture.Service().InspectAsync(scope,operation,ct);
            return RunningInspections-->0 && ledger is not null?ledger with{State=BrainstormingGenerationState.Accepted}:ledger;
        }
        public Task StopAsync(BrainstormingScope scope,Guid operation,CancellationToken ct=default){Stops++;return Task.CompletedTask;}
    }
    private static async Task<BrainstormingCache> MappedCache(BrainstormingConversationTests.Fixture f,LocalBrainstormingStore store) {
        var original=(await new BrainstormingSessions(f.Db,"author",f.Data.Backend).ReadAsync(f.Data.Document,f.Send.Scope.SessionId,default))!;
        var local=original.Scope with{Space=BrainstormingIdentitySpace.Local};
        return await store.WriteAsync(new(1,1,BrainstormingPersistence.ReScope(original,local),f.Send.Instruction,new(1,local,original.Scope,1,DateTimeOffset.UtcNow),null,
            new(1,Guid.NewGuid(),original.Scope,original.Revision,new('A',64),DateTimeOffset.UtcNow),[],[]),0,Guid.NewGuid());
    }
    private static BrainstormingConversationWorkflow RecoveryWorkflow(BrainstormingConversationTests.Fixture f,LocalBrainstormingStore store,IBrainstormingConversation remote)=>new(store,remote,async(scope,pull,ct)=>{
        var cache=(await store.ReadAsync(scope,ct))!;if(!pull)return cache;
        var session=(await new BrainstormingSessions(f.Db,"author",f.Data.Backend).ReadAsync(f.Data.Document,cache.Mapping!.Server.SessionId,ct))!;
        return await store.WriteAsync(cache with {Session=BrainstormingPersistence.ReScope(session,scope),Acknowledgement=cache.Acknowledgement! with{Revision=session.Revision}},cache.CacheRevision,Guid.NewGuid(),ct);
    });
    [Theory][InlineData(false)][InlineData(true)]
    public async Task BackgroundVoiceOnlyCancelsGenerationOwnedBySpokenTurnAndLostDeliveryRecovers(bool spoken) {
        await using var f=new BrainstormingConversationTests.Fixture();await f.Start();var files=Store();var cache=await MappedCache(f,files);var remote=new InterruptedDelivery(f){Wait=true};
        var components=new Components();using var services=Services(components);await using var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async()=>{
            var html=await renderer.RenderComponentAsync<BrainstormingConversationPanel>(ParameterView.FromDictionary(new Dictionary<string,object?>{
                {"Scope",cache.Session.Scope},{"Store",files},{"Ready",true},{"Workflow",RecoveryWorkflow(f,files,remote)},{"Availability",new BrainstormingCapability(BrainstormingCapabilityState.Available,"")}}));
            var sending=(Task)components.Panel.GetType().GetMethod("SendConversation",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(components.Panel,[spoken])!;
            await remote.Accepted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await components.Panel.BackgroundVoice();
            Assert.Equal(spoken?1:0,remote.Stops);
            if(!spoken)Assert.False(sending.IsCompleted);
            remote.Release.TrySetResult();await sending.WaitAsync(TimeSpan.FromSeconds(5));Render(components.Panel);
            var saved=(await files.ReadAsync(cache.Session.Scope))!;
            Assert.Null(saved.PendingGeneration);Assert.Equal(BrainstormingGenerationState.Completed,saved.LastGeneration!.State);
            Assert.Equal(2,saved.Session.Turns.Count);Assert.Equal("",saved.Composer);Assert.Contains("Three openings",html.ToHtmlString());Assert.DoesNotContain("Interrupted or uncertain",html.ToHtmlString());
            Assert.Equal(1,remote.Sends);Assert.Equal(1,remote.Inspections);Assert.Equal(1,f.Provider.Calls);Assert.Equal(1,f.Provider.Charges);
        });
    }
    [Theory][InlineData("completed")][InlineData("cancelled")][InlineData("unknown")][InlineData("offline")][InlineData("inspection-failure")][InlineData("settling")]
    public async Task ReopeningAcceptedCacheInspectsSavedOutcomeWithoutSendingAgain(string mode) {
        await using var f=new BrainstormingConversationTests.Fixture();await f.Start();var files=Store();var cache=await MappedCache(f,files);
        if(mode=="cancelled")f.Provider.BeforeComplete=()=>f.Service().StopAsync(f.Send.Scope,f.Send.OperationId,default);
        if(mode=="unknown")f.Provider.Mode="disconnect";
        if(mode=="cancelled")await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>f.Run());else await f.Run();
        var ledger=(await f.Service().InspectAsync(f.Send.Scope,f.Send.OperationId,default))!;
        await files.WriteAsync(cache with{PendingGeneration=f.Send,LastGeneration=ledger with{State=BrainstormingGenerationState.Accepted,Events=[ledger.Events[0]],InspectionText="",Message="Request accepted."}},cache.CacheRevision,Guid.NewGuid());
        var remote=new InterruptedDelivery(f){Unavailable=mode=="inspection-failure",RunningInspections=mode=="settling"?1:0};var components=new Components();using var services=Services(components);await using var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async()=>{
            var html=await renderer.RenderComponentAsync<BrainstormingConversationPanel>(ParameterView.FromDictionary(new Dictionary<string,object?>{
                {"Scope",cache.Session.Scope},{"Store",files},{"Ready",true},{"Workflow",RecoveryWorkflow(f,files,remote)},
                {"Availability",new BrainstormingCapability(mode=="offline"?BrainstormingCapabilityState.Offline:BrainstormingCapabilityState.Available,"")}}));
            var saved=(await files.ReadAsync(cache.Session.Scope))!;
            bool resolved=mode is "completed" or "cancelled" or "settling";Assert.Equal(resolved,saved.PendingGeneration is null);
            Assert.Equal(mode=="offline"?0:mode=="settling"?2:1,remote.Inspections);Assert.Equal(0,remote.Sends);Assert.Equal(1,f.Provider.Calls);
            if(resolved || mode=="unknown")Assert.Equal(2,saved.Session.Turns.Count);
            if(mode is "completed" or "settling"){Assert.Equal("",saved.Composer);Assert.Contains("Three openings",html.ToHtmlString());}
            else Assert.Equal(f.Send.Instruction,saved.Composer);
            if(mode=="cancelled"){Assert.Equal(BrainstormingGenerationState.Cancelled,saved.LastGeneration!.State);Assert.DoesNotContain("Interrupted or uncertain",html.ToHtmlString());}
        });
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task RejectedThirdResponseRetainsEarlierProseAndLegacyJsonIsOnlyInCollapsedInspection(bool legacy) {
        await using var f=new BrainstormingConversationTests.Fixture();await f.Start();
        async Task<BrainstormingSession> Read()=> (await new BrainstormingSessions(f.Db,"author",f.Data.Backend).ReadAsync(f.Data.Document,f.Send.Scope.SessionId,default))!;
        f.Provider.Wire=BrainstormingPersistence.Json(new BrainstormingAnswer("First passage ready.",1,BrainstormingIntent.Draft,new("Anna started the drill.","",[]),[],[]));await f.Run();
        var session=await Read();
        BrainstormingSend Next(string instruction)=>f.Send with {OperationId=Guid.NewGuid(),RequestId=Guid.NewGuid(),UserTurnId=Guid.NewGuid(),AssistantTurnId=Guid.NewGuid(),ExpectedRevision=session.Revision,ParentDraftId=session.Drafts.Last().Id,Instruction=instruction};
        f.Provider.Wire=BrainstormingPersistence.Json(new BrainstormingAnswer("Second passage ready.",1,BrainstormingIntent.Revise,new("The patrol left.","",[],true),[],[]));await f.Run(Next("The patrol leaves."));
        session=await Read();var retained=BrainstormingPersistence.Json(session.Drafts);var send=Next("Add the drill stopping and a discovery beneath the ice.");
        f.Provider.Wire="""{"answer":"A new passage is ready.","version":1,"intent":"Draft","draft":{"text":"The drill stopped. A metal surface shone beneath the ice.","constraints":"","introducedFacts":[]},"planning":[]}""";
        await f.Run(send);session=await Read();Assert.Equal(retained,BrainstormingPersistence.Json(session.Drafts));
        var failed=session.Turns.Last();Assert.Equal(BrainstormingTurnState.Failed,failed.State);Assert.DoesNotContain("\"draft\"",failed.Text);
        Assert.Equal(f.Provider.Wire,(await f.Service().InspectAsync(send.Scope,send.OperationId,default))!.InspectionText);
        if(legacy)session=session with {Turns=session.Turns.Select(t=>t.Id==failed.Id?t with {State=BrainstormingTurnState.Incomplete,Text=f.Provider.Wire}:t).ToArray()};
        var local=session.Scope with{Space=BrainstormingIdentitySpace.Local};var files=Store();
        await files.WriteAsync(new(1,1,BrainstormingPersistence.ReScope(session,local),send.Instruction,null,null,null,[],[]),0,Guid.NewGuid());
        var components=new Components();using var services=Services(components);await using var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async()=>{
            var html=await renderer.RenderComponentAsync<BrainstormingConversationPanel>(ParameterView.FromDictionary(new Dictionary<string,object?>{{"Scope",local},{"Store",files},{"Ready",true}}));
            var document=new AngleSharp.Html.Parser.HtmlParser().ParseDocument(html.ToHtmlString());
            var article=document.QuerySelectorAll(".brainstorm-message").Last();
            Assert.Contains("Your working draft is unchanged",article.TextContent);
            Assert.Empty(article.QuerySelectorAll(":scope > pre"));
            if(legacy) {var details=Assert.Single(article.QuerySelectorAll("details"));Assert.False(details.HasAttribute("open"));Assert.Equal(f.Provider.Wire,details.QuerySelector("pre")!.TextContent);}
            else Assert.Empty(article.QuerySelectorAll("details"));
            Assert.Contains("First passage ready",document.Body!.TextContent);Assert.Contains("Second passage ready",document.Body.TextContent);
            Assert.Equal(send.Instruction,(await files.ReadAsync(local))!.Composer);
        });
    }

    private sealed class Capabilities(BrainstormingCapabilityState state=BrainstormingCapabilityState.Available):IBrainstormingCapabilityService {
        public Task<BrainstormingCapabilities> ReadAsync(CancellationToken ct=default)=>Task.FromResult(state==BrainstormingCapabilityState.Available?BrainstormingCapabilities.Current:BrainstormingCapabilities.All(state,"Offline or unsupported backend; cached writing remains."));
    }
    private sealed class NoRemote:IBrainstormingRemote,IBrainstormingConversation {
        public int Sends;
        public IAsyncEnumerable<BrainstormingConversationEvent> SendAsync(BrainstormingSend value,CancellationToken ct=default){Sends++;throw new Exception("No generation on reopen or pane changes.");}
        public Task<BrainstormingGeneration?> InspectAsync(BrainstormingScope scope,Guid operation,CancellationToken ct=default)=>Task.FromResult<BrainstormingGeneration?>(null);
        Task<BrainstormingReceipt?> IBrainstormingRemote.InspectAsync(BrainstormingScope scope,Guid operation,CancellationToken ct)=>throw new Exception("No automatic exchange.");
        public Task StopAsync(BrainstormingScope scope,Guid operation,CancellationToken ct=default)=>Task.CompletedTask;
        public Task<BrainstormingSession?> ReadAsync(BrainstormingScope scope,CancellationToken ct=default)=>throw new Exception("No automatic exchange.");
        public Task<BrainstormingReceipt> WriteAsync(BrainstormingWrite value,CancellationToken ct=default)=>throw new Exception("No automatic exchange.");
        public Task<BrainstormingSessionPage> ListAsync(Guid document,Guid? after=null,int limit=25,CancellationToken ct=default)=>throw new Exception("No automatic exchange.");
    }
    private sealed class Js:IJSRuntime {public ValueTask<T> InvokeAsync<T>(string name,object?[]? args)=>throw new Exception("Static renderer does not execute JS.");public ValueTask<T> InvokeAsync<T>(string name,CancellationToken ct,object?[]? args)=>InvokeAsync<T>(name,args);}
    public sealed class RetainedEditor:ComponentBase {protected override void BuildRenderTree(RenderTreeBuilder b){b.OpenElement(0,"div");b.AddAttribute(1,"class","retained-editor");b.AddContent(2,"Original manuscript 日本語");b.CloseElement();}}
    private sealed class Components:IComponentActivator {
        public BrainstormingWorkspace Workspace=default!;public BrainstormingConversationPanel Panel=default!;public BrainstormingContextReview Review=default!;public EditorWorkspace Shell=default!;public int Editors;
        public IComponent CreateInstance(Type type){var value=(IComponent)Activator.CreateInstance(type)!;if(value is BrainstormingWorkspace w)Workspace=w;if(value is BrainstormingConversationPanel p)Panel=p;if(value is BrainstormingContextReview r)Review=r;if(value is EditorWorkspace e)Shell=e;if(value is RetainedEditor)Editors++;return value;}
    }
    private static void Set(object value,string name,object? data){var type=value.GetType();var flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;if(type.GetField(name,flags) is {} field)field.SetValue(value,data);else type.GetProperty(name,flags)!.SetValue(value,data);}
    private static object? Get(object value,string name)=>value.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(value);
    private static Task Call(object value,string method)=>(Task)value.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(value,[])!;
    // HtmlRenderer omits interactive after-render callbacks; advance that lifecycle explicitly.
    private static Task AfterRender(BrainstormingWorkspace value)=>(Task)typeof(BrainstormingWorkspace).GetMethod("OnAfterRenderAsync",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(value,[false])!;
    private static void Render(object value)=>typeof(ComponentBase).GetMethod("StateHasChanged",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(value,[]);
    private static async Task Loaded(Components components,BrainstormingScope scope) {
        var deadline=DateTimeOffset.UtcNow.AddSeconds(5);
        while((Get(components.Panel,"_cache") as BrainstormingCache)?.Session.Scope!=scope && DateTimeOffset.UtcNow<deadline)await Task.Delay(5);
        Assert.Equal(scope,(Get(components.Panel,"_cache") as BrainstormingCache)?.Session.Scope);
    }
    private static ServiceProvider Services(Components components)=>new ServiceCollection().AddLogging().AddSingleton<IComponentActivator>(components).AddSingleton<IJSRuntime,Js>().BuildServiceProvider();
    private static LocalBrainstormingStore Store()=>new(Path.Combine(Path.GetTempPath(),"BrainstormingP05",Guid.NewGuid().ToString()));

    [Theory][InlineData(false)][InlineData(true)]
    public async Task OpeningAutomaticallyPreparesCheckedContextOnceAndRetainsMessage(bool fail) {
        await using var f=new BrainstormingConversationTests.Fixture();await f.Start();var files=Store();var remote=new NoRemote();var sessions=new BrainstormingSessionService(files,remote);
        var local=f.Send.Scope with{Space=BrainstormingIdentitySpace.Local};BrainstormingScope? chosen=null;int localReads=0,serverReads=0;
        Func<BrainstormingTargetQuery,IReadOnlyList<BrainstormingContextChoice>,IReadOnlyList<Guid>,int,Task<BrainstormingPreparation>> server=async(query,choices,support,budget)=>{
            serverReads++;Assert.Equal(f.Data.Section,query.SectionId);
            if(fail)throw new InvalidDataException("Save and synchronize this manuscript before preparing server context.");
            var result=new BrainstormingPreparation(1,new(1,f.Target,null,[],[],[]),f.Context with{Scope=chosen!},[]);
            await sessions.RememberPreparationAsync(chosen!,result);return result;
        };
        var components=new Components();using var services=Services(components);await using var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async()=>{
            var html=await renderer.RenderComponentAsync<BrainstormingWorkspace>(ParameterView.FromDictionary(new Dictionary<string,object?>{
                {"Active",true},{"DocumentId",local.DocumentId},{"SectionId",f.Data.Section},{"Store",files},{"Sessions",sessions},{"Capabilities",new Capabilities()},
                {"Workflow",new BrainstormingConversationWorkflow(files,remote,async(s,_,ct)=>(await files.ReadAsync(s,ct))!)},
                {"ScopeFactory",(Func<Task<BrainstormingScope>>)(()=>Task.FromResult(local))},
                {"SessionChanged",EventCallback.Factory.Create<BrainstormingScope?>(new object(),s=>chosen=s)},
                {"PrepareServer",server},{"Prepare",(Func<BrainstormingTargetQuery,IReadOnlyList<BrainstormingContextChoice>,IReadOnlyList<Guid>,int,Task<BrainstormingPreparation>>)((q,c,s,b)=>{localReads++;throw new Exception("The primary send preparation must use checked server context.");})}}));
            Render(components.Workspace);await Loaded(components,local);
            Set(components.Panel,"_composer","My story idea\nÅsa and her brother");await Call(components.Panel,"SaveComposer");Render(components.Panel);
            Assert.DoesNotContain("Prepare context for sending",html.ToHtmlString());
            Assert.DoesNotContain("Save, sync &amp; prepare context",html.ToHtmlString());
            await Call(components.Panel,"Send");Assert.Equal(0,remote.Sends);
            await AfterRender(components.Workspace);Render(components.Workspace);Render(components.Panel);
            await AfterRender(components.Workspace);
            Assert.Equal(1,serverReads);Assert.Equal(0,localReads);Assert.Equal(0,remote.Sends);
            Assert.Equal("My story idea\nÅsa and her brother",(await files.ReadAsync(local))!.Composer);
            Assert.Equal("My story idea\nÅsa and her brother",Get(components.Panel,"_composer"));
            Assert.Equal(!fail,components.Panel.Ready);Assert.False((bool)Get(components.Panel,"_busy")!);
            Set(components.Workspace,"Active",false);await Call(components.Workspace,"OnParametersSetAsync");
            await AfterRender(components.Workspace);Assert.Equal(1,serverReads);
            Set(components.Workspace,"Active",true);await Call(components.Workspace,"OnParametersSetAsync");
            Render(components.Workspace);await AfterRender(components.Workspace);Render(components.Workspace);
            Assert.Equal(2,serverReads);Assert.Equal(0,remote.Sends);
            Assert.Equal("My story idea\nÅsa and her brother",(await files.ReadAsync(local))!.Composer);
            if(fail){Assert.Contains("Save and synchronize this manuscript",html.ToHtmlString());await Call(components.Panel,"Send");Assert.Equal(0,remote.Sends);}
            else {
                var output=html.ToHtmlString();Assert.Contains(">Send message</button>",output);
                Assert.DoesNotContain("disabled>Send message</button>",output);
                Assert.True(output.IndexOf(">Send message</button>",StringComparison.Ordinal)<output.IndexOf("Microphone dictation",StringComparison.Ordinal));
                // A failed refresh must revoke even a previously successful readiness check.
                fail=true;await Call(components.Review,"Refresh");Render(components.Workspace);Assert.False(components.Panel.Ready);
            }
        });
    }

    [Fact]
    public async Task PreSendWritingChangeRevokesReadinessAndShowsPreparationWithoutDispatchOrMessageLoss() {
        await using var f=new BrainstormingConversationTests.Fixture();await f.Start();var files=Store();var remote=new NoRemote();var sessions=new BrainstormingSessionService(files,remote);
        var local=f.Send.Scope with {Space=BrainstormingIdentitySpace.Local};int reviews=0;
        var components=new Components();using var services=Services(components);await using var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async()=>{
            var html=await renderer.RenderComponentAsync<BrainstormingWorkspace>(ParameterView.FromDictionary(new Dictionary<string,object?>{
                {"Active",true},{"DocumentId",local.DocumentId},{"SectionId",f.Data.Section},{"Store",files},{"Sessions",sessions},{"Workflow",new BrainstormingConversationWorkflow(files,remote,(s,_,ct)=>files.ReadAsync(s,ct)!)},{"Capabilities",new Capabilities()},
                {"ScopeFactory",(Func<Task<BrainstormingScope>>)(()=>Task.FromResult(local))},
                {"BeforeSend",(Func<Task>)(()=>throw new InvalidDataException("Writing changed during the last editor flush. Prepare current context."))},
                {"PrepareServer",(Func<BrainstormingTargetQuery,IReadOnlyList<BrainstormingContextChoice>,IReadOnlyList<Guid>,int,Task<BrainstormingPreparation>>)(async(q,c,s,b)=>{
                    reviews++;var preparation=new BrainstormingPreparation(1,new(1,f.Target with {Id=Guid.NewGuid()},null,[],[],[f.Data.Section]),f.Context with {Id=Guid.NewGuid(),Scope=local},[]);
                    await sessions.RememberPreparationAsync(local,preparation);return preparation;
                })}}));
            Render(components.Workspace);await Loaded(components,local);
            Set(components.Panel,"_composer","Preserve my follow-up and the original conversation");await Call(components.Panel,"SaveComposer");
            await AfterRender(components.Workspace);Render(components.Workspace);Assert.True(components.Panel.Ready);
            await Call(components.Panel,"Send");Render(components.Workspace);Render(components.Panel);
            Assert.False(components.Panel.Ready);Assert.Contains("Story context needs review",html.ToHtmlString());Assert.DoesNotContain("Prepare context for sending",html.ToHtmlString());
            var cache=(await files.ReadAsync(local))!;Assert.Equal("Preserve my follow-up and the original conversation",cache.Composer);Assert.Null(cache.PendingGeneration);Assert.Empty(cache.Session.Requests);Assert.Equal(0,remote.Sends);
            await Call(components.Panel,"PrepareForSend");Render(components.Workspace);Assert.True(components.Panel.Ready);Assert.Equal(2,reviews);Assert.Equal(0,remote.Sends);
        });
    }

    [Fact]
    public async Task PrimaryContextButtonUsesServerWithoutMutatingItsLocalDelegate() {
        int localReads=0,serverReads=0;
        var components=new Components();using var services=Services(components);await using var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async()=>{
            Func<BrainstormingTargetQuery,IReadOnlyList<BrainstormingContextChoice>,IReadOnlyList<Guid>,int,Task<BrainstormingPreparation>> local=(q,c,s,b)=>{localReads++;throw new InvalidDataException("Local review only");};
            await renderer.RenderComponentAsync<BrainstormingContextReview>(ParameterView.FromDictionary(new Dictionary<string,object?>{
                {"Prepare",local},{"PrepareServer",(Func<BrainstormingTargetQuery,IReadOnlyList<BrainstormingContextChoice>,IReadOnlyList<Guid>,int,Task<BrainstormingPreparation>>)((q,c,s,b)=>{serverReads++;throw new InvalidDataException("Save and synchronize first");})}}));
            await Call(components.Review,"Refresh");Assert.Equal(1,serverReads);Assert.Equal(0,localReads);Assert.Same(local,components.Review.Prepare);
            await Call(components.Review,"RefreshLocal");Assert.Equal(1,localReads);Assert.Equal(1,serverReads);
        });
    }

    [Fact]
    public async Task InspectionLocksOtherConversationActionsUntilOriginalCacheExchangeFinishes() {
        await using var f=new BrainstormingConversationTests.Fixture();await f.Start();var files=Store();var remote=new NoRemote();var local=f.Send.Scope with {Space=BrainstormingIdentitySpace.Local};
        await files.WriteAsync(new(1,1,BrainstormingPersistence.Empty(local),"Unsent author text",new(1,local,f.Send.Scope,1,DateTimeOffset.UtcNow),null,null,[],[]),0,Guid.NewGuid());
        var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);int exchanges=0;var busy=new List<bool>();
        var components=new Components();using var services=Services(components);await using var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async()=>{
            await renderer.RenderComponentAsync<BrainstormingConversationPanel>(ParameterView.FromDictionary(new Dictionary<string,object?>{
                {"Scope",local},{"Ready",true},{"Store",files},{"Workflow",new BrainstormingConversationWorkflow(files,remote,async(s,_,ct)=>{exchanges++;entered.SetResult();await release.Task;return (await files.ReadAsync(s,ct))!;})},
                {"BusyChanged",EventCallback.Factory.Create<bool>(new object(),v=>busy.Add(v))}}));
            var inspecting=Call(components.Panel,"Inspect");await entered.Task;Assert.True((bool)Get(components.Panel,"_busy")!);await Call(components.Panel,"Inspect");await Call(components.Panel,"Send");Assert.Equal(1,exchanges);Assert.Equal(0,remote.Sends);
            release.SetResult();await inspecting;Assert.False((bool)Get(components.Panel,"_busy")!);Assert.Equal(new[]{true,false},busy);
        });Assert.Equal("Unsent author text",(await files.ReadAsync(local))!.Composer);
    }

    [Theory][InlineData("same")][InlineData("account")][InlineData("backend")]
    public async Task HostRemountResumesOnlySameOwnedCachedSessionWithoutSendingOrReusingContext(string change) {
        await using var f=new BrainstormingConversationTests.Fixture();await f.Start();var files=Store();var remote=new NoRemote();var sessions=new BrainstormingSessionService(files,remote);var local=f.Send.Scope with {Space=BrainstormingIdentitySpace.Local};
        await files.WriteAsync(new(1,1,BrainstormingPersistence.Empty(local),"Retained author message",null,null,null,[],[]),0,Guid.NewGuid());
        var partition=change switch {"account"=>local with {AccountKey=new('C',64)},"backend"=>local with {BackendKey=new('D',64)},_=>local};
        BrainstormingScope? chosen=null;var components=new Components();using var services=Services(components);await using var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async()=>{
            var html=await renderer.RenderComponentAsync<BrainstormingWorkspace>(ParameterView.FromDictionary(new Dictionary<string,object?>{
                {"Active",true},{"DocumentId",local.DocumentId},{"ResumeScope",local},{"Store",files},{"Sessions",sessions},{"Capabilities",new Capabilities()},
                {"Workflow",new BrainstormingConversationWorkflow(files,remote,async(s,_,ct)=>(await files.ReadAsync(s,ct))!)},{"ScopeFactory",(Func<Task<BrainstormingScope>>)(()=>Task.FromResult(partition))},
                {"SessionChanged",EventCallback.Factory.Create<BrainstormingScope?>(new object(),s=>chosen=s)}}));
            Assert.Equal(change=="same"?local:null,chosen);Assert.False((bool)Get(components.Workspace,"_reviewReady")!);Assert.Equal(0,remote.Sends);
            if(change=="same"){await Loaded(components,local);Assert.Contains("Retained author message",html.ToHtmlString());}else Assert.DoesNotContain("Retained author message",html.ToHtmlString());
        });Assert.Equal("Retained author message",(await files.ReadAsync(local))!.Composer);
    }

    [Theory][InlineData(BrainstormingCapabilityState.Available)][InlineData(BrainstormingCapabilityState.Offline)][InlineData(BrainstormingCapabilityState.UnsupportedServer)]
    public async Task NewReopenAndSceneChangePreserveComposerAndNeverDispatch(BrainstormingCapabilityState availability) {
        await using var f=new BrainstormingConversationTests.Fixture();await f.Start();var files=Store();var remote=new NoRemote();var sessions=new BrainstormingSessionService(files,remote);
        BrainstormingScope? chosen=null;var local=f.Send.Scope with{Space=BrainstormingIdentitySpace.Local};
        var components=new Components();using var services=Services(components);await using var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async()=>{
            var html=await renderer.RenderComponentAsync<BrainstormingWorkspace>(ParameterView.FromDictionary(new Dictionary<string,object?>{
                {"Active",true},{"DocumentId",local.DocumentId},{"SectionId",f.Data.Section},{"Store",files},{"Sessions",sessions},{"Capabilities",new Capabilities(availability)},
                {"Workflow",new BrainstormingConversationWorkflow(files,remote,async(s,_,ct)=>(await files.ReadAsync(s,ct))!)},
                {"ScopeFactory",(Func<Task<BrainstormingScope>>)(()=>Task.FromResult(local with{SessionId=Guid.NewGuid()}))},
                {"SessionChanged",EventCallback.Factory.Create<BrainstormingScope?>(new object(),s=>chosen=s)}}));
            Assert.Contains("Make room for your next idea",html.ToHtmlString());
            await Call(components.Workspace,"CreateSession");Render(components.Workspace);var first=chosen!;await Loaded(components,first);
            Set(components.Panel,"_composer","Opening ideas\nÅsa 日本語 <img src=x onerror=evil()>" );await Call(components.Panel,"SaveComposer");
            var retainedPanel = components.Panel;
            var retainedReview = components.Review;
            var viewField = typeof(BrainstormingWorkspace).GetField("_view", BindingFlags.Instance | BindingFlags.NonPublic)!;
            foreach (var view in new[] { "Context", "History", "Chat" }) {
                viewField.SetValue(components.Workspace, Enum.Parse(viewField.FieldType, view));
                Render(components.Workspace);
                Assert.Same(retainedPanel, components.Panel);
                Assert.Same(retainedReview, components.Review);
                Assert.Equal("Opening ideas\nÅsa 日本語 <img src=x onerror=evil()>", Get(components.Panel,"_composer"));
                Assert.Equal(0, remote.Sends);
            }
            var context=f.Context with{Scope=first};await sessions.RememberPreparationAsync(first,new(1,new(1,f.Target,null,[],[],[]),context,[]));
            await (Task)typeof(BrainstormingWorkspace).GetMethod("Reviewed",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(components.Workspace,[new BrainstormingPreparation(1,new(1,f.Target,null,[],[],[]),context,[])])!;
            Assert.True((bool)Get(components.Workspace,"_reviewReady")!);
            Set(components.Workspace,"SectionId",Guid.NewGuid());await Call(components.Workspace,"OnParametersSetAsync");Assert.False((bool)Get(components.Workspace,"_reviewReady")!);
            Assert.Equal(f.Target,(await files.ReadAsync(first))!.Session.Targets.Single());
            await Call(components.Workspace,"CreateSession");Render(components.Workspace);await Loaded(components,chosen!);Assert.NotEqual(first.SessionId,chosen!.SessionId);
            Set(components.Workspace,"_selected",first.SessionId.ToString());await Call(components.Workspace,"Reopen");Render(components.Workspace);await Loaded(components,first);
            Assert.Equal(first,chosen);Assert.Equal("Opening ideas\nÅsa 日本語 <img src=x onerror=evil()>",Get(components.Panel,"_composer"));
            Assert.False((bool)Get(components.Workspace,"_reviewReady")!);Assert.Equal(0,remote.Sends);Assert.Contains("&lt;img",html.ToHtmlString());
            Assert.True((bool)Get(components.Review,"_needsRefresh")!);
            var restored=(Dictionary<Guid,BrainstormingContextChoice>)Get(components.Review,"_choices")!;
            foreach(var source in context.Sources)Assert.Equal(source.Included,restored[source.Id].Enabled);
            Set(components.Workspace,"Active",false);await Call(components.Workspace,"OnParametersSetAsync");Set(components.Workspace,"Active",true);await Call(components.Workspace,"OnParametersSetAsync");Assert.Equal(first,chosen);
            // A host document change must release old busy/private state while retaining its durable cache.
            Set(components.Workspace,"_generating",true);Set(components.Workspace,"Active",false);Set(components.Workspace,"DocumentId",Guid.NewGuid());await Call(components.Workspace,"OnParametersSetAsync");Render(components.Workspace);
            Assert.Null(chosen);Assert.False((bool)Get(components.Workspace,"_generating")!);Assert.DoesNotContain("Opening ideas",html.ToHtmlString());
            Assert.Equal("Opening ideas\nÅsa 日本語 <img src=x onerror=evil()>",(await files.ReadAsync(first))!.Composer);
        });
    }
    [Theory][InlineData("quota")][InlineData("offline")][InlineData("source changed")]
    public async Task SendFailureKeepsMultilineComposerAndCachedInspectionDoesNotGenerate(string failure) {
        await using var f=new BrainstormingConversationTests.Fixture();await f.Start();var local=f.Send.Scope with{Space=BrainstormingIdentitySpace.Local};var files=Store();var remote=new NoRemote();
        await files.WriteAsync(new(1,1,BrainstormingPersistence.Empty(local),"First line\nSecond line 🧭",null,null,null,[],[]),0,Guid.NewGuid());
        var components=new Components();using var services=Services(components);await using var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async()=>{
            var html=await renderer.RenderComponentAsync<BrainstormingConversationPanel>(ParameterView.FromDictionary(new Dictionary<string,object?>{
                {"Scope",local},{"Ready",true},{"Store",files},{"Workflow",new BrainstormingConversationWorkflow(files,remote,async(s,_,ct)=>(await files.ReadAsync(s,ct))!)},
                {"BeforeSend",(Func<Task>)(()=>throw new InvalidDataException(failure))}}));
            await Call(components.Panel,"Send");Render(components.Panel);Assert.Equal("First line\nSecond line 🧭",(await files.ReadAsync(local))!.Composer);Assert.False((bool)Get(components.Panel,"_busy")!);Assert.Equal(0,remote.Sends);
            if(failure=="quota")Assert.Contains("Quota unavailable",html.ToHtmlString());
            await Call(components.Panel,"Inspect");Assert.Equal(0,remote.Sends);
        });
    }
    [Fact]
    public async Task SharedPaneChangesKeepSameEditorAndExportProductionShell() {
        await using var f=new BrainstormingConversationTests.Fixture();await f.Start();f.Provider.Wire=BrainstormingPersistence.Json(new BrainstormingAnswer("A restrained opening.",1,BrainstormingIntent.Draft,new("Åsa stopped. <img src=x onerror=evil()>","Understated dialogue",[]),[],[]));await f.Run();
        var session=(await new BrainstormingSessions(f.Db,"author",f.Data.Backend).ReadAsync(f.Data.Document,f.Send.Scope.SessionId,default))!;var local=session.Scope with{Space=BrainstormingIdentitySpace.Local};session=BrainstormingPersistence.ReScope(session,local) with{State=BrainstormingConversationState.LocalOnly};
        var turns=session.Turns.ToList();for(int n=0;n<60;n++)turns.Add(new(1,Guid.NewGuid(),local,turns.Count+1,turns.Last().Id,Guid.NewGuid(),null,null,BrainstormingRole.User,BrainstormingTurnState.Unsent,$"Message {n} 日本語\nSecond line",DateTimeOffset.UtcNow));
        var cache=new BrainstormingCache(1,1,session with{Turns=turns},"Unsent next question\nKeep this text",null,null,null,[],[]);var files=Store();await files.WriteAsync(cache,0,Guid.NewGuid());var remote=new NoRemote();var components=new Components();using var services=Services(components);await using var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async()=>{
            RenderFragment main=b=>{b.OpenComponent<RetainedEditor>(0);b.CloseComponent();};
            RenderFragment proposal=b=>{b.OpenComponent<BrainstormingProposalPreview>(0);b.AddAttribute(1,"Cache",cache);b.AddAttribute(2,"ActiveSectionId",Guid.NewGuid());b.AddAttribute(3,"DocumentId",local.DocumentId);b.CloseComponent();};
            RenderFragment conversation=b=>{b.OpenComponent<BrainstormingConversationPanel>(0);b.AddAttribute(1,"Scope",local);b.AddAttribute(2,"Store",files);b.AddAttribute(3,"Workflow",new BrainstormingConversationWorkflow(files,remote,async(s,_,ct)=>(await files.ReadAsync(s,ct))!));b.AddAttribute(4,"Availability",new BrainstormingCapability(BrainstormingCapabilityState.Offline,"Offline"));b.AddAttribute(5,"ShowDrafts",false);b.CloseComponent();};
            var html=await renderer.RenderComponentAsync<EditorWorkspace>(ParameterView.FromDictionary(new Dictionary<string,object?>{{"Main",main},{"Proposal",proposal},{"Brainstorming",conversation},{"Outline",(RenderFragment)(b=>b.AddMarkupContent(0,"<button type='button'>Chapter 1</button><button type='button'>Chapter 2</button>"))},{"BrainstormOpen",false}}));
            Assert.Equal(1,components.Editors);Set(components.Shell,"BrainstormOpen",true);Render(components.Shell);Assert.Equal(1,components.Editors);
            Assert.Contains("brainstorm-draft-host",html.ToHtmlString());
            Assert.Equal("draft",Get(components.Shell,"_pane"));
            foreach(var pane in new[]{"manuscript","draft","outline","conversation","draft"}) {
                typeof(EditorWorkspace).GetMethod("SelectPane",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(components.Shell,[pane]);
                Render(components.Shell);Assert.Equal(1,components.Editors);
                Assert.Contains("retained-editor",html.ToHtmlString());
            }
            Set(components.Shell,"_pane","conversation");Render(components.Shell);Set(components.Shell,"_outlineCollapsed",true);Render(components.Shell);Set(components.Shell,"BrainstormOpen",false);Render(components.Shell);Assert.Equal(1,components.Editors);
            Set(components.Shell,"BrainstormOpen",true);Set(components.Shell,"_outlineCollapsed",false);Set(components.Shell,"_pane","manuscript");Render(components.Shell);
            Assert.Contains("another section",html.ToHtmlString());Assert.Contains("Show earlier messages",html.ToHtmlString());Assert.DoesNotContain("<img",html.ToHtmlString());
            if(Environment.GetEnvironmentVariable("WRITERAPP_BRAINSTORMING_CONTEXT_HTML") is {Length:>0} path)await File.WriteAllTextAsync(Path.Combine(Path.GetDirectoryName(path)!,"workspace-shell.html"),html.ToHtmlString());
        });
    }

    [Fact]
    public async Task NewlySelectedProseRevealsDraftFromManuscriptWithoutResettingUnchangedPane() {
        var components=new Components();using var services=Services(components);
        await using var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async()=>{
            var html=await renderer.RenderComponentAsync<EditorWorkspace>(ParameterView.FromDictionary(new Dictionary<string,object?>{
                {"BrainstormOpen",true},{"Main",(RenderFragment)(b=>b.AddContent(0,"Original manuscript"))},
                {"Proposal",(RenderFragment)(b=>b.AddContent(0,"Generated prose"))}}));
            void Refresh() {
                typeof(EditorWorkspace).GetMethod("OnParametersSet",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(components.Shell,[]);
                Render(components.Shell);
            }
            void Manuscript() {
                typeof(EditorWorkspace).GetMethod("SelectPane",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(components.Shell,["manuscript"]);
                Render(components.Shell);
            }
            Manuscript();Assert.Contains("data-brainstorm-pane=\"manuscript\"",html.ToHtmlString());
            Set(components.Shell,"ProposalId",Guid.NewGuid());Refresh();
            Assert.Contains("data-brainstorm-pane=\"draft\"",html.ToHtmlString());
            Manuscript();Refresh();
            Assert.Contains("data-brainstorm-pane=\"manuscript\"",html.ToHtmlString());
            Set(components.Shell,"ProposalId",Guid.NewGuid());Refresh();
            Assert.Contains("data-brainstorm-pane=\"draft\"",html.ToHtmlString());
        });
    }

    [Theory][InlineData("Device")][InlineData("Web")]
    public async Task GeneratedProseIsReadableInConversationAndDraftActionWorksInBothHosts(string surface) {
        await using var f=new BrainstormingConversationTests.Fixture();await f.Start();
        const string prose="The sergeant stopped beside Anna. <img src=x onerror=evil()>\n\nShe explained their research.";
        f.Provider.Wire=BrainstormingPersistence.Json(new BrainstormingAnswer("The scene is ready for review.",1,BrainstormingIntent.Draft,new(prose,"",[]),[],[]));
        await f.Run();var session=(await new BrainstormingSessions(f.Db,"author",f.Data.Backend).ReadAsync(f.Data.Document,f.Send.Scope.SessionId,default))!;
        var local=session.Scope with{Space=BrainstormingIdentitySpace.Local};var cache=new BrainstormingCache(1,1,BrainstormingPersistence.ReScope(session,local),"Retained follow-up",null,null,null,[],[]);
        var files=Store();await files.WriteAsync(cache,0,Guid.NewGuid());var before=BrainstormingPersistence.Json(await files.ReadAsync(local));
        var remote=new NoRemote();var components=new Components();using var services=Services(components);
        await using var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async()=>{
            var host=surface=="Device"?(object)new WriterApp.Device.Shared.Pages.DocumentWorkspace():new WriterApp.Client.Pages.DocumentEditor();
            RenderFragment conversation=b=>{
                b.OpenComponent<BrainstormingWorkspace>(0);b.AddAttribute(1,"Active",true);b.AddAttribute(2,"DocumentId",local.DocumentId);
                b.AddAttribute(3,"ResumeScope",local);b.AddAttribute(4,"Store",files);b.AddAttribute(5,"Sessions",new BrainstormingSessionService(files,remote));
                b.AddAttribute(6,"Workflow",new BrainstormingConversationWorkflow(files,remote,async(s,_,ct)=>(await files.ReadAsync(s,ct))!));
                b.AddAttribute(7,"Capabilities",new Capabilities());b.AddAttribute(8,"ScopeFactory",(Func<Task<BrainstormingScope>>)(()=>Task.FromResult(local)));
                b.AddAttribute(9,"ShowDraftRequested",EventCallback.Factory.Create(new object(),()=>Call(host,"ShowBrainstormDraft")));b.CloseComponent();
            };
            var html=await renderer.RenderComponentAsync<EditorWorkspace>(ParameterView.FromDictionary(new Dictionary<string,object?>{
                {"BrainstormOpen",true},{"Main",(RenderFragment)(b=>b.AddContent(0,"Original manuscript"))},
                {"Proposal",(RenderFragment)(b=>b.AddContent(0,prose))},{"Brainstorming",conversation}}));
            Set(host,"_brainstormEditorWorkspace",components.Shell);
            typeof(EditorWorkspace).GetMethod("SelectPane",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(components.Shell,["manuscript"]);Render(components.Shell);
            Assert.Contains("View working draft",html.ToHtmlString());Assert.Contains("Generated prose",html.ToHtmlString());
            Assert.Contains("&lt;img",html.ToHtmlString());Assert.DoesNotContain("<img src=x",html.ToHtmlString());
            Assert.Contains("The sergeant stopped beside Anna",html.ToHtmlString());
            await components.Panel.ShowDraftRequested.InvokeAsync();
            Assert.Contains("data-brainstorm-pane=\"draft\"",html.ToHtmlString());
        });
        Assert.Equal(before,BrainstormingPersistence.Json(await files.ReadAsync(local)));Assert.Equal(0,remote.Sends);Assert.Equal(1,f.Provider.Calls);
    }
}
