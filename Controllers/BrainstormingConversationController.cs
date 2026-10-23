using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WriterApp.AI.Abstractions;
using WriterApp.Application.AI;
using WriterApp.Application.Continuity;
using WriterApp.Application.Documents;
using WriterApp.Application.Security;
using WriterApp.Application.Subscriptions;
using WriterApp.Application.Usage;
using WriterApp.Data;
using WriterApp.Shared.Brainstorming;
using Microsoft.Extensions.Options;

namespace WriterApp.Controllers;

[ApiController,Authorize,Route("api/brainstorming/conversation")]
public sealed class BrainstormingConversationController(AppDbContext db,IUserIdResolver identity,IBibleStore bibles,IEntitlementService entitlements,
    IAiRouter router,IAiUsagePolicy policy,IAiQuotaService quota,IUsageMeter meter,IOptions<WriterAiOptions> options,BrainstormingGenerationStops stops) : ControllerBase
{
    private string Owner=>identity.ResolveUserId(User);
    private string Backend=>BrainstormingValidation.Fingerprint($"{Request.Scheme}://{Request.Host}{Request.PathBase}/");
    private WriterApp.Application.Collaboration.CollaborationAccessService? Access => HttpContext.RequestServices?.GetService<WriterApp.Application.Collaboration.CollaborationAccessService>();
    private BrainstormingGenerationContext Sources=>new(db,bibles,entitlements,Owner,Access);
    private BrainstormingConversationService Service=>new(db,router,policy,quota,meter,options,stops,Sources,Owner,Backend,Access);
    private void Require(BrainstormingScope scope) {
        Response.Headers.CacheControl="no-store";BrainstormingValidation.Validate(scope);
        if(scope.AccountKey!=BrainstormingValidation.Fingerprint(Owner)||scope.BackendKey!=Backend||scope.Space!=BrainstormingIdentitySpace.Server)throw new BrainstormingAccessException();
    }
    [HttpPost("context"),RequestSizeLimit(200_000)]
    public async Task<ActionResult<BrainstormingServerContext>> Context(BrainstormingServerContextRequest request,CancellationToken ct) {
        try {Require(request.Scope);return Ok(await Sources.PrepareAsync(request,ct));}
        catch(BrainstormingAccessException){return NotFound(new {code="brainstorming.access_revoked"});}
        catch(DocumentSyncException e){return StatusCode(e.Status,new {code=e.Status==404?"brainstorming.access_revoked":null,message=e.Message});}
        catch(InvalidDataException e){return BadRequest(new {message=e.Message});}
    }
    [HttpPost("stream"),RequestSizeLimit(32_000)]
    public async Task Stream(BrainstormingSend send,CancellationToken ct) {
        try {
            Require(send.Scope);Response.ContentType="application/x-ndjson";
            await foreach(var item in Service.SendAsync(send,ct)) {
                await Response.WriteAsync(BrainstormingPersistence.Json(item)+"\n",ct);await Response.Body.FlushAsync(ct);
            }
        } catch(BrainstormingRequestRejectedException e) {
            Response.ContentType="application/json";Response.StatusCode=400;
            await Response.WriteAsJsonAsync(new {code=e.Code,message=e.Message,accepted=false,operationId=send.OperationId},ct);
        } catch(Exception e) when(e is BrainstormingAccessException or InvalidDataException or BrainstormingConcurrencyException or DocumentSyncException or DbUpdateException or InvalidOperationException) {
            if(Response.HasStarted)return; // durable inspect endpoint is authoritative after a broken stream
            var accessLost=e is BrainstormingAccessException || e is DocumentSyncException {Status:404};
            Response.ContentType="application/json";Response.StatusCode=accessLost?404:e is BrainstormingConcurrencyException or DbUpdateException?409:400;
            await Response.WriteAsJsonAsync(new {code=accessLost?"brainstorming.access_revoked":null,message=e is InvalidDataException?e.Message:"Conversation unavailable. Inspect the operation before retrying."},ct);
        }
    }
    [HttpGet("{sessionId:guid}/{operationId:guid}")]
    public async Task<ActionResult<BrainstormingGeneration>> Inspect(Guid sessionId,Guid operationId,Guid documentId,Guid? projectId,CancellationToken ct) {
        var scope=new BrainstormingScope(1,sessionId,documentId,projectId,BrainstormingIdentitySpace.Server,Backend,BrainstormingValidation.Fingerprint(Owner),null);
        try {Require(scope);var value=await Service.InspectAsync(scope,operationId,ct);return value is null?NotFound(new {code="brainstorming.operation_missing"}):Ok(value);}catch(BrainstormingAccessException){return NotFound(new {code="brainstorming.access_revoked"});}
    }
    [HttpPost("{sessionId:guid}/{operationId:guid}/stop")]
    public async Task<IActionResult> Stop(Guid sessionId,Guid operationId,BrainstormingScope scope,CancellationToken ct) {
        try {Require(scope);if(scope.SessionId!=sessionId)return BadRequest();await Service.StopAsync(scope,operationId,ct);return NoContent();}
        catch(BrainstormingAccessException){return NotFound(new {code="brainstorming.access_revoked"});}catch(InvalidDataException e){return Conflict(new {message=e.Message});}
    }
}
