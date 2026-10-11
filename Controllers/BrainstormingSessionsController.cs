using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WriterApp.Application.AI;
using WriterApp.Application.Security;
using WriterApp.Data;
using WriterApp.Shared.Brainstorming;

namespace WriterApp.Controllers;

[ApiController, Authorize, Route("api/brainstorming/sessions")]
public sealed class BrainstormingSessionsController(AppDbContext db, IUserIdResolver identity) : ControllerBase
{
    private BrainstormingSessions Service() {
        Response.Headers.CacheControl = "no-store";
        var backend = $"{Request.Scheme}://{Request.Host}{Request.PathBase}/";
        return new(db, identity.ResolveUserId(User), BrainstormingValidation.Fingerprint(backend));
    }
    private async Task<ActionResult<T>> Run<T>(Func<BrainstormingSessions, Task<T>> work) {
        try { var result = await work(Service()); return result is null ? NotFound() : Ok(result); }
        catch (BrainstormingAccessException) { return NotFound(new {code="brainstorming.access_revoked"}); }
        catch (System.Security.SecurityException) { return Unauthorized(); }
        catch (BrainstormingConcurrencyException e) { return Conflict(new { code = "brainstorming.conflict", message = e.Message, current = e.Current }); }
        catch (DbUpdateException) { return Conflict(new { code = "brainstorming.concurrent_write", message = "Inspect the retained operation before retrying this session save." }); }
        catch (InvalidDataException e) { return BadRequest(new { message = e.Message }); }
    }
    [HttpGet]
    public Task<ActionResult<BrainstormingSessionPage>> List(Guid documentId, Guid? after, int limit = 25, CancellationToken ct = default) => Run(s => s.ListAsync(documentId, after, limit, ct));
    [HttpGet("{id:guid}")]
    public Task<ActionResult<BrainstormingSession?>> Read(Guid id, Guid documentId, CancellationToken ct) => Run(s => s.ReadAsync(documentId, id, ct));
    [HttpGet("{id:guid}/operations/{operationId:guid}")]
    public Task<ActionResult<BrainstormingReceipt?>> Inspect(Guid id, Guid operationId, Guid documentId, CancellationToken ct) => Run(s => s.InspectAsync(documentId, id, operationId, ct));
    [HttpGet("{id:guid}/turns")]
    public Task<ActionResult<BrainstormingTurnPage?>> Turns(Guid id, Guid documentId, long after = 0, int limit = 25, CancellationToken ct = default) => Run<BrainstormingTurnPage?>(async s => {
        if (after < 0 || limit is < 1 or > 50) throw new InvalidDataException("Use a turn page size of 1–50.");
        var session = await s.ReadAsync(documentId, id, ct); if (session is null) return null;
        var turns = session.Turns.Where(t => t.Sequence > after).Take(limit + 1).ToArray(); var items = turns.Take(limit).ToArray();
        return new(1, session.Scope, session.Revision, items, turns.Length > limit ? items[^1].Sequence : null);
    });
    [HttpPost, RequestSizeLimit(2_100_000)]
    public Task<ActionResult<BrainstormingReceipt>> Write(BrainstormingWrite request, CancellationToken ct) => Run(s => s.WriteAsync(request, ct));
    [HttpPost("{id:guid}/recovery"),RequestSizeLimit(6_100_000)]
    public Task<ActionResult<BrainstormingRecoveryBundle>> Recovery(Guid id,Guid documentId,BrainstormingRecoveryCopy? copy,CancellationToken ct)=>Run(async s=>{
        var session=await s.ReadAsync(documentId,id,ct)??throw new BrainstormingAccessException();
        return await new BrainstormingRecoveryHistory(db,identity.ResolveUserId(User),session.Scope.BackendKey).ExchangeAsync(session.Scope,copy,ct);
    });
}
