using System.Runtime.CompilerServices;

namespace WriterApp.Shared.Brainstorming;

/// <summary>Save a send identity before HTTP. Resume inspects; it never dispatches a provider.</summary>
public sealed class BrainstormingConversationWorkflow(IBrainstormingCacheStore store,IBrainstormingConversation remote,
    Func<BrainstormingScope,bool,CancellationToken,Task<BrainstormingCache>> synchronize)
{
    public async IAsyncEnumerable<BrainstormingConversationEvent> SendAsync(BrainstormingScope local,string instruction,Guid? parentDraft,
        [EnumeratorCancellation]CancellationToken ct=default, BrainstormingDraftSelection? selection=null) {
        var cache=await store.ReadAsync(local,ct)??throw new InvalidDataException("Prepare story context first.");
        if(cache.PendingGeneration is not null)throw new InvalidDataException("Inspect the previous generation before another send. It will not be replayed automatically.");
        if(cache.DraftEdit is not null)throw new InvalidDataException("Checkpoint the retained manual proposal before sending; an older version will not be substituted.");
        var target=cache.Session.Targets.LastOrDefault()??throw new InvalidDataException("Resolve a destination first.");
        if(parentDraft is { } parentId) {
            var parent=cache.Session.Drafts.SingleOrDefault(d=>d.Id==parentId)??throw new InvalidDataException("Reviewed draft unavailable.");
            var original=cache.Session.Targets.Single(t=>t.Id==parent.TargetId);
            if(!BrainstormingDrafts.SameDestination(original,target))throw new InvalidDataException("The reviewed destination differs from this draft's original destination. Return and review it, or explicitly discuss the new destination without this candidate.");
            target=original;
            if(selection is not null)BrainstormingDrafts.RequireSelection(selection,parent);
        } else if(selection is not null)throw new InvalidDataException("Select a reviewed draft before a passage follow-up.");
        var context=cache.Session.Contexts.LastOrDefault()??throw new InvalidDataException("Prepare story context first.");BrainstormingContextPolicy.GenerationData(context);
        cache=await synchronize(local,false,ct);
        var scope=cache.Mapping?.Server??throw new InvalidDataException("Map this authenticated session first.");
        var send=new BrainstormingSend(1,scope,Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),cache.Acknowledgement!.Revision,context.Id,target.Id,parentDraft,instruction,selection,BrainstormingDrafts.Candidate(cache)?.BranchId);
        BrainstormingConversationContract.Validate(send);
        cache=await store.WriteAsync(cache with {PendingGeneration=send,Composer=instruction},cache.CacheRevision,Guid.NewGuid(),ct);
        // The retained immutable identity proves which uncertain request needs inspection after restart.
        await using var stream=remote.SendAsync(send,ct).GetAsyncEnumerator(ct);
        bool received=false;
        while(true) {
            BrainstormingConversationEvent item;
            try {if(!await stream.MoveNextAsync())break;item=stream.Current;}
            catch(BrainstormingSendRejectedException e) when(!received && e.OperationId==send.OperationId) {
                var retained=await store.ReadAsync(local,ct)??throw new InvalidDataException("Conversation unavailable.");
                if(retained.PendingGeneration!=send)throw new OperationCanceledException("Conversation generation changed.");
                var rejected=new BrainstormingGeneration(1,send,BrainstormingGenerationState.Failed,[],"",new(BrainstormingUsageState.NotDispatched,"","",0,0),
                    e.Message+" Message retained; retry only after resolving this issue.",DateTimeOffset.UtcNow);
                await store.WriteAsync(retained with {LastGeneration=rejected},retained.CacheRevision,Guid.NewGuid(),ct);
                throw;
            }
            received=true;
            if(item.Output is { } output)BrainstormingConversationContract.ReadAnswer(BrainstormingPersistence.Json(output),send,context);
            var current=await store.ReadAsync(local,ct)??throw new InvalidDataException("Conversation unavailable.");
            if(current.PendingGeneration!=send)throw new OperationCanceledException("Conversation generation changed.");
            var generation=current.LastGeneration?.Send==send?current.LastGeneration:new BrainstormingGeneration(1,send,BrainstormingGenerationState.Accepted,[],"",item.Usage,"",item.CreatedAt);
            generation=generation with {State=item.State,Events=[..generation.Events,item],Usage=item.Usage,Message=item.Text,UpdatedAt=item.CreatedAt,
                InspectionText=item.Kind==BrainstormingEventKind.AnswerDelta?generation.InspectionText+item.Text:generation.InspectionText};
            await store.WriteAsync(current with {LastGeneration=generation},current.CacheRevision,Guid.NewGuid(),ct);
            yield return item;
        }
        await InspectAsync(local,ct);
    }
    public async Task<BrainstormingCache> InspectAsync(BrainstormingScope local,CancellationToken ct=default) {
        var cache=await store.ReadAsync(local,ct)??throw new InvalidDataException("Conversation unavailable.");
        if(cache.PendingGeneration is not { } send)return await synchronize(local,true,ct);
        var generation=await remote.InspectAsync(send.Scope,send.OperationId,ct);
        var current=await store.ReadAsync(local,ct)??throw new InvalidDataException("Conversation unavailable.");if(current.PendingGeneration!=send)throw new OperationCanceledException("Conversation changed.");
        bool settled=generation is null || generation.State is BrainstormingGenerationState.Completed or BrainstormingGenerationState.Cancelled or BrainstormingGenerationState.Failed;
        var absent=generation is null?current.LastGeneration is {State:BrainstormingGenerationState.Failed,Usage.State:BrainstormingUsageState.NotDispatched} rejected && rejected.Send==send
            ? rejected : new BrainstormingGeneration(1,send,BrainstormingGenerationState.Failed,[],"",new(BrainstormingUsageState.NotDispatched,"","",0,0),"Owned server inspection confirmed this operation was never accepted. Message retained; retry is explicit.",DateTimeOffset.UtcNow):null;
        var saved=await store.WriteAsync(current with {PendingGeneration=settled?null:send,LastGeneration=generation??absent,
            Composer=generation?.State==BrainstormingGenerationState.Completed && current.Composer==send.Instruction?"":current.Composer},current.CacheRevision,Guid.NewGuid(),ct);
        if(generation is null)return saved;
        var pulled=await synchronize(local,true,ct);
        var draft=generation.State==BrainstormingGenerationState.Completed?pulled.Session.Drafts.LastOrDefault(d=>d.RequestId==send.RequestId):null;
        return draft is null?pulled:await store.WriteAsync(pulled with{DraftReview=new(1,draft.Id,true)},pulled.CacheRevision,Guid.NewGuid(),ct);
    }
    public async Task StopAsync(BrainstormingScope local,CancellationToken ct=default) {
        var cache=await store.ReadAsync(local,ct)??throw new InvalidDataException("Conversation unavailable.");
        if(cache.PendingGeneration is { } send)await remote.StopAsync(send.Scope,send.OperationId,ct);
    }
}
