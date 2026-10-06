using System.Data;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WriterApp.Application.AI;
using WriterApp.Application.Documents;
using WriterApp.Application.Security;
using WriterApp.Application.Subscriptions;
using WriterApp.Data;
using WriterApp.Data.AI;
using WriterApp.Shared;

namespace WriterApp.Controllers;

[ApiController,Authorize,Route("api/ai/actions/history/recovery")]
public sealed class WebAiRecoveryController(AppDbContext db,IUserIdResolver users,IEntitlementService entitlements) : ControllerBase
{
    private string Owner=>users.ResolveUserId(User);
    private WebAiHistoryOperations History=>new(db,Owner,HttpContext.RequestServices,HttpContext);
    [HttpGet("{document:guid}")]
    public Task<IActionResult> List(Guid document,CancellationToken ct)=>Run(async()=> {
        await History.RequireOwner(document,ct);
        var tier=await entitlements.GetEntitlementsAsync(Owner);
        if(!FeatureRegistry.IsFeatureAllowed(FeatureKey.AiActionHistory,entitlements.GetUserTier(tier)))return StatusCode(402,new{message="AI history requires the matching plan."});
        var rows=await db.WebAiHistoryOperations.AsNoTracking().Where(r=>r.OwnerUserId==Owner && r.DocumentId==document && r.CommittedAt!=null).Take(1001).ToListAsync(ct);
        if(rows.Count>1000)throw new InvalidOperationException("Too many recovery records. Retained data was not removed.");
        var proposals=await db.AiActionHistoryEntries.AsNoTracking().Where(p=>p.OwnerUserId==Owner && p.DocumentId==document).Take(1000).ToDictionaryAsync(p=>p.Id,ct);
        var items=new List<WebRecoveryItem>();
        foreach(var group in rows.GroupBy(r=>r.ApplicationId)) {
            var row=group.MaxBy(r=>r.Sequence)!;var intent=WebAiHistoryOperations.Intent(row);
            if(!WebPlanningRecovery.Supports(intent.TargetKind) && intent.TargetKind!="Aggregate")continue;
            var snapshot=row.RecoveryJson is null?null:WebPlanningRecovery.Parse(row.RecoveryJson);
            bool supported=snapshot is not null && snapshot.Version==(intent.TargetKind=="Aggregate"?2:1);
            bool undo=supported && row.Outcome!="Undone",redo=supported && row.Outcome=="Undone";
            string before=snapshot is null?intent.BeforeContent??"":Readable(snapshot,false),after=snapshot is null?intent.AfterContent??"":Readable(snapshot,true);
            string? reason=!supported?"Comparison only: this entry has no supported server-captured scoped recovery snapshot.":null;
            if(snapshot is null && intent.TargetKind=="Aggregate") {
                var operation=await db.WebTranslationOperations.AsNoTracking().SingleOrDefaultAsync(o=>o.OwnerUserId==Owner && o.OperationId==intent.AggregateOperationId,ct);
                if(operation is not null && JsonSerializer.Deserialize<WebTranslationApproval>(operation.ApprovalJson,WebAiHistoryContracts.Json)?.Mode!="replace")
                    reason="Translated copy retained: Undo/Redo does not delete copies or later edits. Use Recover original as separate document in translation recovery.";
            }
            items.Add(new(row.ApplicationId,row.ProposalId,proposals.GetValueOrDefault(row.ProposalId)?.ActionKey??"Planning",intent.TargetKind,row.Outcome,row.CommittedAt!.Value,
                intent.Source.SectionId,intent.Source.SceneId,undo,redo,Preview(before),Preview(after),reason,intent.Source.PageId));
        }
        foreach(var proposal in proposals.Values.Where(p=>!rows.Any(r=>r.ProposalId==p.Id) && (p.ActionKey.StartsWith("scene.") || p.ActionKey.StartsWith("synopsis.") || AggregateProposal(p)))) {
            AiActionExecuteResponseDto? result=null;try{result=JsonSerializer.Deserialize<AiActionExecuteResponseDto>(proposal.ResultJson,WebAiHistoryContracts.Json);}catch(JsonException){}
            bool aggregate=AggregateProposal(proposal);
            items.Add(new(proposal.Id,proposal.Id,proposal.ActionKey,aggregate?"Aggregate":"Planning","Generated",proposal.CreatedAt,proposal.SectionId,null,false,false,Preview(result?.OriginalText??""),Preview(result?.ProposedText??""),"Comparison only: no committed scoped snapshots. Provider text cannot be used for recovery."));
        }
        return Ok(items.OrderByDescending(i=>i.CreatedAt).Take(100).ToArray());
    },ct,false);
    [HttpPost("replay"),RequestSizeLimit(8192)]
    public Task<IActionResult> Replay(WebRecoveryRequest request,CancellationToken ct)=>Run(async()=> {
        if(request.OperationId==Guid.Empty || request.ApplicationId==Guid.Empty || request.Outcome is not ("Undone" or "Redone"))throw new InvalidDataException("Choose an identified Undo or Redo operation.");
        await History.RequireOwner(request.Source.DocumentId,ct);
        var tier=await entitlements.GetEntitlementsAsync(Owner);
        if(!FeatureRegistry.IsFeatureAllowed(FeatureKey.AiUndoRedo,entitlements.GetUserTier(tier)))return StatusCode(402,new{message="Scoped AI recovery requires the matching plan. Current content and snapshots are retained."});
        var existing=await db.WebAiHistoryOperations.SingleOrDefaultAsync(r=>r.OwnerUserId==Owner && r.OperationId==request.OperationId,ct);
        if(existing is not null) {
            var prior=WebAiHistoryOperations.Intent(existing);
            if(prior.ApplicationId!=request.ApplicationId || prior.Outcome!=request.Outcome || prior.Source!=request.Source || existing.CommittedAt is null)throw new InvalidDataException("Recovery operation ID already belongs to another transition.");
            return Ok(new WebRecoveryReceipt(prior,WebAiHistoryOperations.Receipt(existing)));
        }
        var latest=await db.WebAiHistoryOperations.Where(r=>r.OwnerUserId==Owner && r.ApplicationId==request.ApplicationId && r.CommittedAt!=null).OrderByDescending(r=>r.Sequence).FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("No committed scoped operation to recover.");
        var previous=WebAiHistoryOperations.Intent(latest);
        if(previous.Source.DocumentId!=request.Source.DocumentId || previous.Source.ProjectId!=request.Source.ProjectId
            || previous.Source.SectionId!=request.Source.SectionId || previous.Source.SceneId!=request.Source.SceneId || previous.Source.PageId!=request.Source.PageId)
            throw new InvalidDataException("Recovery targets another manuscript or planning identity.");
        if(latest.RecoveryJson is null)throw new InvalidOperationException("This older operation is comparison only. Exact snapshots are unavailable.");
        if(request.Outcome!=(latest.Outcome=="Undone"?"Redone":"Undone"))throw new InvalidOperationException("The current persisted outcome does not support this transition. Refresh history.");
        await new WebAiSourceService(db,Owner).Require(request.Source,ct);
        var snapshot=WebPlanningRecovery.Parse(latest.RecoveryJson);
        if(snapshot.DocumentId!=request.Source.DocumentId || snapshot.ProjectId!=request.Source.ProjectId)throw new InvalidDataException("Recovery snapshots belong to another target.");
        if(previous.TargetKind=="Aggregate")await new WebAggregateRecovery(db).Restore(snapshot,request.Outcome=="Undone",ct);
        else await new WebPlanningRecovery(db).Restore(snapshot,request.Outcome=="Undone",ct);
        var intent=previous with {OperationId=request.OperationId,PreviousOperationId=latest.OperationId,Sequence=latest.Sequence+1,Source=request.Source,
            Outcome=request.Outcome,BeforeContent=previous.AfterContent,AfterContent=previous.BeforeContent};
        WebAiHistoryContracts.Validate(intent);
        var row=new WebAiHistoryOperationRecord {OwnerUserId=Owner,OperationId=intent.OperationId,ApplicationId=intent.ApplicationId,ProposalId=intent.ProposalId,DocumentId=intent.Source.DocumentId,
            Sequence=intent.Sequence,Outcome=intent.Outcome,RequestHash=WebAiHistoryContracts.Hash(intent),IntentJson=JsonSerializer.Serialize(intent,WebAiHistoryContracts.Json),RecoveryJson=latest.RecoveryJson,CommittedAt=DateTimeOffset.UtcNow};
        db.WebAiHistoryOperations.Add(row);await db.SaveChangesAsync(ct);return Ok(new WebRecoveryReceipt(intent,WebAiHistoryOperations.Receipt(row)));
    },ct,true);
    private static string Readable(WebRecoverySnapshot snapshot,bool after)=>string.Join("\n\n",snapshot.Targets.Select(t=>t.Kind+" · "+t.Id+"\n"+string.Join("\n",(after?t.After:t.Before).Select(f=>f.Key+": "+(f.Value??"Not set")))));
    private static string Preview(string text)=>text.Length>1000?text[..1000]+"\nComparison preview truncated; complete recovery snapshots remain on this backend.":text;
    private static bool AggregateProposal(AiActionHistoryEntryRecord proposal) {
        if(proposal.ActionKey is "translate.section" or "translate.document" || WritingActions.SectionKeys.Contains(proposal.ActionKey))return true;
        if(!WritingOutline.Consumes(proposal.ActionKey))return false;
        try {return JsonSerializer.Deserialize<AiActionExecuteRequestDto>(proposal.RequestJson,WebAiHistoryContracts.Json)?.Parameters?.ContainsKey(WritingActions.Parameter)==true;}
        catch(JsonException){return false;}
    }
    private async Task<IActionResult> Run(Func<Task<IActionResult>> action,CancellationToken ct,bool transaction) {
        try {
            if(!transaction)return await action();
            return await db.Database.CreateExecutionStrategy().ExecuteAsync(async()=> {
                db.ChangeTracker.Clear();await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
                await db.Database.ExecuteSqlRawAsync("UPDATE DocumentSyncClocks SET Sequence=Sequence WHERE Id=1",ct);
                var result=await action();if((result as Microsoft.AspNetCore.Mvc.Infrastructure.IStatusCodeActionResult)?.StatusCode is not >=400)await tx.CommitAsync(ct);return result;
            });
        }catch(System.Security.SecurityException){return Unauthorized();}
        catch(Exception e) when(e is InvalidOperationException or InvalidDataException or JsonException or DocumentSyncException) {return Conflict(new{message=e.Message});}
        catch(DbUpdateException){return StatusCode(503,new{message="Recovery storage failed. No partial restore was committed. Retry the same operation ID after inspecting history."});}
    }
}
