using System.Data.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WriterApp.Application.Covers;
using WriterApp.Application.Security;
using WriterApp.Shared;

namespace WriterApp.Controllers;

public sealed partial class CoversController
{
    [HttpPost("assets/materialize"),RequestSizeLimit(16_384)]
    public async Task<IActionResult> Materialize(CoverAssetSource source,
        [FromServices] IDeletedUserIdentityService deleted,CancellationToken ct) {
        Response.Headers.CacheControl="no-store";
        if(_assets is null || _userIds is null)return StatusCode(426,new{code="cover.asset_backend_required",message="Update the owned cover asset backend. The existing cover is preserved."});
        string owner=_userIds.ResolveUserId(User);
        if(await deleted.IsDeletedAsync(owner,ct))return Unauthorized();
        try { return Ok(await _assets.ProjectAsync(owner,source,ct)); }
        catch(CoverAssetException e){return StatusCode(e.Status,new{code=e.Code,message=e.Message});}
        catch(InvalidDataException e){return UnprocessableEntity(new{code="cover.asset_invalid",message=e.Message});}
        catch(Exception e) when(e is HttpRequestException or IOException or DbException or DbUpdateException) {
            return StatusCode(503,new{code="cover.asset_unavailable",message="Owned cover materialization was not acknowledged. Retry the same saved reference; previous cover/cache is preserved. Check backend asset migrations and storage configuration."});
        }
    }
}
