using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WriterApp.Application.Collaboration;
using WriterApp.Application.Security;
using WriterApp.Data;
using WriterApp.Shared.Collaboration;

namespace WriterApp.Controllers;

[ApiController, Authorize, RequestSizeLimit(128 * 1024), Route("api/documents/{documentId:guid}/collaboration")]
public sealed class CollaborationManuscriptsController(CollaborationMigrationService migrations, AppDbContext db,
    IUserIdResolver users, IOptions<CollaborationConversionOptions> options, CollaborationAccessService? access = null) : ControllerBase
{
    [HttpGet("workspace")]
    public async Task<IActionResult> Workspace(Guid documentId,CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        if(access is null)throw new CollaborationSessionDeniedException();
        var snapshot=await CollaborationProjectionService.Read(db,users.ResolveUserId(User),documentId,new(),ct,access);
        var d=await db.Documents.AsNoTracking().SingleAsync(d=>d.Id==documentId,ct);
        var sections=await db.Sections.AsNoTracking().Where(s=>s.DocumentId==documentId && snapshot.Sections.Select(x=>x.SectionId).Contains(s.Id)).OrderBy(s=>s.OrderIndex).ToArrayAsync(ct);
        return Ok(new {
            document=new WriterApp.Application.Documents.DocumentDetailDto(d.Id,d.Title,d.CreatedAt,d.UpdatedAt,d.LanguageCode,d.TranslationGroupId,d.IsArchived,d.ArchivedAt,null,d.ProjectId,"manuscript"),
            sections=sections.Select(s=>new WriterApp.Application.Documents.SectionDto(s.Id,s.DocumentId,s.Title,s.NarrativePurpose,s.OrderIndex,s.CreatedAt,s.UpdatedAt,s.LanguageCode,s.TranslationGroupId)),
            pages=snapshot.Pages.Select(p=>new WriterApp.Application.Documents.PageDto(p.PageId,documentId,p.SectionId,p.Title,p.Html,p.OrderIndex,d.CreatedAt,d.UpdatedAt))
        });
    }
    [HttpGet("setup")]
    public async Task<IActionResult> Setup(Guid documentId,CancellationToken ct)
    {
        if(!options.Value.Enabled||access is null)throw new CollaborationSessionDeniedException();
        await access.Require(users.ResolveUserId(User),documentId,CollaborationOperation.Manage,ct);
        var document=await db.Documents.AsNoTracking().SingleAsync(d=>d.Id==documentId,ct);
        var job=await db.CollaborationMigrationJobs.AsNoTracking().SingleOrDefaultAsync(j=>j.DocumentId==documentId,ct);
        var version=job?.SourceVersion??await db.DocumentSyncRecords.AsNoTracking().Where(s=>s.DocumentId==documentId).Select(s=>s.Version).SingleAsync(ct);
        return Ok(new{document.Title,document.CollaborationMode,expectedVersion=version,operationId=job?.Id});
    }
    [HttpGet("migration")]
    public async Task<IActionResult> Status(Guid documentId, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store"; string owner = users.ResolveUserId(User);
        var document = await db.Documents.AsNoTracking().SingleOrDefaultAsync(d => d.Id == documentId && d.OwnerUserId == owner, ct);
        if (document is null || await db.DeletedUserIdentities.AnyAsync(u => u.UserId == owner, ct)) return NotFound();
        var job = await db.CollaborationMigrationJobs.AsNoTracking().SingleOrDefaultAsync(j => j.DocumentId == documentId, ct);
        if (job is null) return NotFound();
        return Ok(new CollaborationMigrationProgress(job.Id, document.CollaborationMode, await db.CollaborationPages.CountAsync(p => p.DocumentId == documentId, ct),
            await db.Pages.CountAsync(p => p.DocumentId == documentId, ct), job.FailureCode));
    }
    [HttpPost("migration")]
    public async Task<IActionResult> Begin(Guid documentId, CollaborationMigrationRequest request, CancellationToken ct) =>
        Ok(await migrations.Begin(users.ResolveUserId(User), documentId, request, ct));
    [HttpPost("migration/{jobId:guid}/resume")]
    public async Task<IActionResult> Resume(Guid documentId, Guid jobId, CancellationToken ct) =>
        Ok(await migrations.Resume(users.ResolveUserId(User), documentId, jobId, ct));
    [HttpPost("read")]
    public async Task<IActionResult> Read(Guid documentId, CollaborationReadRequest request, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        return Ok(await CollaborationProjectionService.Read(db, users.ResolveUserId(User), documentId, request, ct, access));
    }
    [HttpPost("ai-context")]
    public async Task<IActionResult> AiContext(Guid documentId, CollaborationReadRequest request, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        string actor = users.ResolveUserId(User);
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            var snapshot = await CollaborationProjectionService.Read(db, actor, documentId, request, ct, access, CollaborationOperation.PrepareAi);
            var supporting = new List<object>(); var ids = request.SupportingDocumentIds ?? [];
            if (ids.Length > 10 || ids.Distinct().Count() != ids.Length || ids.Length > 0 && access is null) throw new CollaborationSessionDeniedException();
            foreach (var id in ids)
            {
                var source = await CollaborationSupportingReader.Read(db, access!, actor, documentId, id, CollaborationOperation.PrepareAi, ct);
                supporting.Add(new { source.DocumentId, source.Title, source.SourceSha256, source.Text });
            }
            await tx.CommitAsync(ct);
            return Ok(new { snapshot.DocumentId, snapshot.StructureRevision,
                watermark = snapshot.Pages.Select(p => new CollaborationWatermark(p.PageId, p.Generation, p.Sequence)),
                pages = snapshot.Pages.Select(p => new { p.PageId, p.SectionId, p.Text }), supporting });
        });
    }
    [HttpPost("delete-page")]
    public async Task<IActionResult> DeletePage(Guid documentId, CollaborationDeletePage request, CancellationToken ct)
    {
        if (!options.Value.Enabled) throw new CollaborationSessionDeniedException();
        return Ok(new { structureRevision = await CollaborationProjectionService.DeletePage(db, users.ResolveUserId(User), documentId, request, ct) });
    }
}
