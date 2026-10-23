using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using WriterApp.Application.Collaboration;
using WriterApp.Application.Security;

namespace WriterApp.Controllers;

[ApiController, Authorize, RequestSizeLimit(16384)]
[Route("api/documents/{documentId:guid}/pages/{pageId:guid}/collaboration/sessions")]
public sealed class CollaborationSessionsController(CollaborationSessionService sessions, IUserIdResolver users) : ControllerBase
{
    [HttpPost]
    public Task<IActionResult> Issue(Guid documentId, Guid pageId, CollaborationSessionRequest request, CancellationToken ct) =>
        Run(async () => Ok(await sessions.Issue(users.ResolveUserId(User), documentId, pageId, request, ct)));

    [HttpPost("{sessionId:guid}/refresh")]
    public Task<IActionResult> Refresh(Guid documentId, Guid pageId, Guid sessionId, CollaborationSessionRefresh request, CancellationToken ct) =>
        Run(async () => Ok(await sessions.Refresh(users.ResolveUserId(User), documentId, pageId, sessionId, request.Version, ct)));

    [HttpPost("{sessionId:guid}/revoke")]
    public Task<IActionResult> Revoke(Guid documentId, Guid pageId, Guid sessionId, CancellationToken ct) => Run(async () =>
    { await sessions.Revoke(users.ResolveUserId(User), documentId, pageId, sessionId, ct); return NoContent(); });

    private async Task<IActionResult> Run(Func<Task<IActionResult>> action)
    {
        Response.Headers.CacheControl = "no-store";
        try { return await action(); }
        catch (CollaborationSessionDeniedException) { return StatusCode(403, new { code = "collaboration_session_denied", preserveLocalDraft = true }); }
    }
}

// Service-to-service only. An account cookie or a document ticket cannot authorize this route.
[ApiController, AllowAnonymous, RequestSizeLimit(16384)]
[Route("api/internal/collaboration")]
public sealed class InternalCollaborationController(CollaborationSessionService sessions, IOptionsMonitor<CollaborationSessionOptions> options,
    IOptions<CollaborationInteractionOptions>? interactions = null) : ControllerBase
{
    [HttpPost("authorize")]
    public Task<IActionResult> Authorize(CollaborationServiceRequest request, CancellationToken ct) => Run(request, false, ct);
    [HttpPost("bootstrap")]
    public Task<IActionResult> Bootstrap(CollaborationServiceRequest request, CancellationToken ct) => Run(request with { Acquire = false }, true, ct);

    private async Task<IActionResult> Run(CollaborationServiceRequest request, bool bootstrap, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        if (!CollaborationServiceAuthentication.IsAuthenticated(Request, options.CurrentValue))
            return Unauthorized();
        try
        {
            var session = await sessions.Authorize(request, ct);
            return bootstrap ? Ok(await sessions.Bootstrap(session, ct)) : Ok(new { readOnly = true, commitAllowed = session.CommitAllowed && options.CurrentValue.CommitsEnabled,
                session.InstallationId, session.SchemaFingerprint, session.ContentContractVersion, session.ProtocolVersion, session.ExpiresAtUnixMilliseconds,
                actorId = session.OwnerUserId,
                displayName = interactions?.Value.Enabled == true ? await sessions.PresenceDisplayName(session, ct) : null,
                interactionsEnabled = interactions?.Value.Enabled == true });
        }
        catch (CollaborationSessionDeniedException) { return StatusCode(403, new { code = "collaboration_session_denied" }); }
    }
}
