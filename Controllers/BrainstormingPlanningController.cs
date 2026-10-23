using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WriterApp.Application.AI;
using WriterApp.Application.Security;
using WriterApp.Data;
using WriterApp.Shared.Brainstorming;

namespace WriterApp.Controllers;

[ApiController,Authorize,Route("api/brainstorming/{session:guid}/planning")]
public sealed class BrainstormingPlanningController(AppDbContext db,IUserIdResolver users):ControllerBase
{
    private BrainstormingPlanningSaves Service=>new(db,users.ResolveUserId(User),BrainstormingValidation.Fingerprint($"{Request.Scheme}://{Request.Host}{Request.PathBase}/"),HttpContext.RequestServices.GetService<WriterApp.Application.Collaboration.CollaborationAccessService>());
    private async Task<IActionResult> Run(Guid session,BrainstormingScope scope,Func<Task<object>> action) {
        Response.Headers.CacheControl="no-store";
        try {BrainstormingValidation.Validate(scope);if(scope.SessionId!=session)throw new InvalidDataException("Planning session changed.");return Ok(await action());}
        catch(BrainstormingAccessException){return NotFound();}catch(System.Security.SecurityException){return Unauthorized();}
        catch(Exception e) when(e is InvalidDataException or InvalidOperationException or System.Text.Json.JsonException){return Conflict(new{code="brainstorming.planning_rejected",message=e.Message});}
        catch(DbUpdateException){return StatusCode(503,new {message="Planning save unconfirmed. Inspect this same approval before retrying."});}
    }
    [HttpPost("snapshot"),RequestSizeLimit(8192)] public Task<IActionResult> Snapshot(Guid session,BrainstormingScope scope,CancellationToken ct)=>Run(session,scope,async()=>await Service.CaptureAsync(scope,ct));
    [HttpPost("apply"),RequestSizeLimit(2_100_000)] public Task<IActionResult> Apply(Guid session,BrainstormingPlanningReview review,CancellationToken ct)=>Run(session,review.Scope,async()=>await Service.ApplyAsync(review,ct));
    [HttpPost("inspect"),RequestSizeLimit(2_100_000)] public Task<IActionResult> Inspect(Guid session,BrainstormingPlanningReview review,CancellationToken ct)=>Run(session,review.Scope,async()=>await Service.InspectAsync(review,ct));
    [HttpPost("undo"),RequestSizeLimit(2_100_000)] public Task<IActionResult> Undo(Guid session,BrainstormingPlanningReview review,CancellationToken ct)=>Run(session,review.Scope,async()=>await Service.UndoAsync(review,ct));
}
