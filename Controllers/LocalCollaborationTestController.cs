using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WriterApp.Application.Collaboration;
using WriterApp.Application.Security;
using WriterApp.Data;
using WriterApp.Data.Documents;
using WriterApp.Shared.Collaboration;

namespace WriterApp.Controllers;

[ApiController,Authorize,Route("api/collaboration/local-test")]
public sealed class LocalCollaborationTestController(AppDbContext db,CollaborationStagedTestGate gate,IUserIdResolver users,
    CollaborationMigrationService migrations):ControllerBase
{
    public sealed record Create(Guid DocumentId,Guid ProjectId,Guid SectionId,Guid PageId);
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        if(!gate.Enabled)return NotFound();string actor=users.ResolveUserId(User);
        var documents=await db.Documents.AsNoTracking().Where(d=>d.OwnerUserId==actor&&!d.IsArchived&&d.DeletedAtUtc==null)
            .OrderByDescending(d=>d.CreatedAtUnixSeconds).ThenBy(d=>d.Id).Take(100).Select(d=>new{documentId=d.Id,d.Title,d.CollaborationMode}).ToArrayAsync(ct);
        return Ok(new{documents,localTest=true});
    }
    [HttpPost]
    public async Task<IActionResult> New(Create request,CancellationToken ct)
    {
        if(!gate.Enabled)return NotFound();
        if(new[]{request.DocumentId,request.ProjectId,request.SectionId,request.PageId}.Any(id=>id==Guid.Empty)
            ||new[]{request.DocumentId,request.ProjectId,request.SectionId,request.PageId}.Distinct().Count()!=4)return BadRequest();
        string actor=users.ResolveUserId(User);
        var existing=await db.Documents.AsNoTracking().SingleOrDefaultAsync(d=>d.Id==request.DocumentId,ct);
        if(existing is null)
        {
            if(await db.Projects.AnyAsync(p=>p.Id==request.ProjectId,ct)||await db.Sections.AnyAsync(p=>p.Id==request.SectionId,ct)||await db.Pages.AnyAsync(p=>p.Id==request.PageId,ct))return Conflict();
            var now=DateTimeOffset.UtcNow;
            db.Projects.Add(new(){Id=request.ProjectId,OwnerUserId=actor,Title="Local collaboration test",CreatedUtc=now,UpdatedUtc=now});
            db.Documents.Add(new(){Id=request.DocumentId,ProjectId=request.ProjectId,OwnerUserId=actor,Title="Two-account collaboration test",CreatedAt=now,UpdatedAt=now,CreatedAtUnixSeconds=now.ToUnixTimeSeconds(),UpdatedAtUnixSeconds=now.ToUnixTimeSeconds()});
            db.Sections.Add(new(){Id=request.SectionId,DocumentId=request.DocumentId,Title="Shared writing",CreatedAt=now,UpdatedAt=now});
            db.Pages.Add(new(){Id=request.PageId,DocumentId=request.DocumentId,SectionId=request.SectionId,Title="Test page",Content="<p>Write together here.</p>",CreatedAt=now,UpdatedAt=now});
            await db.SaveChangesAsync(ct);
        }
        else if(existing.OwnerUserId!=actor||existing.ProjectId!=request.ProjectId
            ||!await db.Sections.AnyAsync(s=>s.Id==request.SectionId&&s.DocumentId==request.DocumentId,ct)
            ||!await db.Pages.AnyAsync(p=>p.Id==request.PageId&&p.DocumentId==request.DocumentId&&p.SectionId==request.SectionId,ct))return Conflict();
        // The database triggers assign the source version after all page/section writes.
        // Retries of an existing migration must retain the version captured by that job.
        var version=await db.CollaborationMigrationJobs.AsNoTracking().Where(j=>j.DocumentId==request.DocumentId).Select(j=>j.SourceVersion).SingleOrDefaultAsync(ct)
            ??await db.DocumentSyncRecords.AsNoTracking().Where(d=>d.DocumentId==request.DocumentId&&d.OwnerUserId==actor&&!d.IsDeleted&&!d.IsTrashed).Select(d=>d.Version).SingleAsync(ct);
        await migrations.Begin(actor,request.DocumentId,new(request.DocumentId,version,true),ct);
        var progress=await migrations.Resume(actor,request.DocumentId,request.DocumentId,ct);
        return Ok(new{documentId=request.DocumentId,progress});
    }
}
