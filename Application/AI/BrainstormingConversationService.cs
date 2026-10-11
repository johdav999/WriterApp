using System.Collections.Concurrent;
using System.Data;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WriterApp.AI.Abstractions;
using WriterApp.AI.Actions;
using WriterApp.Application.Usage;
using WriterApp.Data;
using WriterApp.Data.AI;
using WriterApp.Data.Usage;
using WriterApp.Shared.Brainstorming;

namespace WriterApp.Application.AI;

// Process-local stop handles supplement (never replace) the database's durable operation identity.
public sealed class BrainstormingGenerationStops
{
    private readonly ConcurrentDictionary<(Guid,Guid),CancellationTokenSource> active=new();
    public bool Add(Guid session,Guid operation,CancellationTokenSource cancellation)=>active.TryAdd((session,operation),cancellation);
    public void Remove(Guid session,Guid operation)=>active.TryRemove((session,operation),out _);
    public bool Stop(Guid session,Guid operation) { if(!active.TryGetValue((session,operation),out var source))return false;source.Cancel();return true; }
    public bool Running(Guid session,Guid operation)=>active.ContainsKey((session,operation));
}
public sealed class BrainstormingConversationService(AppDbContext db,IAiRouter router,IAiUsagePolicy policy,IAiQuotaService quota,
    IUsageMeter meter,IOptions<WriterAiOptions> options,BrainstormingGenerationStops stops,BrainstormingGenerationContext sources,string owner,string backend)
{
    private BrainstormingSessions Sessions=>new(db,owner,backend);
    private async Task<BrainstormingSession> Session(BrainstormingScope scope,CancellationToken ct) {
        var result=await Sessions.ReadAsync(scope.DocumentId,scope.SessionId,ct)??throw new BrainstormingAccessException();
        BrainstormingValidation.RequireScope(result.Scope,scope);if(result.State==BrainstormingConversationState.Deleted)throw new BrainstormingAccessException();return result;
    }
    private static BrainstormingGeneration Decode(BrainstormingGenerationRecord row)=>JsonSerializer.Deserialize<BrainstormingGeneration>(row.SnapshotJson,BrainstormingValidation.Json)!;
    public async Task<BrainstormingGeneration?> InspectAsync(BrainstormingScope scope,Guid operation,CancellationToken ct) {
        await Session(scope,ct);var row=await db.BrainstormingGenerations.AsNoTracking().SingleOrDefaultAsync(g=>g.SessionId==scope.SessionId&&g.OperationId==operation,ct);
        if(row is null)return null;var value=Decode(row);
        // A lost worker is never restarted. Unknown retains the billable reservation and blocks a new send.
        if(value.State is BrainstormingGenerationState.Accepted or BrainstormingGenerationState.Streaming && !stops.Running(scope.SessionId,operation))
            return value with { State=BrainstormingGenerationState.Unknown,Message="Generation was interrupted. Inspect the retained output; this operation will not be sent again.",Usage=value.Usage with {State=BrainstormingUsageState.Unknown} };
        return value;
    }
    public async Task StopAsync(BrainstormingScope scope,Guid operation,CancellationToken ct) {
        await Session(scope,ct); if(!stops.Stop(scope.SessionId,operation)) {
            var value=await InspectAsync(scope,operation,ct);
            if(value?.State==BrainstormingGenerationState.Unknown)throw new InvalidDataException("The provider outcome is uncertain. Stopping cannot prove usage settlement or authorize another paid request.");
        }
    }
    private async Task Save(BrainstormingGenerationRecord row,BrainstormingGeneration value,CancellationToken ct=default) {
        row.Revision++;row.SnapshotJson=BrainstormingPersistence.Json(value);await db.SaveChangesAsync(ct);
    }
    private async Task<BrainstormingGeneration> Event(BrainstormingGenerationRecord row,BrainstormingGeneration value,BrainstormingEventKind kind,string text,
        BrainstormingGenerationState state,BrainstormingAnswer? output=null) {
        if(value.Events.Count>=BrainstormingConversationContract.MaxEvents)throw new InvalidDataException("Stream event limit exceeded.");
        var e=new BrainstormingConversationEvent(1,value.Send.Scope,value.Send.OperationId,value.Send.RequestId,value.Send.AssistantTurnId,value.Events.Count+1,kind,text,output,value.Usage,state,DateTimeOffset.UtcNow);
        var next=value with {State=state,Events=[..value.Events,e],UpdatedAt=e.CreatedAt};await Save(row,next);return next;
    }
    private Task<(BrainstormingGenerationRecord Row,BrainstormingGeneration Value,bool Created)> Accept(BrainstormingSend send,BrainstormingGeneration value,CancellationToken ct)=>db.Database.CreateExecutionStrategy().ExecuteAsync(async()=>{
        // Only durable acceptance is retried. Provider dispatch and usage charging
        // must never be inside this execution strategy.
        db.ChangeTracker.Clear();await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        var session=await Session(send.Scope,ct);
        var existing=await db.BrainstormingGenerations.SingleOrDefaultAsync(g=>g.SessionId==send.Scope.SessionId&&g.OperationId==send.OperationId,ct);
        if(existing is not null) {
            if(existing.PayloadHash!=BrainstormingPersistence.Hash(send))throw new InvalidDataException("Generation operation payload changed.");
            // An acceptance commit may have succeeded without its acknowledgment.
            // Replay its durable events for inspection; never redispatch it.
            return (existing,Decode(existing),false);
        }
        if(session.Revision!=send.ExpectedRevision || session.Requests.Any(r=>r.State is BrainstormingGenerationState.Accepted or BrainstormingGenerationState.Streaming or BrainstormingGenerationState.Unknown))throw new BrainstormingConcurrencyException(session);
        var root=await db.BrainstormingSessions.SingleAsync(s=>s.Id==send.Scope.SessionId,ct);
        var turn=new BrainstormingTurn(1,send.UserTurnId,send.Scope,session.Turns.Count+1,session.Turns.LastOrDefault()?.Id,send.OperationId,send.RequestId,send.ContextId,BrainstormingRole.User,BrainstormingTurnState.Accepted,send.Instruction,DateTimeOffset.UtcNow);
        var next=session with {Revision=session.Revision+1,UpdatedAt=turn.CreatedAt,Turns=[..session.Turns,turn],Requests=[..session.Requests,new(1,send.RequestId,send.Scope,send.OperationId,turn.Id,send.ContextId,send.TargetId,send.ParentDraftId,send.ExpectedRevision,BrainstormingIntent.Explore,BrainstormingGenerationState.Accepted,turn.CreatedAt)]};
        BrainstormingValidation.Validate(next);root.Revision=next.Revision;root.SnapshotJson=BrainstormingPersistence.Json(next);root.UpdatedUnixMilliseconds=next.UpdatedAt.ToUnixTimeMilliseconds();
        var row=new BrainstormingGenerationRecord {SessionId=send.Scope.SessionId,OperationId=send.OperationId,PayloadHash=BrainstormingPersistence.Hash(send)};
        db.BrainstormingGenerations.Add(row);var accepted=await Event(row,value,BrainstormingEventKind.Accepted,"Request accepted.",BrainstormingGenerationState.Accepted);
        await tx.CommitAsync(ct);return (row,accepted,true);
    });
    private Task Terminal(BrainstormingGenerationRecord row,BrainstormingGeneration value,BrainstormingAnswer? output)=>db.Database.CreateExecutionStrategy().ExecuteAsync(async()=>{
        // Keep accepted provenance in the session and terminal outcome in the ledger atomically.
        db.ChangeTracker.Clear();await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var stored=await db.BrainstormingGenerations.SingleAsync(g=>g.SessionId==row.SessionId&&g.OperationId==row.OperationId);
        var snapshot=BrainstormingPersistence.Json(value);
        // The original commit may already contain both the terminal ledger and
        // assistant turn. Its immutable snapshot proves completion of this write.
        if(stored.SnapshotJson==snapshot)return;
        if(Decode(stored).State is BrainstormingGenerationState.Completed or BrainstormingGenerationState.Cancelled or BrainstormingGenerationState.Failed)throw new InvalidDataException("Terminal generation outcome changed.");
        // Retain an inert failure even when manuscript authorization was revoked during generation.
        // Reads still require current authorization; this never writes manuscript or planning data.
        var root=await db.BrainstormingSessions.SingleAsync(s=>s.Id==value.Send.Scope.SessionId&&s.OwnerUserId==owner&&s.BackendKey==backend);
        var session=BrainstormingValidation.ReadSession(Encoding.UTF8.GetBytes(root.SnapshotJson));
        var request=session.Requests.Single(r=>r.OperationId==value.Send.OperationId);
        var state=value.State;var turnState=state==BrainstormingGenerationState.Completed?BrainstormingTurnState.Complete:state==BrainstormingGenerationState.Cancelled?BrainstormingTurnState.Cancelled:state==BrainstormingGenerationState.Failed?BrainstormingTurnState.Failed:BrainstormingTurnState.Incomplete;
        // Raw provider output belongs only to the generation inspection ledger.
        var assistant=new BrainstormingTurn(1,value.Send.AssistantTurnId,session.Scope,session.Turns.Count+1,value.Send.UserTurnId,value.Send.OperationId,value.Send.RequestId,value.Send.ContextId,BrainstormingRole.Assistant,turnState,output?.Answer??BrainstormingConversationContract.UnfinishedTurnMessage(turnState),DateTimeOffset.UtcNow);
        var previous=request.ParentDraftId is { } parent?session.Drafts.Single(d=>d.Id==parent):null;
        var composed=output?.Draft is { } content?BrainstormingDrafts.Compose(previous,content):default;
        var branch=value.Send.BranchId;
        var draft=output?.Draft is { } prose?new BrainstormingDraft(1,Guid.NewGuid(),session.Scope,request.Id,assistant.Id,request.ContextId,request.TargetId!.Value,request.ParentDraftId,
            (previous?.Revision??0)+1,false,value.Send.Instruction,composed.Text,prose.Constraints,prose.IntroducedFacts,assistant.CreatedAt,composed.Html,output.Contradictions,
            BranchId:branch,BranchName:branch is null?null:session.Drafts.Last(d=>d.BranchId==branch).BranchName):null;
        var context=session.Contexts.Single(c=>c.Id==request.ContextId);
        var planning=output?.Planning.Select(p=>new BrainstormingPlanningProposal(1,Guid.NewGuid(),session.Scope,assistant.Id,context.Id,p.Kind,p.EntityKey,context.Sources.Single(s=>s.Id==p.SourceId).Source,p.Field,p.Before,p.Proposed,1,assistant.CreatedAt)).ToArray()??[];
        var next=session with {Revision=session.Revision+1,UpdatedAt=assistant.CreatedAt,Turns=[..session.Turns,assistant],
            Requests=session.Requests.Select(r=>r.Id==request.Id?r with { State=state,Intent=output?.Intent??r.Intent }:r).ToArray(),Drafts=draft is null?session.Drafts:[..session.Drafts,draft],Planning=[..session.Planning,..planning]};
        BrainstormingValidation.Validate(next);root.Revision=next.Revision;root.SnapshotJson=BrainstormingPersistence.Json(next);root.UpdatedUnixMilliseconds=next.UpdatedAt.ToUnixTimeMilliseconds();
        stored.Revision++;stored.SnapshotJson=snapshot;
        await db.SaveChangesAsync();await tx.CommitAsync();
    });
    public async IAsyncEnumerable<BrainstormingConversationEvent> SendAsync(BrainstormingSend send,[EnumeratorCancellation]CancellationToken ct) {
        BrainstormingConversationContract.Validate(send);var session=await Session(send.Scope,ct);
        var old=await db.BrainstormingGenerations.AsNoTracking().SingleOrDefaultAsync(g=>g.SessionId==send.Scope.SessionId&&g.OperationId==send.OperationId,ct);
        if(old is not null) {
            if(old.PayloadHash!=BrainstormingPersistence.Hash(send))throw new InvalidDataException("Generation operation payload changed.");
            var replay=Decode(old); foreach(var e in replay.Events) { ct.ThrowIfCancellationRequested();yield return e; } yield break;
        }
        if(session.Revision!=send.ExpectedRevision || session.Requests.Any(r=>r.State is BrainstormingGenerationState.Accepted or BrainstormingGenerationState.Streaming or BrainstormingGenerationState.Unknown))throw new BrainstormingConcurrencyException(session);
        if(session.Turns.Count>BrainstormingValidation.MaxTurns-2 || session.Drafts.Count>=BrainstormingValidation.MaxTurns || session.Planning.Count>BrainstormingValidation.MaxTurns-20
            || Encoding.UTF8.GetByteCount(BrainstormingPersistence.Json(session))>BrainstormingValidation.MaxSessionBytes-700_000)
            throw new InvalidDataException("This conversation has reached its generation capacity. Preserve it and explicitly start a new session before another paid request.");
        var context=session.Contexts.SingleOrDefault(c=>c.Id==send.ContextId)??throw new InvalidDataException("Review context before sending.");
        var target=session.Targets.SingleOrDefault(t=>t.Id==send.TargetId)??throw new InvalidDataException("Resolve a destination before sending.");
        await sources.RequireAsync(context,target,ct);
        var request=BrainstormingConversationAction.Build(send,session,context,target);var provider=router.Route(request).Provider;
        var decision=await policy.EvaluateAsync(provider,request.ActionId);if(!decision.Allowed || decision.UserId!=owner)throw new InvalidDataException(decision.ErrorMessage??"Conversation is not authorized.");
        bool billable=provider is IAiBillingProvider {IsBillable:true,RequiresEntitlement:true};
        if(billable) { var allowed=await quota.EnsureAiAllowedAsync(owner,(request.Inputs["conversation_data"].ToString()!.Length+3)/4+Math.Clamp(Math.Max(options.Value.Providers.OpenAI.MaxOutputTokens,8192),8192,32768),ct);if(!allowed.Allowed)throw new InvalidDataException(allowed.ErrorMessage??"AI quota exceeded."); }
        using var stop=CancellationTokenSource.CreateLinkedTokenSource(ct);stop.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(options.Value.Streaming.BrainstormingTimeoutSeconds,1,300)));
        if(!stops.Add(send.Scope.SessionId,send.OperationId,stop))throw new BrainstormingConcurrencyException(session);
        var row=new BrainstormingGenerationRecord {SessionId=send.Scope.SessionId,OperationId=send.OperationId,PayloadHash=BrainstormingPersistence.Hash(send)};
        var value=new BrainstormingGeneration(1,send,BrainstormingGenerationState.Accepted,[],"",new(BrainstormingUsageState.Pending,provider.ProviderId,"",0,0),"",DateTimeOffset.UtcNow);
        bool accepted=false,dispatched=false,complete=false,responseInvalid=false;BrainstormingAnswer? output=null;string? preview=null;Exception? failure=null;AiUsage usage=new(0,0,TimeSpan.Zero);
        try {
            // This CAS and unique ledger identity happen before any provider dispatch.
            var acceptance=await Accept(send,value,ct);row=acceptance.Row;value=acceptance.Value;
            if(!acceptance.Created) {foreach(var retained in value.Events)yield return retained;yield break;}
            accepted=true;
            yield return value.Events[^1];
            // Recheck after persistence and before dispatch. The submitted Ready/hash is never authority.
            await sources.RequireAsync(context,target,stop.Token);stop.Token.ThrowIfCancellationRequested();dispatched=true;
            var wire=new StringBuilder();int checkpoint=0,providerEvents=0;
            if(provider is IAiStreamingProvider streaming && options.Value.Streaming.Enabled && streaming.StreamingCapabilities.SupportsTextStreaming) {
                await using var enumerator=streaming.StreamAsync(request,stop.Token).GetAsyncEnumerator(stop.Token);
                while(true) {
                    AiStreamEvent? item=null;
                    try { if(!await enumerator.MoveNextAsync())break;item=enumerator.Current; }
                    catch(Exception e) {failure=e;break;}
                    stop.Token.ThrowIfCancellationRequested();
                    if(++providerEvents>4096) {failure=new InvalidDataException("Provider stream event limit exceeded.");break;}
                    if(item is AiStreamEvent.TextDelta delta) {
                        if(wire.Length+delta.Delta.Length>BrainstormingConversationContract.MaxOutput) {failure=new InvalidDataException("Oversized conversation output.");break;}
                        wire.Append(delta.Delta);value=value with {InspectionText=wire.ToString()};
                        if(wire.Length-checkpoint>=512) {await Save(row,value);checkpoint=wire.Length;}
                        var answer=preview is null?BrainstormingConversationContract.PreviewAnswer(value.InspectionText):null;
                        if(preview is null && answer is not null) {preview=answer;
                            foreach(var chunk in answer.Chunk(8000)) {value=await Event(row,value,BrainstormingEventKind.AnswerDelta,new string(chunk),BrainstormingGenerationState.Streaming);yield return value.Events[^1];}
                            value=await Event(row,value,BrainstormingEventKind.DraftPending,"Checking response and any prose proposal…",BrainstormingGenerationState.Streaming);yield return value.Events[^1];
                        }
                    }
                    else if(item is AiStreamEvent.Completed terminal) {usage=terminal.Usage??usage;value=value with {Usage=value.Usage with {Model=terminal.Model??provider.ProviderId}};complete=true;break;}
                    else if(item is AiStreamEvent.Failed) {failure=new InvalidDataException("Provider returned a failed conversation.");break;}
                }
            } else {
                try {var result=await provider.ExecuteAsync(request,stop.Token).WaitAsync(stop.Token);value=value with {InspectionText=result.Artifacts.Single(a=>a.Modality==AiModality.Text).TextContent??"",Usage=value.Usage with {Model=result.ProviderMeta.GetValueOrDefault("model")?.ToString()??provider.ProviderId}};usage=result.Usage;complete=true;await Save(row,value);}
                catch(Exception e) {failure=e;}
            }
            if(complete) {
                try {output=BrainstormingConversationContract.ReadAnswer(value.InspectionText,send,context);if(output.Draft is { } prose)_=BrainstormingDrafts.Compose(send.ParentDraftId is { } parent?session.Drafts.Single(d=>d.Id==parent):null,prose);}
                catch(Exception e) {failure=e;output=null;responseInvalid=true;}
                if(output is not null)try {await sources.RequireAsync(context,target,stop.Token);}catch(Exception e) {failure=e;output=null;}
            } else if(failure is null)failure=new IOException("Provider ended without terminal output.");
        } finally {
            try {if(accepted)value=await Settle(row,value,request,target,provider,billable,dispatched,complete,stop.IsCancellationRequested,failure,usage,output,responseInvalid);}
            finally {stops.Remove(send.Scope.SessionId,send.OperationId);}
        }
        if(accepted)yield return value.Events[^1];
    }
    private async Task<BrainstormingGeneration> Settle(BrainstormingGenerationRecord row,BrainstormingGeneration value,AiRequest request,BrainstormingTarget target,
        IAiProvider provider,bool billable,bool dispatched,bool complete,bool cancelled,Exception? failure,AiUsage usage,BrainstormingAnswer? output,bool responseInvalid) {
        var send=value.Send;
        if(usage.InputTokens==0 && usage.OutputTokens==0 && dispatched)
            usage=new((request.Inputs["conversation_data"].ToString()!.Length+3)/4,(value.InspectionText.Length+3)/4,usage.Latency);
        // Persist Settling before invoking non-idempotent existing quota accounting. A crash leaves Unknown, never double charges.
        value=value with {Usage=value.Usage with {State=BrainstormingUsageState.Settling,InputTokens=Math.Max(0,usage.InputTokens),OutputTokens=Math.Max(0,usage.OutputTokens)}};await Save(row,value);
        if(dispatched) {
            try {
                var result=new AiResult(send.RequestId,[new(Guid.NewGuid(),AiModality.Text,"application/json",value.InspectionText,null,null)],usage,new(){{"model",value.Usage.Model}});
                if(billable) {var charged=await quota.ChargeActualUsageAsync(owner,request,result,default);if(!charged.Applied)throw new InvalidDataException("Usage settlement failed.");
                    await meter.RecordAsync(new UsageEvent {UserId=owner,Kind=request.ActionId,Provider=provider.ProviderId,Model=value.Usage.Model,InputTokens=usage.InputTokens,OutputTokens=usage.OutputTokens,DocumentId=send.Scope.DocumentId,SectionId=target.SectionId,CorrelationId=send.RequestId});}
                value=value with {Usage=value.Usage with {State=complete||cancelled||failure is InvalidDataException?BrainstormingUsageState.Settled:BrainstormingUsageState.Unknown}};
            } catch {value=value with {Usage=value.Usage with {State=BrainstormingUsageState.Unknown}};output=null;}
        } else value=value with {Usage=value.Usage with {State=BrainstormingUsageState.NotDispatched}};
        var state=value.Usage.State==BrainstormingUsageState.Unknown?BrainstormingGenerationState.Unknown:cancelled?BrainstormingGenerationState.Cancelled:output is not null?BrainstormingGenerationState.Completed:BrainstormingGenerationState.Failed;
        if(state!=BrainstormingGenerationState.Completed)output=null;
        var message=state==BrainstormingGenerationState.Completed?"Validated conversation saved. Proposals remain unapplied.":state==BrainstormingGenerationState.Unknown?"Provider or usage outcome is uncertain. Inspect this operation; no automatic retry is permitted.":state==BrainstormingGenerationState.Cancelled?"Generation stopped. Incomplete output is inspection only.":"Conversation failed validation or source checks. Refresh context or explicitly retry with a new operation.";
        if(state==BrainstormingGenerationState.Failed && responseInvalid)message="Prosa returned a response that could not be validated. Your working draft is unchanged. Retry as a new request.";
        var terminal=new BrainstormingConversationEvent(1,send.Scope,send.OperationId,send.RequestId,send.AssistantTurnId,value.Events.Count+1,state==BrainstormingGenerationState.Completed?BrainstormingEventKind.Completed:state==BrainstormingGenerationState.Cancelled?BrainstormingEventKind.Cancelled:BrainstormingEventKind.Failed,message,output,value.Usage,state,DateTimeOffset.UtcNow);
        value=value with {State=state,Message=message,Events=[..value.Events,terminal],UpdatedAt=terminal.CreatedAt};await Terminal(row,value,output);return value;
    }
}
