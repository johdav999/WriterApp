using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WriterApp.Application.Collaboration;
using WriterApp.Application.Security;
using WriterApp.Data;
using WriterApp.Shared.Brainstorming;
using WriterApp.Shared.Collaboration;

namespace WriterApp.Controllers;

[ApiController, Authorize, Route("api/documents/{documentId:guid}/collaboration/brainstorm-draft")]
public sealed class BrainstormingSharedDraftsController(AppDbContext db, IUserIdResolver users, CollaborationAccessService access) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Read(Guid documentId, CancellationToken ct)
    {
        Response.Headers.CacheControl="no-store";
        var actor=users.ResolveUserId(User);
        var grant=await access.Require(actor,documentId,CollaborationOperation.Read,ct);
        if(!await db.Documents.AnyAsync(d=>d.Id==documentId && d.CollaborationMode==CollaborationMode.Collaborative,ct))return NotFound();
        var backend=BrainstormingValidation.Fingerprint($"{Request.Scheme}://{Request.Host}{Request.PathBase}/");
        var draft=await db.CollaborationBrainstormDrafts.AsNoTracking().Where(d=>d.DocumentId==documentId && d.BackendKey==backend && d.ResourceOwnerId==grant.ResourceOwnerId)
            .OrderByDescending(d=>d.CreatedUnixMilliseconds).ThenByDescending(d=>d.Id).FirstOrDefaultAsync(ct);
        return Ok(draft is null ? null : new BrainstormingSharedDraft(draft.Id,documentId,draft.PlainText,DateTimeOffset.FromUnixTimeMilliseconds(draft.CreatedUnixMilliseconds),draft.AuthorId==actor));
    }
}
