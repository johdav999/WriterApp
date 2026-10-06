using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WriterApp.Application.Covers;
using WriterApp.Application.Subscriptions;
using WriterApp.Shared;

namespace WriterApp.Controllers;

public sealed partial class CoversController
{
    [HttpGet("generation-source/{project:guid}/{document:guid}")]
    public Task<IActionResult> GenerationSource(Guid project,Guid document,long revision,CancellationToken ct)=>EditBoundary(async()=>{
        if(_db is null || _assets is null || _userIds is null)throw new CoverAssetException(426,"cover.edit_backend_required","Update the checked cover backend.");
        string owner=_userIds.ResolveUserId(User);
        var state=await _db.DocumentSyncRecords.AsNoTracking().SingleOrDefaultAsync(r=>r.DocumentId==document && r.OwnerUserId==owner,ct);
        if(state is null)throw new CoverAssetException(404,"cover.not_found","Save and synchronize the owned manuscript first.");
        var source=new CoverAssetSource(1,project,document,revision,state.Version,new string('0',64));
        if(!await _assets.CurrentAsync(owner,source,ct))throw new CoverAssetException(409,"cover.stale_source","The project changed. Reload and save first.");
        return source;
    });
    [HttpGet("edit-capabilities")]
    public IActionResult EditCapabilities() { Response.Headers.CacheControl="no-store"; return Ok(_coverImageService.EditCapabilities); }

    [HttpPost("edit-source"),RequestSizeLimit(16_384)]
    public Task<IActionResult> EditSource(CoverEditSourceRequest request,CancellationToken ct)=>EditBoundary(async()=>{
        if(_assets is null || _userIds is null)throw new CoverAssetException(426,"cover.edit_backend_required","Update the owned cover edit backend.");
        return await _assets.EditSourceAsync(_userIds.ResolveUserId(User),request,ct);
    });

    [HttpPost("edit-save"),RequestSizeLimit(4_194_304)]
    public Task<IActionResult> SaveEdit(CoverEditSaveRequest request,CancellationToken ct)=>EditBoundary(async()=>{
        if(_assets is null || _userIds is null)throw new CoverAssetException(426,"cover.edit_backend_required","Update the cover save/recovery backend.");
        return await _assets.SaveEditAsync(_userIds.ResolveUserId(User),request,ct);
    });
    [HttpGet("edit-save/{operation:guid}")]
    public Task<IActionResult> ReadEditSave(Guid operation,CancellationToken ct)=>EditBoundary(async()=>{
        if(_assets is null || _userIds is null)throw new CoverAssetException(426,"cover.edit_backend_required","Update the cover save/recovery backend.");
        return await _assets.ReadSaveAsync(_userIds.ResolveUserId(User),operation,ct);
    });
    [HttpGet("edit-recovery/{project:guid}")]
    public Task<IActionResult> LatestEditSave(Guid project,CancellationToken ct)=>EditBoundary(async()=>{
        if(_assets is null || _userIds is null)throw new CoverAssetException(426,"cover.edit_backend_required","Update the cover recovery backend.");
        return await _assets.LatestSaveAsync(_userIds.ResolveUserId(User),project,ct);
    });
    [HttpPost("edit-save/{operation:guid}/restore"),RequestSizeLimit(1024)]
    public Task<IActionResult> RestoreEditSave(Guid operation,CoverEditRestoreRequest request,CancellationToken ct)=>EditBoundary(async()=>{
        if(_assets is null || _userIds is null)throw new CoverAssetException(426,"cover.edit_backend_required","Update the cover save/recovery backend.");
        return await _assets.RestoreEditAsync(_userIds.ResolveUserId(User),operation,request.ExpectedMetadataRevision,ct);
    });

    [HttpPost("edit"),RequestSizeLimit(16_384)]
    public Task<IActionResult> Edit(CoverEditRequest request,CancellationToken ct)=>EditBoundary(async()=>{
        CoverEdits.Validate(request);
        if(!_coverImageService.EditCapabilities.Operations.Contains(request.Operation))
            throw new CoverAssetException(422,"cover.edit_unsupported",_coverImageService.EditCapabilities.UnavailableReason ?? "This operation is unavailable.");
        if(_assets is null || _userIds is null)throw new CoverAssetException(426,"cover.edit_backend_required","Update the owned cover edit backend.");
        string owner=_userIds.ResolveUserId(User);
        var original=await _assets.RequireEditSourceAsync(owner,request,ct);
        var brief=new CoverPrompt {ContractVersion=1,ProjectId=request.Source.ProjectId,DocumentId=request.Source.DocumentId,
            ExpectedMetadataRevision=request.Source.MetadataRevision,ExpectedDocumentVersion=request.Source.DocumentVersion,
            Description=request.Brief.Description,Genre=request.Brief.Genre,Mood=request.Brief.Mood,Style=request.Brief.Style,ColorPalette=request.Brief.ColorPalette};
        string edited=await _coverImageService.EditAsync(original.Bytes,request.Operation,brief,ct);
        ct.ThrowIfCancellationRequested();
        await _assets.RequireEditSourceAsync(owner,request,ct);
        byte[] bytes=CoverStudioContract.ReadPng(edited);
        if(CoverAssetContract.Hash(bytes)==request.ContentHash)throw new InvalidDataException("The provider returned the unchanged cover. No new preview was selected.");
        var proposed=await _assets.RegisterInlineAsync(owner,request.Source,bytes,ct);
        await _assets.RequireEditSourceAsync(owner,request,ct);
        return await _assets.IssueEditAsync(owner,request,proposed,ct);
    });

    private async Task<IActionResult> EditBoundary(Func<Task<object>> action) {
        Response.Headers.CacheControl="no-store";
        try { return Ok(await action()); }
        catch(CoverAssetException e){return StatusCode(e.Status,new{code=e.Code,message=e.Message});}
        catch(EntitlementDeniedException e){return StatusCode(402,EntitlementDeniedApiError.ToProblemDetails(e));}
        catch(CoverImageGenerationException e){return StatusCode(e.Code is "ai.quota_exceeded" or "ai.rate_limited" ? 429 : 503,new{code=e.Code,message=e.Message});}
        catch(Exception e) when(e is InvalidDataException or ArgumentException or System.Text.Json.JsonException){return UnprocessableEntity(new{code="cover.edit_invalid",message=e.Message});}
        catch(Exception e) when(e is IOException or HttpRequestException or System.Data.Common.DbException or DbUpdateException){return StatusCode(503,new{code="cover.edit_unavailable",message="Cover edit was not acknowledged. The previous cover and cached concepts are preserved."});}
    }
}
