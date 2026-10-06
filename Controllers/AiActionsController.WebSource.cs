using Microsoft.AspNetCore.Mvc;
using WriterApp.Application.AI;
using WriterApp.Application.Documents;
using WriterApp.Shared;

namespace WriterApp.Controllers;
public sealed partial class AiActionsController
{
    private async Task<bool> WebSourceCurrent(AiActionExecuteRequestDto request, string owner, CancellationToken ct)
    {
        if (request.WebSource is null) return true;
        try {
            WebAiSources.Validate(request.WebSource);
            if (request.DocumentId != request.WebSource.DocumentId || request.SectionId != request.WebSource.SectionId || request.PageId != request.WebSource.PageId) return false;
            await new WebAiSourceService(_dbContext, owner).Require(request.WebSource, ct); return true;
        } catch (Exception e) when (e is DocumentSyncException or InvalidDataException or InvalidOperationException) { return false; }
    }
    private async Task<bool> CheckedUndoSource(AiActionUndoRedoRequestDto request,string owner,AiActionUndoRedoResult result,CancellationToken ct) {
        if(request.WebSource is not { } source || request.DocumentId!=source.DocumentId || request.SectionId!=source.SectionId || request.PageId!=source.PageId || result.ExpectedContent is null)return false;
        try {
            await new WebAiSourceService(_dbContext,owner).Require(source,ct);
            var page=request.PageId is { } id ? await _pages.GetAsync(id,owner,ct) : null;
            return page is not null && page.DocumentId==source.DocumentId && page.Content==result.ExpectedContent;
        } catch(Exception e) when(e is DocumentSyncException or InvalidDataException or InvalidOperationException) { return false; }
    }
}

[Microsoft.AspNetCore.Authorization.Authorize,ApiController,Route("api/ai/actions/web-source")]
public sealed class WebAiSourcesController(WriterApp.Data.AppDbContext db,WriterApp.Application.Security.IUserIdResolver users) : ControllerBase
{
    [HttpGet("{documentId:guid}")]
    public async Task<IActionResult> Source(Guid documentId,Guid? sectionId,Guid? pageId,Guid? sceneId,CancellationToken ct)
    {
        Response.Headers.CacheControl="no-store";
        try { return Ok(await new WebAiSourceService(db,users.ResolveUserId(User)).Capture(documentId,sectionId,pageId,sceneId,ct)); }
        catch(System.Security.SecurityException) { return Unauthorized(); }
        catch(DocumentSyncException e) { return StatusCode(e.Status,new { message=e.Message }); }
        catch(InvalidDataException e) { return UnprocessableEntity(new { message=e.Message }); }
    }
}
