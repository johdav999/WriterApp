using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WriterApp.Application.AI;
using WriterApp.Application.Collaboration;
using WriterApp.Application.Documents;
using WriterApp.Application.Security;
using WriterApp.Data;
using WriterApp.Shared.Collaboration;

namespace WriterApp.Controllers;

[ApiController, Authorize, Route("api/documents/{documentId:guid}/collaboration/tools")]
public sealed class CollaborationEditorToolsController(AppDbContext db, ICollaborationCodec codec,
    CollaborationAccessService access, IUserIdResolver users) : ControllerBase
{
    private string Actor => users.ResolveUserId(User);
    public sealed record NotesEdit(string Before, string Notes);
    [HttpGet("sections/{sectionId:guid}/notes")]
    public async Task<IActionResult> Notes(Guid documentId,Guid sectionId,CancellationToken ct) {
        await RequireSection(documentId,sectionId,CollaborationOperation.Read,ct);
        var note=await db.SectionNotes.AsNoTracking().SingleOrDefaultAsync(n=>n.SectionId==sectionId,ct);
        return Ok(new SectionNotesDto(sectionId,note?.NotesText ?? "",note?.UpdatedAtUtc ?? DateTimeOffset.UnixEpoch));
    }
    [HttpPut("sections/{sectionId:guid}/notes"),RequestSizeLimit(600_000)]
    public async Task<IActionResult> SaveNotes(Guid documentId,Guid sectionId,NotesEdit request,CancellationToken ct) {
        if(request.Before is null || request.Notes is null || request.Notes.Length>200_000 || request.Before.Length>200_000)return BadRequest();
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async()=> {
            await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);
            await CollaborationMigrationService.Lock(db,documentId,ct);
            await RequireSection(documentId,sectionId,CollaborationOperation.WriteText,ct);
            var note=await db.SectionNotes.AsNoTracking().SingleOrDefaultAsync(n=>n.SectionId==sectionId,ct);
            if((note?.NotesText ?? "")!=request.Before && note?.NotesText!=request.Notes)return (IActionResult)Conflict(new {message="Another collaborator changed these notes. Your draft is retained; reload the notes before replacing them."});
            var now=DateTimeOffset.UtcNow;
            if(note is null)await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO SectionNotes(SectionId,NotesText,UpdatedAtUtc) VALUES({sectionId},{request.Notes},{now})",ct);
            else await db.SectionNotes.Where(n=>n.SectionId==sectionId).ExecuteUpdateAsync(n=>n.SetProperty(x=>x.NotesText,request.Notes).SetProperty(x=>x.UpdatedAtUtc,now),ct);
            await tx.CommitAsync(ct);return (IActionResult)Ok(new SectionNotesDto(sectionId,request.Notes,now));
        });
    }
    private async Task RequireSection(Guid document,Guid section,CollaborationOperation operation,CancellationToken ct) {
        Response.Headers.CacheControl="no-store";
        await access.Require(Actor,document,operation,ct);
        if(!await db.CollaborationPages.AnyAsync(p=>p.DocumentId==document && !p.IsDeleted && db.Pages.Any(x=>x.Id==p.PageId && x.SectionId==section),ct))throw new CollaborationSessionDeniedException();
    }
    [HttpPost("structure"), RequestSizeLimit(100_000)]
    public async Task<IActionResult> Structure(Guid documentId, CollaborationStructureCommand request, CancellationToken ct) =>
        Ok(new { sectionId = await new CollaborationStructureCommands(db, codec, access).Execute(Actor, documentId, request, ct) });
    [HttpPost("replace"), RequestSizeLimit(3_000_000)]
    public async Task<IActionResult> Replace(Guid documentId, CollaborationCheckedEdit request, CancellationToken ct)
    {
        var result = await new CollaborationCheckedEdits(db, codec, access).Apply(Actor, documentId, request, ct);
        Response.Headers["X-WriterApp-Checked-Save"] = "1";
        if (request.HistoryOperationId is { } operation) Response.Headers["X-WriterApp-AI-Operation"] = operation.ToString();
        return Ok(result);
    }
    [HttpGet("pages/{pageId:guid}/versions")]
    public async Task<IActionResult> Versions(Guid documentId, Guid pageId, CancellationToken ct)
    {
        await RequirePage(documentId, pageId, ct);
        var generations = await (from p in db.CollaborationPages.AsNoTracking()
            join s in db.CollaborationSnapshots.AsNoTracking() on new { p.PageId, p.Generation } equals new { s.PageId, s.Generation }
            join v in db.CollaborationProjections.AsNoTracking() on new { p.PageId, p.Generation } equals new { v.PageId, v.Generation }
            where p.DocumentId == documentId && p.PageId == pageId && p.IsDeleted && s.Sequence == p.SnapshotSequence
            select new { p.Generation, s.CreatedAtUnixMilliseconds, v.Text, v.Html }).ToArrayAsync(ct);
        var legacy = await db.PageVersions.AsNoTracking().Where(v => v.DocumentId == documentId && v.PageId == pageId).ToArrayAsync(ct);
        return Ok(generations.Select(v => new PageVersionListItemDto(v.Generation, pageId, DateTimeOffset.FromUnixTimeMilliseconds(v.CreatedAtUnixMilliseconds),
            "Before shared replacement", v.Text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length, System.Text.Encoding.UTF8.GetByteCount(v.Html)))
            .Concat(legacy.Select(v => new PageVersionListItemDto(v.Id, pageId, v.CreatedAt, v.Reason, v.WordCount, v.SizeBytes))).OrderByDescending(v => v.CreatedAt));
    }
    [HttpGet("pages/{pageId:guid}/versions/{versionId:guid}")]
    public async Task<IActionResult> Version(Guid documentId, Guid pageId, Guid versionId, CancellationToken ct)
    {
        await RequirePage(documentId, pageId, ct);
        var content=await VersionContent(documentId,pageId,versionId,ct);
        return content is null ? NotFound() : Ok(new {content});
    }
    [HttpGet("pages/{pageId:guid}/versions/diff")]
    public async Task<IActionResult> Diff(Guid documentId,Guid pageId,Guid fromVersionId,Guid? toVersionId,string? granularity,int? maxLines,CancellationToken ct)
    {
        await RequirePage(documentId,pageId,ct);
        var before=await VersionContent(documentId,pageId,fromVersionId,ct);
        var after=toVersionId is { } id ? await VersionContent(documentId,pageId,id,ct)
            : (await CollaborationProjectionService.Read(db,Actor,documentId,new(),ct,access)).Pages.Single(p=>p.PageId==pageId).Html;
        if(before is null || after is null)return NotFound();
        return Ok(new PageVersionDiffService().BuildDiff(pageId,fromVersionId,toVersionId,toVersionId is null,before,after,granularity ?? "word",Math.Clamp(maxLines ?? 800,1,2000)));
    }
    private async Task<string?> VersionContent(Guid documentId,Guid pageId,Guid versionId,CancellationToken ct)
    {
        var projection = await db.CollaborationProjections.AsNoTracking().SingleOrDefaultAsync(p => p.PageId == pageId && p.Generation == versionId, ct);
        if (projection is not null) return projection.Html;
        var legacy = await db.PageVersions.AsNoTracking().SingleOrDefaultAsync(v => v.Id == versionId && v.PageId == pageId && v.DocumentId == documentId, ct);
        if (legacy is null) return null;
        using var input = new MemoryStream(legacy.ContentCompressed);
        using var gzip = new System.IO.Compression.GZipStream(input, System.IO.Compression.CompressionMode.Decompress);
        using var reader = new StreamReader(gzip);
        return await reader.ReadToEndAsync(ct);
    }
    private async Task RequirePage(Guid document, Guid page, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        await access.Require(Actor, document, CollaborationOperation.Read, ct);
        if (!await db.CollaborationPages.AnyAsync(p => p.DocumentId == document && p.PageId == page && !p.IsDeleted, ct)) throw new CollaborationSessionDeniedException();
    }
}
