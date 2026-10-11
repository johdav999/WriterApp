using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WriterApp.Application.AI;
using WriterApp.Application.Security;
using WriterApp.Data;
using WriterApp.Shared.Brainstorming;

namespace WriterApp.Controllers;

[ApiController,Authorize,Route("api/brainstorming/{session:guid}/keeps/{operation:guid}")]
public sealed class BrainstormingKeepsController(AppDbContext db,IUserIdResolver users):ControllerBase
{
    private string Owner=>users.ResolveUserId(User);
    private string Backend=>BrainstormingValidation.Fingerprint($"{Request.Scheme}://{Request.Host}{Request.PathBase}/");
    private BrainstormingKeeps Service=>new(db,Owner,Backend);
    private BrainstormingScope Scope(Guid session,Guid documentId,Guid? projectId)=>new(1,session,documentId,projectId,BrainstormingIdentitySpace.Server,Backend,BrainstormingValidation.Fingerprint(Owner),null);
    [HttpPost,RequestSizeLimit(2_100_000)]
    public Task<IActionResult> Prepare(Guid session,Guid operation,BrainstormingKeepReview review,CancellationToken ct)=>Run(async()=>{
        BrainstormingKeepContract.Validate(review);
        if(review.Scope.SessionId!=session || review.OperationId!=operation)throw new InvalidDataException("Application identity changed.");return Ok(await Service.PrepareAsync(review,ct));
    });
    [HttpGet]
    public Task<IActionResult> Inspect(Guid session,Guid operation,Guid documentId,Guid? projectId,CancellationToken ct)=>Run(async()=>{
        var receipt=await Service.InspectAsync(Scope(session,documentId,projectId),operation,ct);return receipt is null?NotFound(new{code="brainstorming.keep_missing"}):Ok(receipt);
    });
    [HttpPost("commit"),RequestSizeLimit(8192)]
    public Task<IActionResult> Commit(Guid session,Guid operation,BrainstormingScope scope,CancellationToken ct)=>Run(async()=>{
        if(scope.SessionId!=session)throw new InvalidDataException("Application session changed.");return Ok(await Service.CommitAsync(scope,operation,ct));
    });
    [HttpPost("inspect"),RequestSizeLimit(2_100_000)]
    public Task<IActionResult> InspectReview(Guid session,Guid operation,BrainstormingKeepReview review,CancellationToken ct)=>Run(async()=>{
        BrainstormingKeepContract.Validate(review);
        if(review.Scope.SessionId!=session || review.OperationId!=operation)throw new InvalidDataException("Application identity changed.");
        var receipt=await Service.InspectAsync(review.Scope,operation,ct);
        // InspectAsync first proves the owned session exists. A missing ledger row is then authoritative absence, not a foreign 404.
        return Ok(receipt??new BrainstormingKeepReceipt(1,operation,review.Scope,review.Candidate.Id,BrainstormingKeepContract.Hash(review),BrainstormingApplicationState.Failed,operation,0,"No application record exists in this owned session. Explicit retry may prepare this same approved operation.",DateTimeOffset.UtcNow));
    });
    [HttpPost("move"),RequestSizeLimit(8192)]
    public Task<IActionResult> Move(Guid session,Guid operation,Guid documentId,Guid? projectId,BrainstormingKeepMove move,CancellationToken ct)=>Run(async()=>Ok(await Service.MoveAsync(Scope(session,documentId,projectId),operation,move,ct)));
    [HttpPost("original"),RequestSizeLimit(8192)]
    public Task<IActionResult> Original(Guid session,Guid operation,Guid documentId,Guid? projectId,CancellationToken ct)=>Run(async()=>Ok(await Service.RecoverAsync(Scope(session,documentId,projectId),operation,ct)));
    private async Task<IActionResult> Run(Func<Task<IActionResult>> work)
    {
        Response.Headers.CacheControl="no-store";
        try{return await work();}catch(BrainstormingAccessException){return NotFound();}catch(System.Security.SecurityException){return Unauthorized();}
        catch(Exception e) when(e is InvalidDataException or InvalidOperationException or System.Text.Json.JsonException or WriterApp.Application.Documents.DocumentSyncException){return Conflict(new{code="brainstorming.keep_rejected",message=e.Message});}
        catch(DbUpdateException){return StatusCode(503,new{message="Application transaction was not confirmed. Inspect the same operation; no partial writing is accepted."});}
    }
}
