using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;
using WriterApp.Application.AI;
using WriterApp.Client.Pages;
using WriterApp.Device.Shared.Pages;
using WriterApp.Device.Shared.Storage;
using WriterApp.Shared.Brainstorming;
using WriterApp.UI.Shared;
using Xunit;

namespace WriterApp.Tests;

public sealed class BrainstormingDraftTests
{
    [Theory][InlineData("Web")][InlineData("Device")]
    public async Task StoryBeatsAppendByDefaultAndExplicitRewriteReplacesTheCompleteDraft(string surface) {
        await using var f=new BrainstormingConversationTests.Fixture();await f.Start();
        var store=Store();var cache=await Draft(f,store);var scope=cache.Session.Scope;
        var remote=new Remote(f);var sessions=new BrainstormingSessionService(store,remote);
        var first=cache.Session.Drafts.Single();var manuscript=(await f.Db.Pages.SingleAsync()).Content;
        const string opening="<p><strong>Anna</strong> steadied the drill.</p>";
        await sessions.SaveDraftEditAsync(scope,first.Id,BrainstormingValidation.CandidateFingerprint(first),opening,BrainstormingDraftContent.PlainText(opening));
        cache=await sessions.CheckpointDraftAsync(scope);
        var components=new Components();using var services=new ServiceCollection().AddLogging().AddSingleton<IComponentActivator>(components).BuildServiceProvider();
        await using var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async()=>{
            await renderer.RenderComponentAsync<BrainstormingConversationPanel>(ParameterView.FromDictionary(new Dictionary<string,object?>{
                {"Scope",scope},{"Store",store},{"Sessions",sessions},{"Ready",true},
                {"Workflow",new BrainstormingConversationWorkflow(store,remote,(s,pull,ct)=>pull?sessions.PullAsync(s,Map(),()=>{},ct):sessions.SyncAsync(s,Map(),()=>{},ct))}}));
            async Task Send(string instruction,string text,string? revision=null) {
                // Deliberately reproduce the faulty model flag; ordinary story beats
                // still append because no current-author rewrite clause is supplied.
                var wire=System.Text.Json.Nodes.JsonNode.Parse(BrainstormingPersistence.Json(new BrainstormingAnswer("Editable prose is ready.",1,BrainstormingIntent.Revise,new(text,"",[],false,revision),[],[])))!;
                if(surface=="Web")wire["draft"]!["continuation"]=false;
                f.Provider.Wire=wire.ToJsonString();
                typeof(BrainstormingConversationPanel).GetField("_composer",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(components.Panel,instruction);
                await Call(components.Panel,"Send");cache=(await store.ReadAsync(scope))!;
                Assert.Equal(BrainstormingGenerationState.Completed,cache.LastGeneration!.State);
            }
            await Send("Add text now the military patrol stops by Anna and her team.","The patrol stopped beside Anna. She explained their research.");
            var patrol=BrainstormingDrafts.Candidate(cache)!;
            await Send("The sergeant looks at her for a moment and then says they had better finish and move away. The patrol leaves on their scooters.","The sergeant studied Anna, then let them finish. The patrol left on their scooters.");
            var departure=BrainstormingDrafts.Candidate(cache)!;
            Assert.Equal("Anna steadied the drill.\n\nThe patrol stopped beside Anna. She explained their research.\n\nThe sergeant studied Anna, then let them finish. The patrol left on their scooters.",departure.PlainText);
            Assert.StartsWith(opening,departure.EditorHtml);Assert.Equal(patrol.Id,departure.ParentDraftId);
            Assert.Single(BrainstormingDrafts.Branches(cache.Session));
            using var data=JsonDocument.Parse(f.Provider.Request!.Inputs["conversation_data"].ToString()!);
            Assert.Equal(patrol.PlainText,data.RootElement.GetProperty("previousDraft").GetProperty("plainText").GetString());
            var rendered=await renderer.RenderComponentAsync<BrainstormingProposalPreview>(ParameterView.FromDictionary(new Dictionary<string,object?>{{"Cache",cache},{"DocumentId",scope.DocumentId},{"Sessions",sessions},{"EditorHost",surface}}));
            Assert.Contains("Anna",rendered.ToHtmlString());Assert.Contains("The patrol stopped",rendered.ToHtmlString());Assert.Contains("The sergeant studied",rendered.ToHtmlString());
            const string rewrite="Write a new version of the current text with more tension.";
            await Send(rewrite,"Anna gripped the drill as the patrol stopped. After a tense exchange, the sergeant let them finish and left.",rewrite);
            var revised=BrainstormingDrafts.Candidate(cache)!;
            Assert.Equal("Anna gripped the drill as the patrol stopped. After a tense exchange, the sergeant let them finish and left.",revised.PlainText);
            Assert.Equal(departure.Id,revised.ParentDraftId);Assert.Equal(BrainstormingPersistence.Json(departure),BrainstormingPersistence.Json(cache.Session.Drafts.Single(d=>d.Id==departure.Id)));
            Assert.Equal(5,cache.Session.Drafts.Count);Assert.Equal(manuscript,(await f.Db.Pages.SingleAsync()).Content);
            Assert.Empty(cache.Session.Applications);Assert.Empty(await f.Db.AiActionHistoryEntries.ToListAsync());
            var reopened=await new BrainstormingSessionService(store,remote).ReadAsync(scope);
            Assert.Equal(revised.Id,BrainstormingDrafts.Candidate(reopened!)!.Id);Assert.Equal(departure.PlainText,reopened!.Session.Drafts.Single(d=>d.Id==departure.Id).PlainText);
            // Keeping the accumulated draft commits both requested sections, even
            // after a later rewrite: the retained candidate remains reviewable.
            var cloud=(await remote.ReadAsync(f.Send.Scope))!;var candidate=cloud.Drafts.Single(d=>d.Id==departure.Id);
            var source=await new BrainstormingStorySourceReader(f.Db,"author").ReadAsync(f.Data.Document);
            var review=BrainstormingKeepContract.Review(cloud.Scope,candidate,cloud.Targets.Single(t=>t.Id==candidate.TargetId),BrainstormingKeepContract.Writing(source));
            var keeps=new BrainstormingKeeps(f.Db,"author",f.Data.Backend);await keeps.PrepareAsync(review);
            Assert.Equal(BrainstormingApplicationState.Committed,(await keeps.CommitAsync(review.Scope,review.OperationId)).State);
            var saved=(await f.Db.Pages.AsNoTracking().SingleAsync()).Content;
            Assert.Contains(opening,saved);Assert.True(saved.IndexOf("The patrol stopped",StringComparison.Ordinal)<saved.IndexOf("The sergeant studied",StringComparison.Ordinal));
        });
    }

    [Theory][InlineData("invented")][InlineData("empty")][InlineData("selection")][InlineData("contradictory")]
    public async Task InvalidRewriteAuthorityCannotDiscardTheReviewedDraft(string mode) {
        await using var f=new BrainstormingConversationTests.Fixture();await f.Start();var cache=await Draft(f,Store());var original=cache.Session.Drafts.Single();
        var selection=new BrainstormingDraftSelection(1,original.Id,BrainstormingValidation.CandidateFingerprint(original),BrainstormingDrafts.TextMapping,0,3,"Åsa");
        var send=f.Send with{OperationId=Guid.NewGuid(),RequestId=Guid.NewGuid(),UserTurnId=Guid.NewGuid(),AssistantTurnId=Guid.NewGuid(),ExpectedRevision=cache.Acknowledgement!.Revision,
            ParentDraftId=original.Id,Instruction="Change her answer.",Selection=mode=="selection"?selection:null};
        string? quote=mode switch{"invented"=>"Rewrite the scene.","empty"=>" ","selection"=>null,_=>send.Instruction};
        f.Provider.Wire=BrainstormingPersistence.Json(new BrainstormingAnswer("New prose.",1,BrainstormingIntent.Revise,new("A replacement fragment.","",[],mode=="contradictory",quote),[],[]));
        var events=await f.Run(send);Assert.Equal(BrainstormingEventKind.Failed,events[^1].Kind);
        var cloud=(await new Remote(f).ReadAsync(f.Send.Scope))!;Assert.Equal(original.Id,cloud.Drafts.Single().Id);Assert.Equal(original.PlainText,cloud.Drafts.Single().PlainText);
    }

    [Fact]
    public async Task ContinuousDraftAlternativesAndRestoreRetainEveryVersionAcrossSync() {
        await using var f=new BrainstormingConversationTests.Fixture();await f.Start();var store=Store();var cache=await Draft(f,store);
        var scope=cache.Session.Scope;var remote=new Remote(f);var sessions=new BrainstormingSessionService(store,remote);var first=cache.Session.Drafts.Single();
        Assert.DoesNotContain("branchId",BrainstormingPersistence.Json(first));
        const string rich="<p><strong>Anna</strong> steadied the drill.</p>";
        await sessions.SaveDraftEditAsync(scope,first.Id,BrainstormingValidation.CandidateFingerprint(first),rich,BrainstormingDraftContent.PlainText(rich));
        cache=await sessions.CheckpointDraftAsync(scope);var edited=BrainstormingDrafts.Candidate(cache)!;
        f.Provider.Wire=BrainstormingPersistence.Json(new BrainstormingAnswer("Continued the scene.",1,BrainstormingIntent.Revise,new("A distant engine broke the silence.","",[],true),[],[]));
        var flow=new BrainstormingConversationWorkflow(store,remote,(s,pull,ct)=>pull?sessions.PullAsync(s,Map(),()=>{},ct):sessions.SyncAsync(s,Map(),()=>{},ct));
        await foreach(var _ in flow.SendAsync(scope,"Continue with the approaching patrol.",edited.Id)){}
        cache=(await store.ReadAsync(scope))!;var continued=BrainstormingDrafts.Candidate(cache)!;
        Assert.StartsWith(edited.PlainText,continued.PlainText);Assert.EndsWith("A distant engine broke the silence.",continued.PlainText);Assert.Contains("<strong>Anna</strong>",continued.EditorHtml);
        Assert.Equal(3,cache.Session.Drafts.Count);Assert.Single(BrainstormingDrafts.Branches(cache.Session));Assert.Equal(BrainstormingPersistence.Json(first),BrainstormingPersistence.Json(cache.Session.Drafts[0]));
        cache=await sessions.CreateAlternativeAsync(scope,"Quieter arrival");var alternative=BrainstormingDrafts.Candidate(cache)!;Assert.NotNull(alternative.BranchId);
        await foreach(var _ in flow.SendAsync(scope,"Continue this alternative.",alternative.Id)){}
        cache=(await store.ReadAsync(scope))!;Assert.Equal(alternative.BranchId,BrainstormingDrafts.Candidate(cache)!.BranchId);Assert.Equal(2,BrainstormingDrafts.Branches(cache.Session).Count);
        var retained=cache.Session.Drafts.ToArray();cache=await sessions.RestoreDraftAsync(scope,edited.Id);var restored=BrainstormingDrafts.Candidate(cache)!;
        Assert.Null(restored.BranchId);Assert.Equal(rich,restored.EditorHtml);Assert.Equal(BrainstormingPersistence.Json(retained),BrainstormingPersistence.Json(cache.Session.Drafts.Take(retained.Length).ToArray()));
        cache=await sessions.SyncAsync(scope,Map(),()=>{});var reopened=(await new BrainstormingSessionService(store,remote).ReadAsync(scope))!;
        Assert.Equal(6,reopened.Session.Drafts.Count);Assert.Equal(2,BrainstormingDrafts.Branches(reopened.Session).Count);Assert.Equal(restored.Id,BrainstormingDrafts.Candidate(reopened)!.Id);
        Assert.Empty(reopened.Session.Applications);
    }
    [Fact]
    public async Task WorkingDraftControlsCreateSwitchPreviewAndRestoreWithoutReplacingHistory() {
        await using var f=new BrainstormingConversationTests.Fixture();await f.Start();var store=Store();var cache=await Draft(f,store);var original=cache.Session.Drafts.Single();
        var sessions=new BrainstormingSessionService(store,new Remote(f));var components=new Components();
        using var services=new ServiceCollection().AddLogging().AddSingleton<IComponentActivator>(components).BuildServiceProvider();await using var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async()=>{
            var rendered=await renderer.RenderComponentAsync<BrainstormingProposalPreview>(ParameterView.FromDictionary(new Dictionary<string,object?>{{"Cache",cache},{"DocumentId",cache.Session.Scope.DocumentId},{"Sessions",sessions}}));
            async Task Refresh(){cache=(await store.ReadAsync(cache.Session.Scope))!;typeof(BrainstormingProposalPreview).GetProperty("Cache")!.SetValue(components.Preview,cache);await Call(components.Preview,"OnParametersSetAsync");typeof(ComponentBase).GetMethod("StateHasChanged",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(components.Preview,[]);}
            Assert.Contains("Version history",rendered.ToHtmlString());Assert.Contains("Main draft",rendered.ToHtmlString());
            await Call(components.Preview,"CreateAlternative");await Refresh();Assert.Contains("Alternative opening",rendered.ToHtmlString());Assert.Equal(2,cache.Session.Drafts.Count);
            await (Task)typeof(BrainstormingProposalPreview).GetMethod("SelectBranch",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(components.Preview,[original.Id])!;await Refresh();Assert.Equal(original.Id,BrainstormingDrafts.Candidate(cache)!.Id);
            typeof(BrainstormingProposalPreview).GetField("_historyPreview",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(components.Preview,original.Id);await Refresh();Assert.Contains("Later versions remain in history.",rendered.ToHtmlString());
            await (Task)typeof(BrainstormingProposalPreview).GetMethod("RestoreVersion",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(components.Preview,[original.Id])!;await Refresh();
            Assert.Equal(3,cache.Session.Drafts.Count);Assert.Equal(2,BrainstormingDrafts.Versions(cache.Session,null).Count);Assert.Equal(original.PlainText,BrainstormingDrafts.Candidate(cache)!.PlainText);Assert.NotEqual(original.Id,BrainstormingDrafts.Candidate(cache)!.Id);
            Assert.Empty(cache.Session.Applications);
        });
    }
    private sealed class Remote(BrainstormingConversationTests.Fixture f):IBrainstormingRemote,IBrainstormingConversation {
        private BrainstormingSessions Sessions=>new(f.Db,"author",f.Data.Backend);
        public Task<BrainstormingSession?> ReadAsync(BrainstormingScope scope,CancellationToken ct=default)=>Sessions.ReadAsync(scope.DocumentId,scope.SessionId,ct);
        public Task<BrainstormingReceipt> WriteAsync(BrainstormingWrite write,CancellationToken ct=default)=>Sessions.WriteAsync(write,ct);
        public Task<BrainstormingSessionPage> ListAsync(Guid document,Guid? after=null,int limit=25,CancellationToken ct=default)=>Sessions.ListAsync(document,after,limit,ct);
        Task<BrainstormingReceipt?> IBrainstormingRemote.InspectAsync(BrainstormingScope scope,Guid operation,CancellationToken ct)=>Sessions.InspectAsync(scope.DocumentId,scope.SessionId,operation,ct);
        public Task<BrainstormingGeneration?> InspectAsync(BrainstormingScope scope,Guid operation,CancellationToken ct=default)=>f.Service().InspectAsync(scope,operation,ct);
        public Task StopAsync(BrainstormingScope scope,Guid operation,CancellationToken ct=default)=>f.Service().StopAsync(scope,operation,ct);
        public IAsyncEnumerable<BrainstormingConversationEvent> SendAsync(BrainstormingSend send,CancellationToken ct=default)=>f.Service().SendAsync(send,ct);
    }
    private sealed class Components:IComponentActivator {
        public BrainstormingProposalPreview Preview=default!;public BrainstormingConversationPanel Panel=default!;
        public IComponent CreateInstance(Type type){var value=(IComponent)Activator.CreateInstance(type)!;if(value is BrainstormingProposalPreview p)Preview=p;if(value is BrainstormingConversationPanel panel)Panel=panel;return value;}
    }
    private static LocalBrainstormingStore Store()=>new(Path.Combine(Path.GetTempPath(),"BrainstormingP06",Guid.NewGuid().ToString()));
    private static Task Call(object instance,string method)=>(Task)instance.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(instance,[])!;
    private sealed class DelayedProposalJs:IJSRuntime,IJSObjectReference {
        public readonly TaskCompletionSource ImportStarted=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource ReleaseImport=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Creates; public Guid? CreatedCandidate;
        public ValueTask<T> InvokeAsync<T>(string name,object?[]? args)=>InvokeAsync<T>(name,default,args);
        public async ValueTask<T> InvokeAsync<T>(string name,CancellationToken ct,object?[]? args) {
            if(name=="import") {ImportStarted.TrySetResult();await ReleaseImport.Task;return (T)(object)this;}
            if(name=="create") {Creates++;CreatedCandidate=(Guid)args![4]!;return (T)(object)this;}
            return default!;
        }
        public ValueTask DisposeAsync()=>ValueTask.CompletedTask;
    }
    private static Task AfterRender(BrainstormingProposalPreview preview)=>(Task)typeof(BrainstormingProposalPreview).GetMethod("OnAfterRenderAsync",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(preview,[false])!;
    [Theory][InlineData(false)][InlineData(true)]
    public async Task OverlappingRendersCreateOnlyTheCurrentProposalEditor(bool followUpDuringImport) {
        await using var f=new BrainstormingConversationTests.Fixture();await f.Start();var store=Store();var cache=await Draft(f,store);
        var js=new DelayedProposalJs();var components=new Components();
        using var services=new ServiceCollection().AddLogging().AddSingleton<IJSRuntime>(js).AddSingleton<IComponentActivator>(components).BuildServiceProvider();
        await using var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async()=>{
            await renderer.RenderComponentAsync<BrainstormingProposalPreview>(ParameterView.FromDictionary(new Dictionary<string,object?>{{"Cache",cache},{"DocumentId",cache.Session.Scope.DocumentId},{"Sessions",new BrainstormingSessionService(store,new Remote(f))}}));
            var first=AfterRender(components.Preview);await js.ImportStarted.Task;
            var second=AfterRender(components.Preview);
            var expected=cache.Session.Drafts[^1];
            if(followUpDuringImport) {
                expected=expected with {Id=Guid.NewGuid(),PlainText="The sergeant asks Anna to leave.",EditorHtml=null};
                typeof(BrainstormingProposalPreview).GetProperty("Cache")!.SetValue(components.Preview,cache with {Session=cache.Session with {Drafts=[..cache.Session.Drafts,expected]},DraftReview=new(1,expected.Id,true)});
                await Call(components.Preview,"OnParametersSetAsync");
            }
            js.ReleaseImport.SetResult();await Task.WhenAll(first,second);
            // HtmlRenderer omits actual interactive after-render callbacks.
            await AfterRender(components.Preview);
            Assert.Equal(1,js.Creates);Assert.Equal(expected.Id,js.CreatedCandidate);
        });
    }
    private static async Task<BrainstormingCache> Draft(BrainstormingConversationTests.Fixture f,LocalBrainstormingStore store) {
        f.Provider.Wire=BrainstormingPersistence.Json(new BrainstormingAnswer("The stranger arrives without winning her trust.",1,BrainstormingIntent.Draft,new("Åsa hesitated. 日本語 🧭\n\nShe kept her distance.","Understated dialogue; third person",["The stranger carries a letter"]),[],["The note contradicts the synopsis."]));
        await f.Run();var cloud=(await new Remote(f).ReadAsync(f.Send.Scope))!;var scope=cloud.Scope with{Space=BrainstormingIdentitySpace.Local};
        var cache=new BrainstormingCache(1,1,BrainstormingPersistence.ReScope(cloud,scope),"Make her more suspicious.",new(1,scope,cloud.Scope,1,DateTimeOffset.UtcNow),null,new(1,Guid.NewGuid(),cloud.Scope,cloud.Revision,BrainstormingPersistence.Hash(cloud),DateTimeOffset.UtcNow),[],[]);
        return await store.WriteAsync(cache,0,Guid.NewGuid());
    }
    private static BrainstormingIdentityMap Map()=>new(new Dictionary<Guid,Guid>(),new Dictionary<Guid,Guid>(),new Dictionary<Guid,Guid>(),new Dictionary<Guid,Guid>(),true);
    [Theory][InlineData("Web")][InlineData("Device")]
    public async Task CreatedSceneProseReachesTheEditableProposalWithTheCheckedDestinationAndSeparateKeep(string surface) {
        await using var f=new BrainstormingConversationTests.Fixture();await f.Start();
        f.Send=f.Send with {Instruction="Add a scene where scientist anna is with her team in the arctics and drilling ice cores for environmental research"};
        const string prose="Anna steadied the drill in the Arctic wind.\n\nHer team lifted the ice core into its protective sleeve.";
        f.Provider.Wire=BrainstormingPersistence.Json(new BrainstormingAnswer("The scene is ready for review in the draft editor.",1,BrainstormingIntent.Draft,new(prose,"Arctic field research",["Anna leads the team"]),[],[]));
        var before=(await f.Db.Pages.AsNoTracking().SingleAsync()).Content;await f.Run();
        var server=(await new Remote(f).ReadAsync(f.Send.Scope))!;var local=server.Scope with {Space=BrainstormingIdentitySpace.Local};var store=Store();
        var cache=await store.WriteAsync(new(1,1,BrainstormingPersistence.ReScope(server,local),"",new(1,local,server.Scope,1,DateTimeOffset.UtcNow),null,null,[],[]),0,Guid.NewGuid());
        var candidate=Assert.Single(cache.Session.Drafts);Assert.Equal(f.Target.Id,candidate.TargetId);Assert.Equal(prose,candidate.PlainText);
        using var services=new ServiceCollection().AddLogging().BuildServiceProvider();await using var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async()=>{
            Func<Guid,bool,string?,Task<BrainstormingKeepReview>> prepareKeep=(_,_,_)=>throw new InvalidOperationException("Generation must not invoke manuscript review automatically.");
            var html=await renderer.RenderComponentAsync<BrainstormingProposalPreview>(ParameterView.FromDictionary(new Dictionary<string,object?>{{"Cache",cache},{"DocumentId",local.DocumentId},{"ActiveSectionId",f.Target.SectionId},{"EditorHost",surface},{"Sessions",new BrainstormingSessionService(store,new Remote(f))},{"PrepareKeep",prepareKeep}}));
            var content=html.ToHtmlString();Assert.Contains("brainstorm-proposal-editor",content);Assert.Contains("Anna steadied the drill",content);Assert.Contains("Review &amp; keep",content);Assert.Contains("Original destination",content);
        });
        Assert.Equal(before,(await f.Db.Pages.AsNoTracking().SingleAsync()).Content);Assert.Empty(server.Applications);Assert.Empty(await f.Db.AiActionHistoryEntries.ToListAsync());Assert.Equal(1,f.Provider.Calls);
    }
    [Theory][InlineData("Web")][InlineData("Device")]
    public async Task ReachableHostCheckpointAndSharedVersionReviewUseManualCandidateInNextGeneration(string surface) {
        await using var f=new BrainstormingConversationTests.Fixture();await f.Start();var store=Store();var cache=await Draft(f,store);var scope=cache.Session.Scope;var remote=new Remote(f);var sessions=new BrainstormingSessionService(store,remote);var original=cache.Session.Drafts.Single();var manuscript=(await f.Db.Pages.SingleAsync()).Content;
        var components=new Components();using var services=new ServiceCollection().AddLogging().AddSingleton<IComponentActivator>(components).BuildServiceProvider();await using var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async()=>{
            var html=await renderer.RenderComponentAsync<BrainstormingProposalPreview>(ParameterView.FromDictionary(new Dictionary<string,object?>{{"Cache",cache},{"DocumentId",scope.DocumentId},{"ActiveSectionId",f.Data.Section},{"Sessions",sessions},{"EditorHost",surface}}));
            if(Environment.GetEnvironmentVariable("WRITERAPP_BRAINSTORMING_CONTEXT_HTML") is {Length:>0} basePath)await File.WriteAllTextAsync(Path.Combine(Path.GetDirectoryName(basePath)!,"draft-base-"+surface.ToLowerInvariant()+".json"),BrainstormingPersistence.Json(cache));
            Assert.Contains("Captured manuscript versus proposal",html.ToHtmlString());Assert.Contains("Introduced story fact",html.ToHtmlString());Assert.Contains("Story contradiction",html.ToHtmlString());
            const string edited="<p><strong>Åsa</strong> refused. 日本語 🧭</p><p>She kept her distance.</p>";var text=BrainstormingDraftContent.PlainText(edited);
            await components.Preview.OnProposalChanged(edited,text,original.Id,BrainstormingValidation.CandidateFingerprint(original));
            var reopened=await new BrainstormingSessionService(store,remote).ReadAsync(scope);Assert.Equal(edited,reopened!.DraftEdit!.Html);Assert.Single(reopened.Session.Drafts);
            typeof(ComponentBase).GetMethod("StateHasChanged",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(components.Preview,[]);
            if(Environment.GetEnvironmentVariable("WRITERAPP_BRAINSTORMING_CONTEXT_HTML") is {Length:>0} editPath)await File.WriteAllTextAsync(Path.Combine(Path.GetDirectoryName(editPath)!,"draft-review-"+surface.ToLowerInvariant()+".html"),html.ToHtmlString());
            if(Environment.GetEnvironmentVariable("WRITERAPP_BRAINSTORMING_CONTEXT_HTML") is {Length:>0} bufferPath)await File.WriteAllTextAsync(Path.Combine(Path.GetDirectoryName(bufferPath)!,"draft-buffer-"+surface.ToLowerInvariant()+".json"),BrainstormingPersistence.Json(reopened));
            var host=surface=="Web"?(object)new DocumentEditor():new DocumentWorkspace();host.GetType().GetField("_brainstormProposal",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(host,components.Preview);
            await Call(host,"PrepareBrainstormDraftSend");cache=(await store.ReadAsync(scope))!;var manual=cache.Session.Drafts.Last();Assert.True(manual.ManuallyEdited);Assert.Equal(original.Id,manual.ParentDraftId);Assert.Equal(edited,manual.EditorHtml);Assert.Null(cache.DraftEdit);
            // Recreate the presentation from persisted cache before the actual revision request.
            typeof(BrainstormingProposalPreview).GetProperty("Cache")!.SetValue(components.Preview,cache);await Call(components.Preview,"OnParametersSetAsync");
            var from=text.IndexOf("refused",StringComparison.Ordinal);var selection=new BrainstormingDraftSelection(1,manual.Id,BrainstormingValidation.CandidateFingerprint(manual),BrainstormingDrafts.TextMapping,from,from+7,"refused");await sessions.ReviewDraftAsync(scope,manual.Id,true,selection);
            var fresh=await f.Sources.PrepareAsync(new(1,f.Send.Scope,new(SectionId:f.Data.Section),[],[],60_000),default);
            await sessions.RememberPreparationAsync(scope,new(1,fresh.Preparation.Resolution,fresh.Preparation.Context! with{Scope=scope},[]));
            f.Provider.Wire=BrainstormingPersistence.Json(new BrainstormingAnswer("She now questions the stranger.",1,BrainstormingIntent.Revise,new("Åsa questioned him.\n\nShe kept her distance.","Understated dialogue",[],RevisionInstruction:"change her answer"),[],[]));
            var flow=new BrainstormingConversationWorkflow(store,remote,(s,pull,ct)=>pull?sessions.PullAsync(s,Map(),()=>{},ct):sessions.SyncAsync(s,Map(),()=>{},ct));
            await foreach(var _ in flow.SendAsync(scope,"Keep the second paragraph; change her answer.",manual.Id,default,selection)){}
            cache=(await store.ReadAsync(scope))!;Assert.Equal(3,cache.Session.Drafts.Count);var revision=cache.Session.Drafts.Last();Assert.Equal(manual.Id,revision.ParentDraftId);Assert.Equal(original.TargetId,revision.TargetId);Assert.Equal(3,revision.Revision);Assert.Equal(revision.Id,cache.DraftReview!.CandidateId);
            using var data=JsonDocument.Parse(f.Provider.Request!.Inputs["conversation_data"].ToString()!);Assert.Equal(text,data.RootElement.GetProperty("previousDraft").GetProperty("plainText").GetString());Assert.Equal(edited,data.RootElement.GetProperty("previousDraft").GetProperty("editorHtml").GetString());Assert.Equal("refused",data.RootElement.GetProperty("selectedPassage").GetProperty("quote").GetString());
            typeof(BrainstormingProposalPreview).GetProperty("Cache")!.SetValue(components.Preview,cache);await Call(components.Preview,"OnParametersSetAsync");await (Task)typeof(BrainstormingProposalPreview).GetMethod("Move",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(components.Preview,[-1])!;cache=(await store.ReadAsync(scope))!;Assert.Equal(manual.Id,cache.DraftReview!.CandidateId);
            typeof(BrainstormingProposalPreview).GetProperty("Cache")!.SetValue(components.Preview,cache);await Call(components.Preview,"OnParametersSetAsync");await (Task)typeof(BrainstormingProposalPreview).GetMethod("Move",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(components.Preview,[1])!;cache=(await store.ReadAsync(scope))!;Assert.Equal(revision.Id,cache.DraftReview!.CandidateId);
            typeof(BrainstormingProposalPreview).GetProperty("Cache")!.SetValue(components.Preview,cache);await Call(components.Preview,"OnParametersSetAsync");
            typeof(ComponentBase).GetMethod("StateHasChanged",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(components.Preview,[]);
            if(Environment.GetEnvironmentVariable("WRITERAPP_BRAINSTORMING_CONTEXT_HTML") is {Length:>0} path)await File.WriteAllTextAsync(Path.Combine(Path.GetDirectoryName(path)!,"draft-versions-"+surface.ToLowerInvariant()+".html"),html.ToHtmlString());
            Assert.Equal(manuscript,(await f.Db.Pages.SingleAsync()).Content);Assert.Empty(cache.Session.Applications);Assert.Empty(await f.Db.AiActionHistoryEntries.ToListAsync());Assert.Equal(2,f.Provider.Calls);
        });
    }
    [Theory][InlineData("<p style='color:red'>Changed</p>","Changed")][InlineData("<script>evil()</script>","evil()")][InlineData("<p>Changed</p>","Other")][InlineData("<p><strong>broken", "broken")]
    public async Task UnsupportedOrMismatchedManualContentRetainsOriginal(string html,string text) {
        await using var f=new BrainstormingConversationTests.Fixture();await f.Start();var store=Store();var cache=await Draft(f,store);var draft=cache.Session.Drafts[0];var before=BrainstormingPersistence.Json(await store.ReadAsync(cache.Session.Scope));var sessions=new BrainstormingSessionService(store,new Remote(f));
        await Assert.ThrowsAsync<InvalidDataException>(()=>sessions.SaveDraftEditAsync(cache.Session.Scope,draft.Id,BrainstormingValidation.CandidateFingerprint(draft),html,text));Assert.Equal(before,BrainstormingPersistence.Json(await store.ReadAsync(cache.Session.Scope)));Assert.Equal(1,f.Provider.Calls);
    }
    [Theory][InlineData("quote")][InlineData("fingerprint")][InlineData("range")][InlineData("candidate")]
    public async Task ServerRejectsLostOrChangedSelectionBeforeDispatch(string mode) {
        await using var f=new BrainstormingConversationTests.Fixture();await f.Start();var cache=await Draft(f,Store());var draft=cache.Session.Drafts[0];var selection=new BrainstormingDraftSelection(1,draft.Id,BrainstormingValidation.CandidateFingerprint(draft),BrainstormingDrafts.TextMapping,0,3,"Åsa");
        selection=mode switch{"quote"=>selection with{Quote="She"},"fingerprint"=>selection with{CandidateFingerprint=new string('A',64)},"range"=>selection with{From=1},_=>selection with{CandidateId=Guid.NewGuid()}};
        var send=f.Send with{OperationId=Guid.NewGuid(),RequestId=Guid.NewGuid(),UserTurnId=Guid.NewGuid(),AssistantTurnId=Guid.NewGuid(),ExpectedRevision=cache.Acknowledgement!.Revision,ParentDraftId=draft.Id,Selection=selection};
        await Assert.ThrowsAnyAsync<Exception>(()=>f.Run(send));Assert.Equal(1,f.Provider.Calls);Assert.Equal(1,await f.Db.BrainstormingGenerations.CountAsync());
    }
    [Theory][InlineData(BrainstormingIntent.Explore)][InlineData(BrainstormingIntent.Clarify)]
    public async Task DiscussionAndAmbiguityKeepCandidateAndManuscriptUnchanged(BrainstormingIntent intent) {
        await using var f=new BrainstormingConversationTests.Fixture();await f.Start();var cache=await Draft(f,Store());var original=cache.Session.Drafts[0];var before=(await f.Db.Pages.SingleAsync()).Content;
        f.Provider.Wire=BrainstormingPersistence.Json(new BrainstormingAnswer(intent==BrainstormingIntent.Clarify?"Which paragraph do you mean?":"She could be curious about the letter.",1,intent,null,[],[]));await f.Run(f.Send with{OperationId=Guid.NewGuid(),RequestId=Guid.NewGuid(),UserTurnId=Guid.NewGuid(),AssistantTurnId=Guid.NewGuid(),ExpectedRevision=cache.Acknowledgement!.Revision,ParentDraftId=original.Id,Instruction="Why would she approach him?"});
        var session=(await new Remote(f).ReadAsync(f.Send.Scope))!;Assert.Equal(BrainstormingPersistence.Json(original with{Scope=session.Scope}),BrainstormingPersistence.Json(session.Drafts.Single()));Assert.Equal(before,(await f.Db.Pages.SingleAsync()).Content);
    }
    [Fact]
    public async Task PendingManualEditAndChangedDestinationCannotUseAnOlderCandidate() {
        await using var f=new BrainstormingConversationTests.Fixture();await f.Start();var store=Store();var cache=await Draft(f,store);var scope=cache.Session.Scope;var remote=new Remote(f);var sessions=new BrainstormingSessionService(store,remote);var parent=cache.Session.Drafts[0];
        await sessions.SaveDraftEditAsync(scope,parent.Id,BrainstormingValidation.CandidateFingerprint(parent),"<p>Manual</p>","Manual");var flow=new BrainstormingConversationWorkflow(store,remote,(_,_,_)=>throw new Exception("Must fail before exchange"));
        async Task Send(){await foreach(var _ in flow.SendAsync(scope,"Revise",parent.Id)){} }
        await Assert.ThrowsAsync<InvalidDataException>(Send);await sessions.CheckpointDraftAsync(scope);cache=(await store.ReadAsync(scope))!;var changed=cache.Session.Targets[0] with{Id=Guid.NewGuid(),Source=cache.Session.Targets[0].Source with{ContentRevision="intervening"}};
        await sessions.RememberPreparationAsync(scope,new(1,new(1,changed,null,[],[],[]),cache.Session.Contexts[0] with{Id=Guid.NewGuid()},[]));await Assert.ThrowsAsync<InvalidDataException>(Send);Assert.Equal(1,f.Provider.Calls);Assert.Equal("Manual",(await store.ReadAsync(scope))!.Session.Drafts.Last().PlainText);
    }
    [Fact]
    public async Task CancelledRevisionRetainsExistingCompleteVersions() {
        await using var f=new BrainstormingConversationTests.Fixture();await f.Start();var cache=await Draft(f,Store());var original=cache.Session.Drafts.Single();
        f.Provider.Mode="wait";f.Provider.Wire=BrainstormingPersistence.Json(new BrainstormingAnswer("Revising the opening.",1,BrainstormingIntent.Revise,new("Incomplete revision","",[]),[],[]));
        var send=f.Send with{OperationId=Guid.NewGuid(),RequestId=Guid.NewGuid(),UserTurnId=Guid.NewGuid(),AssistantTurnId=Guid.NewGuid(),ExpectedRevision=cache.Acknowledgement!.Revision,ParentDraftId=original.Id,Instruction="Revise this opening."};
        BrainstormingConversationEvent? terminal=null;await foreach(var item in f.Service().SendAsync(send,default)){terminal=item;if(item.Kind==BrainstormingEventKind.DraftPending)await f.Service().StopAsync(send.Scope,send.OperationId,default);}
        Assert.Equal(BrainstormingEventKind.Cancelled,terminal!.Kind);var cloud=(await new Remote(f).ReadAsync(send.Scope))!;Assert.Equal(original.Id,cloud.Drafts.Single().Id);Assert.Equal(2,f.Provider.Calls);Assert.Empty(cloud.Applications);
    }
    private sealed class ReviewIdentity:IBrainstormingReviewIdentity {public string Identity=>"original";public string? ActiveAccountKey=>null;public event Action? Changed;public void Change()=>Changed?.Invoke();}
    [Theory][InlineData("document")][InlineData("account")]
    public async Task LateEditorCallbackCannotAdoptIntoAnotherReview(string change) {
        await using var f=new BrainstormingConversationTests.Fixture();await f.Start();var store=Store();var cache=await Draft(f,store);var draft=cache.Session.Drafts[0];var sessions=new BrainstormingSessionService(store,new Remote(f));var identity=new ReviewIdentity();
        var components=new Components();using var services=new ServiceCollection().AddLogging().AddSingleton<IComponentActivator>(components).BuildServiceProvider();await using var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async()=>{
            var html=await renderer.RenderComponentAsync<BrainstormingProposalPreview>(ParameterView.FromDictionary(new Dictionary<string,object?>{{"Cache",cache},{"DocumentId",cache.Session.Scope.DocumentId},{"Sessions",sessions},{"ReviewIdentity",identity}}));
            if(change=="account")identity.Change();else{typeof(BrainstormingProposalPreview).GetProperty("DocumentId")!.SetValue(components.Preview,Guid.NewGuid());await Call(components.Preview,"OnParametersSetAsync");}
            await components.Preview.OnProposalChanged("<p>Late private edit</p>","Late private edit",draft.Id,BrainstormingValidation.CandidateFingerprint(draft));
            typeof(ComponentBase).GetMethod("StateHasChanged",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(components.Preview,[]);
            Assert.DoesNotContain("Åsa",html.ToHtmlString());Assert.Null((await store.ReadAsync(cache.Session.Scope))!.DraftEdit);Assert.Single((await store.ReadAsync(cache.Session.Scope))!.Session.Drafts);
        });
    }
    [Fact]
    public async Task EmptyManualCheckpointIsRefusedWithoutDiscardingRecoverableEdit() {
        await using var f=new BrainstormingConversationTests.Fixture();await f.Start();var store=Store();var cache=await Draft(f,store);var parent=cache.Session.Drafts.Single();var sessions=new BrainstormingSessionService(store,new Remote(f));
        await sessions.SaveDraftEditAsync(cache.Session.Scope,parent.Id,BrainstormingValidation.CandidateFingerprint(parent),"<p></p>","");
        await Assert.ThrowsAsync<InvalidDataException>(()=>sessions.CheckpointDraftAsync(cache.Session.Scope));var retained=(await store.ReadAsync(cache.Session.Scope))!;Assert.NotNull(retained.DraftEdit);Assert.Single(retained.Session.Drafts);Assert.Equal(parent.Id,retained.Session.Drafts[0].Id);
    }

    private sealed class RecordingConversation:IBrainstormingConversation {
        public BrainstormingSend? Captured;
        public IAsyncEnumerable<BrainstormingConversationEvent> SendAsync(BrainstormingSend send,CancellationToken ct=default){Captured=send;throw new IOException("Synthetic interrupted transport; no provider dispatch.");}
        public Task<BrainstormingGeneration?> InspectAsync(BrainstormingScope scope,Guid operation,CancellationToken ct=default)=>Task.FromResult<BrainstormingGeneration?>(null);
        public Task StopAsync(BrainstormingScope scope,Guid operation,CancellationToken ct=default)=>Task.CompletedTask;
    }
    [Theory][InlineData(true)][InlineData(false)]
    public async Task SharedSendUsesNewSceneWithoutRetargetingOldDraftButRefusesSameLocationWithChangedSource(bool newScene) {
        await using var f=new BrainstormingConversationTests.Fixture();await f.Start();var store=Store();var cache=await Draft(f,store);var parent=cache.Session.Drafts[0];var sessions=new BrainstormingSessionService(store,new Remote(f));
        var target=cache.Session.Targets[0] with{Id=Guid.NewGuid()};target=newScene?target with{SectionId=Guid.NewGuid(),PageId=Guid.NewGuid()}:target with{Source=target.Source with{ContentRevision="changed"}};
        await sessions.RememberPreparationAsync(cache.Session.Scope,new(1,new(1,target,null,[],[],[]),cache.Session.Contexts[0] with{Id=Guid.NewGuid()},[]));cache=(await store.ReadAsync(cache.Session.Scope))!;var recording=new RecordingConversation();var components=new Components();using var services=new ServiceCollection().AddLogging().AddSingleton<IComponentActivator>(components).BuildServiceProvider();await using var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async()=>{
            await renderer.RenderComponentAsync<BrainstormingConversationPanel>(ParameterView.FromDictionary(new Dictionary<string,object?>{{"Scope",cache.Session.Scope},{"Store",store},{"Ready",true},{"Workflow",new BrainstormingConversationWorkflow(store,recording,async(s,_,ct)=>(await store.ReadAsync(s,ct))!)}}));
            await Call(components.Panel,"Send");if(newScene){Assert.NotNull(recording.Captured);Assert.Null(recording.Captured.ParentDraftId);Assert.Equal(target.Id,recording.Captured.TargetId);}else Assert.Null(recording.Captured);
            Assert.Equal(parent.Id,(await store.ReadAsync(cache.Session.Scope))!.Session.Drafts.Single().Id);Assert.Equal(parent.TargetId,(await store.ReadAsync(cache.Session.Scope))!.Session.Drafts.Single().TargetId);Assert.Equal(1,f.Provider.Calls);
        });
    }

}
