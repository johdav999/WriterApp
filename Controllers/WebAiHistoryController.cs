using System.Data;
using System.Security;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WriterApp.Application.AI;
using WriterApp.Application.Security;
using WriterApp.Application.Subscriptions;
using WriterApp.Data;
using WriterApp.Data.AI;
using WriterApp.Shared;

namespace WriterApp.Controllers;

[ApiController,Authorize,Route("api/ai/actions/history/web")]
public sealed class WebAiHistoryController(AppDbContext db,IUserIdResolver users,IEntitlementService entitlements) : ControllerBase
{
    private string Owner=>users.ResolveUserId(User);
    private WebAiHistoryOperations Operations=>new(db,Owner,HttpContext.RequestServices,HttpContext);
    private async Task<IActionResult?> Gate(FeatureKey key)
    {
        var access=await entitlements.GetEntitlementsAsync(Owner);
        return FeatureRegistry.IsFeatureAllowed(key,entitlements.GetUserTier(access))?null:StatusCode(402,new{code="entitlement_denied",message="History delivery requires the matching plan. Saved writing and reporting intent are retained; retry after access is restored."});
    }
    [HttpPost("operations"),RequestSizeLimit(2_000_000)]
    public Task<IActionResult> Prepare(WebAiHistoryIntent intent,CancellationToken ct)=>Run(async()=> {
        WebAiHistoryContracts.Validate(intent);await Operations.RequireOwner(intent.Source.DocumentId,ct);
        var old=await db.WebAiHistoryOperations.SingleOrDefaultAsync(o=>o.OwnerUserId==Owner && o.OperationId==intent.OperationId,ct);
        if(old is not null) {if(old.RequestHash!=WebAiHistoryContracts.Hash(intent))throw new InvalidDataException("Operation ID already identifies another approval.");return Ok(WebAiHistoryOperations.Receipt(old));}
        await Operations.ValidateNew(intent,ct);
        var row=new WebAiHistoryOperationRecord {OwnerUserId=Owner,OperationId=intent.OperationId,ApplicationId=intent.ApplicationId,ProposalId=intent.ProposalId,
            DocumentId=intent.Source.DocumentId,Sequence=intent.Sequence,Outcome=intent.Outcome,RequestHash=WebAiHistoryContracts.Hash(intent),IntentJson=JsonSerializer.Serialize(intent,WebAiHistoryContracts.Json)};
        db.WebAiHistoryOperations.Add(row);await db.SaveChangesAsync(ct);return Ok(WebAiHistoryOperations.Receipt(row));
    },ct);
    [HttpGet("operations/{operation:guid}")]
    public Task<IActionResult> Inspect(Guid operation,CancellationToken ct)=>Run(async()=> {
        var row=await db.WebAiHistoryOperations.SingleOrDefaultAsync(o=>o.OwnerUserId==Owner && o.OperationId==operation,ct);
        if(row is null)return NotFound(new{message="This intent has no server receipt yet. No successful persistence is inferred."});
        await Operations.RequireOwner(row.DocumentId,ct);await Operations.ReconcileAggregate(row,ct);await db.SaveChangesAsync(ct);
        return Ok(WebAiHistoryOperations.Receipt(row));
    },ct);
    [HttpPost("operations/{operation:guid}/report"),RequestSizeLimit(2_000_000)]
    public Task<IActionResult> Report(Guid operation,WebAiHistoryIntent intent,CancellationToken ct)=>Run(async()=> {
        WebAiHistoryContracts.Validate(intent);await Operations.RequireOwner(intent.Source.DocumentId,ct);
        var row=await db.WebAiHistoryOperations.SingleOrDefaultAsync(o=>o.OwnerUserId==Owner && o.OperationId==operation,ct);
        if(row is null || operation!=intent.OperationId)return NotFound();
        if(row.RequestHash!=WebAiHistoryContracts.Hash(intent))throw new InvalidDataException("History payload differs from the approved immutable intent.");
        if(row.ReportedAt is not null)return Ok(WebAiHistoryOperations.Receipt(row));
        await Operations.ReconcileAggregate(row,ct);
        if(row.CommittedAt is null)throw new InvalidOperationException("Save has no committed receipt. A preview or failed save cannot report Applied.");
        var gate=await Gate(FeatureKey.AiActionHistory);if(gate is not null)return gate;
        if(intent.PreviousOperationId is { } previous && !await db.WebAiHistoryOperations.AnyAsync(o=>o.OwnerUserId==Owner && o.OperationId==previous && o.ReportedAt!=null,ct))
            throw new InvalidOperationException("Deliver the preceding outcome first; saved writing is unchanged.");
        row.ReportedAt=DateTimeOffset.UtcNow;await db.SaveChangesAsync(ct);return Ok(WebAiHistoryOperations.Receipt(row));
    },ct);
    // Reading a transition never changes history. Its later content save and receipt commit together.
    [HttpPost("move"),RequestSizeLimit(8192)]
    public Task<IActionResult> Move(WebAiHistoryMoveRequest request,CancellationToken ct)=>Run(async()=> {
        if(request.Outcome is not ("Undone" or "Redone"))throw new InvalidDataException("Choose Undo or Redo.");
        var gate=await Gate(FeatureKey.AiUndoRedo);if(gate is not null)return gate;
        await new WebAiSourceService(db,Owner).Require(request.Source,ct);
        var rows=await db.WebAiHistoryOperations.AsNoTracking().Where(o=>o.OwnerUserId==Owner && o.DocumentId==request.Source.DocumentId && o.CommittedAt!=null).Take(1001).ToListAsync(ct);
        if(rows.Count>1000)throw new InvalidOperationException("History inspection exceeds the supported limit; use original-copy recovery.");
        var current=rows.Select(row=>(Row:row,Intent:WebAiHistoryOperations.Intent(row))).Where(x=>x.Intent.TargetKind is "Page" or "SceneContent"
            && x.Intent.Source.PageId==request.Source.PageId && x.Intent.Source.SceneId==request.Source.SceneId && x.Intent.Source.SectionId==request.Source.SectionId)
            .GroupBy(x=>x.Row.ApplicationId).Select(g=>g.MaxBy(x=>x.Row.Sequence)).OrderByDescending(x=>x.Row.CommittedAt).FirstOrDefault();
        if(current.Row is null)return NoContent();
        if(request.Outcome!=(current.Row.Outcome=="Undone"?"Redone":"Undone"))throw new InvalidOperationException("The latest persisted AI outcome does not support this transition.");
        var id=Guid.NewGuid();var intent=current.Intent with {OperationId=id,Sequence=current.Row.Sequence+1,PreviousOperationId=current.Row.OperationId,
            Outcome=request.Outcome,Source=request.Source,BeforeContent=current.Intent.AfterContent,AfterContent=current.Intent.BeforeContent};
        await Operations.ValidateNew(intent,ct);return Ok(new WebAiHistoryMove(intent));
    },ct);
    private async Task<IActionResult> Run(Func<Task<IActionResult>> action,CancellationToken ct)
    {
        try {return await db.Database.CreateExecutionStrategy().ExecuteAsync(async()=> {
            db.ChangeTracker.Clear();await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
            await db.Database.ExecuteSqlRawAsync("UPDATE DocumentSyncClocks SET Sequence=Sequence WHERE Id=1",ct);
            var result=await action();if((result as Microsoft.AspNetCore.Mvc.Infrastructure.IStatusCodeActionResult)?.StatusCode is not >=400)await tx.CommitAsync(ct);return result;
        });}
        catch(SecurityException){return Unauthorized();}
        catch(Exception e) when(e is InvalidOperationException or InvalidDataException or JsonException or WriterApp.Application.Documents.DocumentSyncException)
        {return Conflict(new{code="history.web_rejected",message=e.Message});}
    }
}
