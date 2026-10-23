using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WriterApp.Application.AI;
using WriterApp.Application.Security;
using WriterApp.Data;
using WriterApp.Shared.Brainstorming;

namespace WriterApp.Controllers;

[ApiController, Authorize, Route("api/brainstorming/sources")]
public sealed class BrainstormingStorySourcesController(AppDbContext db, IUserIdResolver identity) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<BrainstormingSupportingPage>> List(Guid? after, int limit = 25, CancellationToken ct = default)
    {
        Response.Headers.CacheControl = "no-store";
        if (limit is < 1 or > 50 || after == Guid.Empty) return BadRequest();
        string owner; try { owner = identity.ResolveUserId(User); } catch (System.Security.SecurityException) { return Unauthorized(); }
        if (await db.DeletedUserIdentities.AnyAsync(u => u.UserId == owner, ct)) return NotFound();
        var query = db.Documents.AsNoTracking().Where(d => d.OwnerUserId == owner && d.DeletedAtUtc == null && !d.IsArchived
            && db.Projects.Any(p => p.Id == d.ProjectId && p.OwnerUserId == owner));
        if (after is { } cursor) {
            if (!await query.AnyAsync(d => d.Id == cursor, ct)) return NotFound();
            query = query.Where(d => d.Id.CompareTo(cursor) > 0);
        }
        var rows = await query.OrderBy(d => d.Id).Take(limit + 1).Select(d => new { d.Id, d.Title, d.DocumentKind }).ToArrayAsync(ct);
        return Ok(new BrainstormingSupportingPage(1, rows.Take(limit).Select(d => new BrainstormingSupportingDocument(d.Id, d.Title, d.DocumentKind.ToString())).ToArray(), rows.Length > limit ? rows[limit - 1].Id : null));
    }
    [HttpGet("{documentId:guid}")]
    public async Task<ActionResult<BrainstormingStorySnapshot>> Read(Guid documentId, Guid? projectId, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        try { return Ok(await new BrainstormingStorySourceReader(db, identity.ResolveUserId(User), HttpContext.RequestServices?.GetService<WriterApp.Application.Collaboration.CollaborationAccessService>()).ReadAsync(documentId, projectId, ct)); }
        catch (BrainstormingAccessException) { return NotFound(new {code="brainstorming.access_revoked"}); }
        catch (System.Security.SecurityException) { return Unauthorized(); }
        catch (WriterApp.Application.Documents.DocumentSyncException e) { return e.Status == 404 ? NotFound(new {code="brainstorming.access_revoked"}) : Conflict(new { code = "brainstorming.source_changed", message = e.Message }); }
        catch (InvalidDataException e) { return UnprocessableEntity(new { code = "brainstorming.invalid_source", message = e.Message }); }
        catch (InvalidOperationException) { return Conflict(new { code = "brainstorming.source_changed", message = "Save/synchronize or refresh story context. Writing was not changed." }); }
    }
}
