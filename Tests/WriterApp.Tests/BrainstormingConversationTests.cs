using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WriterApp.AI.Abstractions;
using WriterApp.Application.AI;
using WriterApp.Application.Continuity;
using WriterApp.Application.Subscriptions;
using WriterApp.Application.Usage;
using WriterApp.Data;
using WriterApp.Data.Usage;
using WriterApp.Device.Shared.Storage;
using WriterApp.Shared.Brainstorming;
using Xunit;

namespace WriterApp.Tests;

public sealed class BrainstormingConversationTests
{
    internal sealed class Provider : IAiStreamingProvider,IAiBillingProvider,IAiRouter,IAiUsagePolicy,IAiQuotaService,IUsageMeter,IBibleStore,IEntitlementService
    {
        public string Wire=BrainstormingPersistence.Json(new BrainstormingAnswer("Three openings: a missing letter, a stranger, or a promise.",1,BrainstormingIntent.Explore,null,[],[]));
        public string Mode="complete"; public bool Allowed=true,QuotaAllowed=true,Charge=true;public int Calls,Charges,Records;public AiRequest? Request;public Func<Task>? BeforeComplete;
        public string ProviderId=>"deterministic-brainstorming-test";public AiProviderCapabilities Capabilities=>new(true,false);public AiStreamingCapabilities StreamingCapabilities=>new(true,false);public bool RequiresEntitlement=>true;public bool IsBillable=>true;
        public AiProviderSelection Route(AiRequest request)=>new(this,ProviderId,false,"test");
        public Task<AiUsageDecision> EvaluateAsync(IAiProvider provider,string action)=>Task.FromResult(new AiUsageDecision(Allowed,"author",Allowed?null:"quota",Allowed?null:"AI is not authorized."));
        public Task<AiResult> ExecuteAsync(AiRequest request,CancellationToken ct){Calls++;Request=request;return Task.FromResult(new AiResult(request.RequestId,[new(Guid.NewGuid(),AiModality.Text,"application/json",Wire,null,null)],new(10,20,TimeSpan.Zero),new(){{"model","test"}}));}
        public async IAsyncEnumerable<AiStreamEvent> StreamAsync(AiRequest request,[EnumeratorCancellation]CancellationToken ct) {
            Calls++;Request=request;yield return new AiStreamEvent.Started();
            foreach(var chunk in Wire.Chunk(17)){ct.ThrowIfCancellationRequested();yield return new AiStreamEvent.TextDelta(new string(chunk));}
            if(Mode=="disconnect")throw new IOException("disconnected");
            if(Mode=="wait")await Task.Delay(Timeout.Infinite,ct);
            if(Mode=="fail"){yield return new AiStreamEvent.Failed("test failed");yield break;}
            if(BeforeComplete is not null)await BeforeComplete();
            yield return new AiStreamEvent.Completed(new(10,20,TimeSpan.Zero),"test");
        }
        public Task<AiQuotaDecision> EnsureAiAllowedAsync(string user,int estimate,CancellationToken ct)=>Task.FromResult(new AiQuotaDecision(QuotaAllowed,null,"Quota blocked.",new("Pro",1_000_000,0,DateTimeOffset.UtcNow),null));
        public Task<AiQuotaChargeResult> ChargeActualUsageAsync(string user,AiRequest request,AiResult result,CancellationToken ct){Charges++;return Task.FromResult(new AiQuotaChargeResult(Charge,30,new("Pro",1_000_000,30,DateTimeOffset.UtcNow),null,null,null));}
        public Task RecordAsync(UsageEvent item){Records++;Assert.Equal(Request!.RequestId,item.CorrelationId);return Task.CompletedTask;}
        public Task<UsageSnapshot> GetCurrentPeriodAsync(string user,string kind)=>throw new NotSupportedException();public Task<UsageSnapshot> GetRangeAsync(string user,string kind,DateTime start,DateTime end)=>throw new NotSupportedException();
        public Task<BibleSnapshotState?> GetSnapshotAsync(Guid document,BibleType kind,CancellationToken ct)=>Task.FromResult<BibleSnapshotState?>(null);
        public Task<BibleSnapshotState> UpsertSnapshotAsync(Guid doc,BibleType kind,string text,string hash,BibleRefreshCursor cursor,BibleRefreshStats stats,CancellationToken ct)=>throw new Exception("No canon mutation allowed.");
        public Task<UserEntitlements> GetEntitlementsAsync(string user)=>Task.FromResult(new UserEntitlements(user,"Professional","Professional",new Dictionary<string,string>()));public PlanTier GetUserTier(UserEntitlements e)=>PlanTier.Professional;
        public Task<bool> HasAsync(string user,string key)=>Task.FromResult(true);public Task<int?> GetIntAsync(string user,string key)=>Task.FromResult<int?>(1_000_000);public void InvalidateForUser(string user){}
    }
    internal sealed class Fixture : IAsyncDisposable
    {
        public readonly BrainstormingPersistenceTests.Database Data=new();public readonly Provider Provider=new();public readonly BrainstormingGenerationStops Stops=new();public AppDbContext Db=default!;public BrainstormingSend Send=default!;public BrainstormingContext Context=default!;public BrainstormingTarget Target=default!;
        public async Task Start(bool migrated=false) {
            await Data.Start(migrated);Db=new(Data.Options);var version=await Db.DocumentSyncRecords.SingleOrDefaultAsync();if(version is null)Db.DocumentSyncRecords.Add(new(){DocumentId=Data.Document,OwnerUserId="author",Version="v1"});else{version.OwnerUserId="author";version.Version="v1";}await Db.SaveChangesAsync();
            var scope=Data.Scope();var preparation=await Sources.PrepareAsync(new(1,scope,new(SectionId:Data.Section),[],[],60_000),default);Context=preparation.Preparation.Context!;Target=preparation.Preparation.Resolution.Target!;
            var session=BrainstormingPersistence.Empty(scope) with {State=BrainstormingConversationState.Saved,Contexts=[Context],Targets=[Target]};await new BrainstormingSessions(Db,"author",Data.Backend).WriteAsync(new(1,Guid.NewGuid(),0,session),default);
            Send=new(1,scope,Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),1,Context.Id,Target.Id,null,"Explore three openings without changing writing.");
        }
        public BrainstormingGenerationContext Sources=>new(Db,Provider,Provider,"author");
        public BrainstormingConversationService Service(bool stream=true,int timeout=180)=>new(Db,Provider,Provider,Provider,Provider,Options.Create(new WriterAiOptions{Enabled=true,Streaming=new(){Enabled=stream,BrainstormingTimeoutSeconds=timeout}}),Stops,Sources,"author",Data.Backend);
        public async Task<List<BrainstormingConversationEvent>> Run(BrainstormingSend? send=null,bool stream=true,int timeout=180){var items=new List<BrainstormingConversationEvent>();await foreach(var item in Service(stream,timeout).SendAsync(send??Send,default))items.Add(item);return items;}
        public async ValueTask DisposeAsync(){if(Db is not null)await Db.DisposeAsync();await Data.DisposeAsync();}
    }
    [Theory][InlineData(true)][InlineData(false)]
    public async Task InitialSectionWithModelContinuationFlagBecomesAReviewableDraftWithoutChangingManuscript(bool stream) {
        await using var f=new Fixture();await f.Start();
        f.Send=f.Send with {Instruction="add section where anna sitting still sitting in the kitchen decides she need to report what they found to her management, but hesitant because of what the this might start and what  the conseqeunces are. she still call her boss and then go to bed."};
        const string prose="Anna sat quietly in the kitchen, weighing what reporting their discovery could set in motion.\n\nShe called her boss despite her hesitation, then went to bed.";
        f.Provider.Wire=System.Text.Json.JsonSerializer.Serialize(new BrainstormingAnswer("A new section is ready for review.",1,BrainstormingIntent.Draft,new(prose,"Reflective scene",[],true,null),[],[]),BrainstormingConversationContract.ResponseJson);
        var before=(await f.Db.Pages.SingleAsync()).Content;
        Assert.Null(f.Send.ParentDraftId);
        // This was the precise rejected flag combination in the retained live response.
        var events=await f.Run(stream:stream);
        Assert.Equal(BrainstormingEventKind.Completed,events[^1].Kind);
        Assert.False(events[^1].Output!.Draft!.Continuation);
        var session=(await new BrainstormingSessions(f.Db,"author",f.Data.Backend).ReadAsync(f.Data.Document,f.Send.Scope.SessionId,default))!;
        var draft=Assert.Single(session.Drafts);
        Assert.Equal(prose,draft.PlainText);Assert.Null(draft.ParentDraftId);
        Assert.Equal(BrainstormingTurnState.Complete,session.Turns.Last().State);
        Assert.Empty(session.Applications);Assert.Equal(before,(await f.Db.Pages.SingleAsync()).Content);
        var retained=await f.Service().InspectAsync(f.Send.Scope,f.Send.OperationId,default);
        Assert.Equal(f.Provider.Wire,retained!.InspectionText);
        Assert.Equal(BrainstormingUsageState.Settled,retained.Usage.State);
        await f.Run(stream:stream);Assert.Equal(1,f.Provider.Calls);Assert.Equal(1,f.Provider.Charges);
        // A first proposal still continues the manuscript as a whole when kept.
        // The internal continuation flag controls proposal composition only.
        var writing=await new BrainstormingStorySourceReader(f.Db,"author").ReadAsync(f.Data.Document);
        var review=BrainstormingKeepContract.Review(session.Scope,draft,f.Target,BrainstormingKeepContract.Writing(writing));
        review=BrainstormingPlacements.ReviewAt(review,BrainstormingPlacements.Default(review),"Kitchen decision");
        Assert.Equal(BrainstormingPlacementMode.ContinueSection,review.Placement!.Mode);
        var keeps=new BrainstormingKeeps(f.Db,"author",f.Data.Backend);
        await keeps.PrepareAsync(review);
        Assert.Equal(BrainstormingApplicationState.Committed,(await keeps.CommitAsync(review.Scope,review.OperationId)).State);
        var saved=BrainstormingDraftContent.PlainText((await f.Db.Pages.AsNoTracking().SingleAsync()).Content);
        Assert.StartsWith(BrainstormingDraftContent.PlainText(before)+"\n\n",saved);
        Assert.EndsWith(prose,saved);
    }
    [Theory][InlineData("revise")][InlineData("rewrite-authority")]
    public async Task MissingParentStillCannotAuthorizeRewritingExistingProse(string mode) {
        await using var f=new Fixture();await f.Start();
        f.Send=f.Send with {Instruction="Rewrite the existing scene."};
        f.Provider.Wire=System.Text.Json.JsonSerializer.Serialize(new BrainstormingAnswer("Replacement.",1,
            mode=="revise"?BrainstormingIntent.Revise:BrainstormingIntent.Draft,
            new("Changed prose.","",[],true,mode=="rewrite-authority"?f.Send.Instruction:null),[],[]),BrainstormingConversationContract.ResponseJson);
        var before=(await f.Db.Pages.SingleAsync()).Content;
        Assert.Equal(BrainstormingEventKind.Failed,(await f.Run())[^1].Kind);
        var session=(await new BrainstormingSessions(f.Db,"author",f.Data.Backend).ReadAsync(f.Data.Document,f.Send.Scope.SessionId,default))!;
        Assert.Empty(session.Drafts);Assert.Empty(session.Applications);
        Assert.Equal(before,(await f.Db.Pages.SingleAsync()).Content);
    }
    [Theory][InlineData(true)][InlineData(false)]
    public async Task ExplorationUsesCheckedDataAndSavesConversationWithoutMutatingWriting(bool stream) {
        await using var f=new Fixture();await f.Start(true);var before=(await f.Db.Pages.SingleAsync()).Content;var events=await f.Run(stream:stream);
        Assert.Equal(BrainstormingEventKind.Accepted,events[0].Kind);Assert.Equal(BrainstormingEventKind.Completed,events[^1].Kind);Assert.Equal(BrainstormingIntent.Explore,events[^1].Output!.Intent);
        long order=0;foreach(var e in events){BrainstormingConversationContract.ValidateEvent(e,f.Send,order);order++;}
        using var data=System.Text.Json.JsonDocument.Parse(f.Provider.Request!.Inputs["conversation_data"].ToString()!);Assert.Contains("Åsa",data.RootElement.GetProperty("context").GetProperty("sources")[0].GetProperty("text").GetString());Assert.Contains("quoted data",f.Provider.Request.Inputs["conversation_data"].ToString());
        var session=(await new BrainstormingSessions(f.Db,"author",f.Data.Backend).ReadAsync(f.Data.Document,f.Send.Scope.SessionId,default))!;
        Assert.Equal(2,session.Turns.Count);Assert.Empty(session.Drafts);Assert.Equal(before,(await f.Db.Pages.SingleAsync()).Content);Assert.Empty(await f.Db.AiActionHistoryEntries.ToListAsync());Assert.Equal(1,f.Provider.Charges);Assert.Equal(1,f.Provider.Records);
        var replay=await f.Run();Assert.Equal(BrainstormingPersistence.Json(events),BrainstormingPersistence.Json(replay));Assert.Equal(1,f.Provider.Calls);Assert.Equal(1,f.Provider.Charges);
        await Assert.ThrowsAsync<InvalidDataException>(()=>f.Run(f.Send with {Instruction="Changed duplicate"}));
    }
    [Fact]
    public async Task DraftAndRevisionHaveSeparateAnswerTypedProseAndPinnedProvenance() {
        await using var f=new Fixture();await f.Start();f.Provider.Wire=BrainstormingPersistence.Json(new BrainstormingAnswer("I will keep the dialogue understated.",1,BrainstormingIntent.Draft,new("Åsa hesitated. 日本語 🧭","Suspicious; restrained dialogue",["A stranger arrived"]),[],["A planning note conflicts with the synopsis."]));
        await f.Run();var session=(await new BrainstormingSessions(f.Db,"author",f.Data.Backend).ReadAsync(f.Data.Document,f.Send.Scope.SessionId,default))!;var draft=Assert.Single(session.Drafts);
        Assert.Equal(f.Context.Id,draft.ContextId);Assert.Equal(f.Target.Id,draft.TargetId);Assert.DoesNotContain("I will",draft.PlainText);Assert.Null(draft.EditorHtml);Assert.Empty(session.Applications);
        f.Provider.Wire=BrainstormingPersistence.Json(new BrainstormingAnswer("Here is a more suspicious opening.",1,BrainstormingIntent.Revise,new("Åsa stepped back.","Understated dialogue",[],RevisionInstruction:"Make her more suspicious."),[],[]));
        var next=f.Send with {OperationId=Guid.NewGuid(),RequestId=Guid.NewGuid(),UserTurnId=Guid.NewGuid(),AssistantTurnId=Guid.NewGuid(),ExpectedRevision=session.Revision,ParentDraftId=draft.Id,Instruction="Make her more suspicious."};await f.Run(next);
        session=(await new BrainstormingSessions(f.Db,"author",f.Data.Backend).ReadAsync(f.Data.Document,f.Send.Scope.SessionId,default))!;Assert.Equal(2,session.Drafts.Count);Assert.Equal(draft.Id,session.Drafts[1].ParentDraftId);using var data=System.Text.Json.JsonDocument.Parse(f.Provider.Request!.Inputs["conversation_data"].ToString()!);Assert.Equal(draft.PlainText,data.RootElement.GetProperty("previousDraft").GetProperty("plainText").GetString());
        await Assert.ThrowsAsync<InvalidDataException>(()=>new BrainstormingSessions(f.Db,"author",f.Data.Backend).WriteAsync(new(1,Guid.NewGuid(),session.Revision,session with {Revision=session.Revision+1,Requests=session.Requests.Select(r=>r with {State=BrainstormingGenerationState.Failed}).ToArray()}),default));
    }
    [Theory][InlineData(BrainstormingIntent.Clarify)][InlineData(BrainstormingIntent.ProposePlanning)]
    public async Task ClarificationAndOwnedPlanningRemainInertPersistedConversation(BrainstormingIntent intent) {
        await using var f=new Fixture();await f.Start();
        f.Db.DocumentSynopses.Add(new(){DocumentId=f.Data.Document,EndingIntent="Her brother learns in Chapter 4.",UpdatedAt=DateTimeOffset.UtcNow});await f.Db.SaveChangesAsync();
        var fresh=await f.Sources.PrepareAsync(new(1,f.Send.Scope,new(SectionId:f.Data.Section),[],[],60_000),default);
        f.Context=fresh.Preparation.Context!;f.Target=fresh.Preparation.Resolution.Target!;
        var sessions=new BrainstormingSessions(f.Db,"author",f.Data.Backend);
        var original=(await sessions.ReadAsync(f.Data.Document,f.Send.Scope.SessionId,default))!;
        await sessions.WriteAsync(new(1,Guid.NewGuid(),original.Revision,original with{Revision=original.Revision+1,Contexts=[..original.Contexts,f.Context],Targets=[..original.Targets,f.Target]}),default);
        f.Send=f.Send with{ExpectedRevision=original.Revision+1,ContextId=f.Context.Id,TargetId=f.Target.Id,Instruction=intent==BrainstormingIntent.Clarify?"What detail do you need?":"Suggest a revised ending intent."};
        var source=f.Context.Sources.Single(s=>s.Kind==BrainstormingSourceKind.Synopsis && s.Included);
        f.Provider.Wire=BrainstormingPersistence.Json(new BrainstormingAnswer("Please review this idea.",1,intent,null,
            intent==BrainstormingIntent.ProposePlanning?[new(source.Id,BrainstormingPlanningKind.SynopsisField,f.Data.Document.ToString(),"EndingIntent","Her brother learns in Chapter 4.","Her brother learns in Chapter 5.")]:[],[]));
        string before=(await f.Db.Pages.SingleAsync()).Content;var events=await f.Run();Assert.Equal(BrainstormingEventKind.Completed,events[^1].Kind);
        var result=(await sessions.ReadAsync(f.Data.Document,f.Send.Scope.SessionId,default))!;Assert.Empty(result.Drafts);Assert.Empty(result.Applications);Assert.Empty(result.Approvals);
        Assert.Equal(intent==BrainstormingIntent.ProposePlanning?1:0,result.Planning.Count);
        if(result.Planning.Count!=0){Assert.Equal(f.Context.Id,result.Planning[0].ContextId);Assert.Equal(source.Source,result.Planning[0].Source);}
        Assert.Equal(before,(await f.Db.Pages.SingleAsync()).Content);Assert.Equal("Her brother learns in Chapter 4.",(await f.Db.DocumentSynopses.SingleAsync()).EndingIntent);
    }
    [Theory][InlineData("malformed")][InlineData("partial")][InlineData("tool")][InlineData("disconnect")][InlineData("quota-settlement")][InlineData("fail")][InlineData("missing-contradictions")]
    public async Task InvalidInterruptedAndUncertainOutputCannotBecomeAnApplicableProposal(string mode) {
        await using var f=new Fixture();await f.Start();
        if(mode=="malformed")f.Provider.Wire="{bad}";if(mode=="partial")f.Provider.Wire="{\"answer\":\"Incomplete\",\"version\":1";if(mode=="tool")f.Provider.Wire="{\"tool_call\":\"write_manuscript\"}";
        if(mode=="missing-contradictions")f.Provider.Wire="""{"answer":"A passage is ready.","version":1,"intent":"Draft","draft":{"text":"The drill stopped. Anna saw something beneath the ice.","constraints":"","introducedFacts":[]},"planning":[]}""";
        if(mode is "disconnect" or "fail")f.Provider.Mode=mode;if(mode=="quota-settlement")f.Provider.Charge=false;
        var events=await f.Run();Assert.Equal(BrainstormingEventKind.Failed,events[^1].Kind);Assert.Null(events[^1].Output);
        var session=(await new BrainstormingSessions(f.Db,"author",f.Data.Backend).ReadAsync(f.Data.Document,f.Send.Scope.SessionId,default))!;Assert.Empty(session.Drafts);Assert.All(session.Turns.Where(t=>t.Role==BrainstormingRole.Assistant),t=>Assert.NotEqual(BrainstormingTurnState.Complete,t.State));
        var inspection=await f.Service().InspectAsync(f.Send.Scope,f.Send.OperationId,default);Assert.Equal(f.Provider.Wire,inspection!.InspectionText);
        var assistant=Assert.Single(session.Turns,t=>t.Role==BrainstormingRole.Assistant);
        Assert.Equal(BrainstormingConversationContract.UnfinishedTurnMessage(assistant.State),assistant.Text);
        Assert.NotEqual(f.Provider.Wire,assistant.Text);
        if(mode=="missing-contradictions") {Assert.Equal(BrainstormingTurnState.Failed,assistant.State);Assert.Contains("response that could not be validated",inspection.Message);}
        if(mode is "disconnect" or "quota-settlement") {Assert.Equal(BrainstormingGenerationState.Unknown,inspection.State);await Assert.ThrowsAsync<BrainstormingConcurrencyException>(()=>f.Run(f.Send with {OperationId=Guid.NewGuid(),ExpectedRevision=session.Revision}));}
        await f.Run();Assert.Equal(1,f.Provider.Calls);Assert.Equal(1,f.Provider.Charges);
    }
    [Theory][InlineData("policy")][InlineData("quota")][InlineData("stale")][InlineData("forged")][InlineData("account")][InlineData("deleted")]
    public async Task DispatchRefusesQuotaUnauthorizedForgedAndStaleContext(string mode) {
        await using var f=new Fixture();await f.Start();
        if(mode=="policy")f.Provider.Allowed=false;if(mode=="quota")f.Provider.QuotaAllowed=false;
        if(mode=="stale"){(await f.Db.Pages.SingleAsync()).Content="<p>Intervening writing</p>";await f.Db.SaveChangesAsync();}
        if(mode=="deleted"){(await f.Db.Documents.SingleAsync()).DeletedAtUtc=DateTime.UtcNow;await f.Db.SaveChangesAsync();}
        if(mode=="forged") {var root=await f.Db.BrainstormingSessions.SingleAsync();var s=BrainstormingValidation.ReadSession(Encoding.UTF8.GetBytes(root.SnapshotJson));root.SnapshotJson=BrainstormingPersistence.Json(s with {Contexts=[f.Context with {Sources=f.Context.Sources.Select(source=>source.Included?source with {Text="Invented current manuscript"}:source).ToArray(),Policy=f.Context.Policy! with {CharactersUsed=f.Context.Sources.Where(s=>s.Included).Sum(s=>s.Title.Length+s.Reason.Length+"Invented current manuscript".Length)}}]});await f.Db.SaveChangesAsync();}
        var send=mode=="account"?f.Send with {Scope=f.Send.Scope with {AccountKey=BrainstormingValidation.Fingerprint("other")}}:f.Send;
        await Assert.ThrowsAnyAsync<Exception>(()=>f.Run(send));Assert.Equal(0,f.Provider.Calls);Assert.Equal(0,f.Provider.Charges);Assert.Empty(await f.Db.BrainstormingGenerations.ToListAsync());
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task StopAndTimeoutRetainIncompleteOutputAndRejectLateCompletion(bool timeout) {
        await using var f=new Fixture();await f.Start();f.Provider.Mode="wait";
        var items=new List<BrainstormingConversationEvent>();await foreach(var e in f.Service(timeout:timeout?1:180).SendAsync(f.Send,default)) {
            items.Add(e);if(!timeout&&e.Kind==BrainstormingEventKind.DraftPending)await f.Service().StopAsync(f.Send.Scope,f.Send.OperationId,default);
        }
        Assert.Equal(BrainstormingEventKind.Cancelled,items[^1].Kind);Assert.Null(items[^1].Output);Assert.Equal(1,f.Provider.Charges);
        var s=(await new BrainstormingSessions(f.Db,"author",f.Data.Backend).ReadAsync(f.Data.Document,f.Send.Scope.SessionId,default))!;Assert.Empty(s.Drafts);
    }
    [Fact]
    public async Task RestartAfterAcceptedOperationInspectsUnknownWithoutReissuingOrCharging() {
        await using var f=new Fixture();await f.Start();var iterator=f.Service().SendAsync(f.Send,default).GetAsyncEnumerator();Assert.True(await iterator.MoveNextAsync());
        // A second process has no stop handle. It can inspect/replay the durable acceptance, never dispatch.
        var isolated=new BrainstormingConversationService(f.Db,f.Provider,f.Provider,f.Provider,f.Provider,Options.Create(new WriterAiOptions()),new(),f.Sources,"author",f.Data.Backend);
        var state=await isolated.InspectAsync(f.Send.Scope,f.Send.OperationId,default);Assert.Equal(BrainstormingGenerationState.Unknown,state!.State);
        var replay=new List<BrainstormingConversationEvent>();await foreach(var e in isolated.SendAsync(f.Send,default))replay.Add(e);
        Assert.Single(replay);Assert.Equal(0,f.Provider.Calls);Assert.Equal(0,f.Provider.Charges);await iterator.DisposeAsync();
    }
    [Fact]
    public async Task ConcurrentGenerationIsBlockedAndExplicitNewRetryIsAllowedOnlyAfterKnownTerminalFailure() {
        await using var f=new Fixture();await f.Start();await using(var first=f.Service().SendAsync(f.Send,default).GetAsyncEnumerator()) {
            Assert.True(await first.MoveNextAsync());await Assert.ThrowsAsync<BrainstormingConcurrencyException>(()=>f.Run(f.Send with {OperationId=Guid.NewGuid(),ExpectedRevision=2}));Assert.Equal(0,f.Provider.Calls);
        }
        var session=(await new BrainstormingSessions(f.Db,"author",f.Data.Backend).ReadAsync(f.Data.Document,f.Send.Scope.SessionId,default))!;
        Assert.Equal(BrainstormingGenerationState.Failed,session.Requests[0].State);
        var retry=f.Send with {OperationId=Guid.NewGuid(),RequestId=Guid.NewGuid(),UserTurnId=Guid.NewGuid(),AssistantTurnId=Guid.NewGuid(),ExpectedRevision=session.Revision};await f.Run(retry);Assert.Equal(1,f.Provider.Calls);Assert.Equal(1,f.Provider.Charges);
    }
    [Fact]
    public async Task RevokedManuscriptAccessDuringCompletionRetainsFailureAndCannotReturnAPreviewAsAProposal() {
        await using var f=new Fixture();await f.Start();f.Provider.BeforeComplete=async()=>{(await f.Db.Documents.SingleAsync()).OwnerUserId="other";await f.Db.SaveChangesAsync();};var events=await f.Run();Assert.Equal(BrainstormingEventKind.Failed,events[^1].Kind);Assert.Null(events[^1].Output);
        var ledger=System.Text.Json.JsonSerializer.Deserialize<BrainstormingGeneration>((await f.Db.BrainstormingGenerations.SingleAsync()).SnapshotJson,BrainstormingValidation.Json)!;Assert.Equal(BrainstormingGenerationState.Failed,ledger.State);
        await Assert.ThrowsAsync<BrainstormingAccessException>(()=>f.Service().InspectAsync(f.Send.Scope,f.Send.OperationId,default));
    }
    private sealed class Handler(Func<HttpRequestMessage,HttpResponseMessage> send):HttpMessageHandler {protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)=>Task.FromResult(send(request));}
    [Fact]
    public async Task LateProviderCompletionAfterStopCannotProduceACompleteTurn() {
        await using var f=new Fixture();await f.Start();f.Provider.BeforeComplete=()=>f.Service().StopAsync(f.Send.Scope,f.Send.OperationId,default);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>f.Run());
        var value=await f.Service().InspectAsync(f.Send.Scope,f.Send.OperationId,default);Assert.Equal(BrainstormingGenerationState.Cancelled,value!.State);Assert.Null(value.Events[^1].Output);
    }
    [Theory][InlineData("valid")][InlineData("duplicate")][InlineData("reordered")][InlineData("changed-duplicate")][InlineData("disconnect")][InlineData("auth-loss")][InlineData("late")]
    public async Task RealHttpTransportEnforcesIdentityOrderingDedupAndTerminalBoundaries(string mode) {
        await using var f=new Fixture();await f.Start();var events=await f.Run();var source=events.ToList();bool signed=true;
        if(mode=="duplicate")source.Insert(1,source[0]);if(mode=="changed-duplicate")source.Insert(1,source[0] with {Text="changed"});if(mode=="reordered")source[0]=source[0] with {Sequence=2};if(mode=="disconnect")source.RemoveAt(source.Count-1);if(mode=="late")source.Add(source[1] with {Sequence=source.Count+1});
        using var http=new HttpClient(new Handler(_=>new(HttpStatusCode.OK){Content=new StringContent(string.Join('\n',source.Select(BrainstormingPersistence.Json))+"\n",Encoding.UTF8,"application/x-ndjson")})){BaseAddress=new("https://test.invalid/")};
        var remote=new BrainstormingConversationHttp(http,_=>new(()=>{if(!signed)throw new OperationCanceledException("Authentication lost.");}));
        async Task Run(){int n=0;await foreach(var e in remote.SendAsync(f.Send)){n++;if(mode=="auth-loss")signed=false;}Assert.Equal(events.Count,n);}
        if(mode is "valid" or "duplicate")await Run();else await Assert.ThrowsAnyAsync<Exception>(Run);
    }
    [Fact]
    public async Task PendingSendSurvivesLocalRestartAndInspectionNeverAutomaticallyDispatches() {
        await using var f=new Fixture();await f.Start();var local=f.Send.Scope with {Space=BrainstormingIdentitySpace.Local};var store=new LocalBrainstormingStore(Path.Combine(Path.GetTempPath(),"BrainstormingSend",Guid.NewGuid().ToString()));
        var session=BrainstormingPersistence.Empty(local) with {Contexts=[f.Context with {Scope=local}],Targets=[f.Target],State=BrainstormingConversationState.Saved};
        var cache=new BrainstormingCache(1,1,session,"Preserve unsent",new(1,local,f.Send.Scope,1,DateTimeOffset.UtcNow),null,new(1,Guid.NewGuid(),f.Send.Scope,1,new('A',64),DateTimeOffset.UtcNow),[],[],PendingGeneration:f.Send);
        await store.WriteAsync(cache,0,Guid.NewGuid());var reopened=(await store.ReadAsync(local))!;Assert.Equal(f.Send,reopened.PendingGeneration);
        Assert.Equal("Preserve unsent",reopened.Composer);
        var workflow=new BrainstormingConversationWorkflow(store,new NeverSend(),async(scope,_,ct)=>(await store.ReadAsync(scope,ct))!);
        await Assert.ThrowsAsync<InvalidDataException>(async()=>{await foreach(var _ in workflow.SendAsync(local,"Retry",null)){} });
        var inspected=await workflow.InspectAsync(local);Assert.Null(inspected.PendingGeneration);Assert.Equal("Preserve unsent",inspected.Composer);
    }
    private sealed class NeverSend:IBrainstormingConversation {
        public IAsyncEnumerable<BrainstormingConversationEvent> SendAsync(BrainstormingSend send,CancellationToken ct=default)=>throw new Exception("No automatic provider request.");
        public Task<BrainstormingGeneration?> InspectAsync(BrainstormingScope scope,Guid op,CancellationToken ct=default)=>Task.FromResult<BrainstormingGeneration?>(null);
        public Task StopAsync(BrainstormingScope scope,Guid op,CancellationToken ct=default)=>Task.CompletedTask;
    }
}
