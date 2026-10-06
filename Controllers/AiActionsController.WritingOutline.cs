using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WriterApp.Shared;

namespace WriterApp.Controllers;

public sealed partial class AiActionsController
{
    [HttpGet("writing-outline/{documentId:guid}")]
    public async Task<ActionResult<WritingOutlineSnapshot>> GetWritingOutline(Guid documentId,CancellationToken ct)
    {
        Response.Headers.CacheControl="no-store";
        try {
            var snapshot=await ReadWritingOutlineAsync(documentId,_userIdResolver.ResolveUserId(User),ct);
            return snapshot is null ? NotFound() : Ok(snapshot);
        } catch(InvalidDataException e) { return UnprocessableEntity(new{code="ai.invalid_outline",message=e.Message}); }
    }
    private async Task<WritingOutlineSnapshot?> ReadWritingOutlineAsync(Guid id,string owner,CancellationToken ct)
    {
        if(await _dbContext.DeletedUserIdentities.AsNoTracking().AnyAsync(x=>x.UserId==owner,ct))return null;
        var doc=await _dbContext.Documents.AsNoTracking().SingleOrDefaultAsync(d=>d.Id==id && d.OwnerUserId==owner && d.DeletedAtUtc==null && !d.IsArchived,ct);
        if(doc is null)return null;
        if(doc.ProjectId is { } project && !await _dbContext.Projects.AsNoTracking().AnyAsync(p=>p.Id==project && p.OwnerUserId==owner,ct))return null;
        var sync=await _dbContext.DocumentSyncRecords.AsNoTracking().SingleOrDefaultAsync(r=>r.DocumentId==id && r.OwnerUserId==owner && !r.IsDeleted && !r.IsTrashed,ct);
        if(sync is null)throw new InvalidDataException("This backend has no active saved source version. Save/synchronize the manuscript before generating writing.");
        var sections=await _dbContext.Sections.AsNoTracking().Where(s=>s.DocumentId==id).OrderBy(s=>s.OrderIndex).ThenBy(s=>s.Id)
            .Take(WritingOutline.MaxEntities+1).Select(s=>new WritingOutlineSection(s.Id,s.OrderIndex,s.Title)).ToListAsync(ct);
        var nodes=await _dbContext.ProjectNodes.AsNoTracking().Where(n=>n.DocumentId==id && n.ProjectId==doc.ProjectId && n.SyncDeletionId==null)
            .OrderBy(n=>n.Id).Take(WritingOutline.MaxEntities+1).ToListAsync(ct);
        return WritingOutline.Create(id,doc.ProjectId,sync.Version,sections,
            nodes.Select(n=>new WritingOutlineNode(n.Id,n.ParentId,n.OrderIndex,n.NodeType.ToString().ToLowerInvariant(),n.Title,n.LinkedSectionId)));
    }
    private async Task<bool> WritingOutlineCurrentAsync(Guid document,string owner,WritingOutlineSnapshot source,CancellationToken ct,bool contentChecked=false)
    {
        try { var current=await ReadWritingOutlineAsync(document,owner,ct);return current is not null && (contentChecked || current.DocumentVersion==source.DocumentVersion) && current.Fingerprint==source.Fingerprint; }
        catch(InvalidDataException) { return false; }
    }
}
