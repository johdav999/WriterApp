using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WriterApp.Application.Collaboration;
using WriterApp.Application.Security;
using WriterApp.Shared.Collaboration;

namespace WriterApp.Controllers;

[ApiController, Authorize, RequestSizeLimit(65536), Route("api/documents/{documentId:guid}/pages/{pageId:guid}/collaboration/comments")]
public sealed class CollaborationCommentsController(CollaborationCommentService comments, IUserIdResolver users) : ControllerBase
{
    [HttpPost("read")]
    public async Task<IActionResult> Read(Guid documentId, Guid pageId, CollaborationCommentRead request, CancellationToken ct)
    { Response.Headers.CacheControl = "no-store"; return Ok(await comments.Read(users.ResolveUserId(User), documentId, pageId, request.Generation, ct)); }
    [HttpPost]
    public async Task<IActionResult> Create(Guid documentId, Guid pageId, CollaborationCreateComment request, CancellationToken ct)
    { Response.Headers.CacheControl = "no-store"; return Ok(new { revision = await comments.Create(users.ResolveUserId(User), documentId, pageId, request, ct) }); }
    [HttpPost("{threadId:guid}")]
    public async Task<IActionResult> Change(Guid documentId, Guid pageId, Guid threadId, CollaborationCommentMutation request, CancellationToken ct)
    { Response.Headers.CacheControl = "no-store"; return Ok(new { revision = await comments.Change(users.ResolveUserId(User), documentId, pageId, threadId, request, ct) }); }
    [HttpPost("import-legacy")]
    public async Task<IActionResult> Import(Guid documentId, Guid pageId, CollaborationCommentRead request, CancellationToken ct)
    { Response.Headers.CacheControl = "no-store"; return Ok(new { imported = await comments.ImportLegacy(users.ResolveUserId(User), documentId, pageId, request.Generation, ct) }); }
}
