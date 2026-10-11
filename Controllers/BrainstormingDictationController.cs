using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WriterApp.AI.Abstractions;
using WriterApp.Application.AI;
using WriterApp.Application.Security;
using WriterApp.Application.Usage;
using WriterApp.Data;
using WriterApp.Shared.Brainstorming;

namespace WriterApp.Controllers;

[ApiController,Authorize,Route("api/brainstorming/dictation")]
public sealed class BrainstormingDictationController(AppDbContext db,IUserIdResolver identity,IAiRouter router,IAiUsagePolicy policy,IAiQuotaService quota,IUsageMeter meter,IOptions<WriterAiOptions> options,BrainstormingGenerationStops stops):ControllerBase {
    private string Owner=>identity.ResolveUserId(User);
    private string Backend=>BrainstormingValidation.Fingerprint($"{Request.Scheme}://{Request.Host}{Request.PathBase}/");
    private BrainstormingDictationService Service=>new(db,router,policy,quota,meter,options,stops,Owner,Backend);
    private void Require(BrainstormingScope scope){Response.Headers.CacheControl="no-store";BrainstormingValidation.Validate(scope);if(scope.Space!=BrainstormingIdentitySpace.Server || scope.AccountKey!=BrainstormingValidation.Fingerprint(Owner) || scope.BackendKey!=Backend)throw new BrainstormingAccessException();}
    [HttpPost,RequestSizeLimit(2_700_000)]
    public async Task<IActionResult> Transcribe(BrainstormingSpeechRequest request,CancellationToken ct) {
        try {Require(request.Scope);return Ok(await Service.TranscribeAsync(request,ct));}
        catch(BrainstormingAccessException){return NotFound(new {code="brainstorming.access_revoked"});}
        catch(BrainstormingSpeechException e){return StatusCode(e.Code=="quota"?429:503,new {code=e.Code,message=e.Message});}
        catch(InvalidDataException e){return BadRequest(new {message=e.Message});}
        catch(Exception e) when(e is BrainstormingConcurrencyException or DbUpdateException){return Conflict(new {message="Inspect the original recording before retrying. Audio was not replayed."});}
        catch(InvalidOperationException){return StatusCode(503,new {message="Configured audio transcription unavailable. Continue typing."});}
        finally{if(request.Wav is not null)Array.Clear(request.Wav);}
    }
    [HttpGet("{sessionId:guid}/{operationId:guid}")]
    public async Task<IActionResult> Inspect(Guid sessionId,Guid operationId,Guid documentId,Guid? projectId,CancellationToken ct) {
        var scope=new BrainstormingScope(1,sessionId,documentId,projectId,BrainstormingIdentitySpace.Server,Backend,BrainstormingValidation.Fingerprint(Owner),null);
        try{Require(scope);var value=await Service.InspectAsync(scope,operationId,ct);return value is null?NotFound(new {code="brainstorming.operation_missing"}):Ok(value);}catch(BrainstormingAccessException){return NotFound(new {code="brainstorming.access_revoked"});}
    }
}
