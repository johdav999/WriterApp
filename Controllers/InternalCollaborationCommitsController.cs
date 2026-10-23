using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using WriterApp.Application.Collaboration;
using WriterApp.Data;
using Microsoft.EntityFrameworkCore;

namespace WriterApp.Controllers;

internal static class CollaborationServiceAuthentication
{
    public static bool IsAuthenticated(HttpRequest request, CollaborationSessionOptions options)
    {
        string supplied = request.Headers.Authorization.ToString();
        return options.IsConfigured && supplied.Length <= 1024 && supplied.StartsWith("Bearer ", StringComparison.Ordinal)
            && CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(options.ServiceKey)), SHA256.HashData(Encoding.UTF8.GetBytes(supplied[7..])));
    }
}

[ApiController, AllowAnonymous, RequestSizeLimit(24 * 1024 * 1024)]
[Route("api/internal/collaboration")]
public sealed class InternalCollaborationCommitsController(CollaborationCommitService commits, AppDbContext db,
    IOptionsMonitor<CollaborationSessionOptions> options) : ControllerBase
{
    [HttpPost("lease")]
    public Task<IActionResult> Lease(CollaborationLeaseRequest request, CancellationToken ct) => Run(async () => Ok(await commits.Lease(request, ct)));
    [HttpPost("commit")]
    public Task<IActionResult> Commit(CollaborationCommitRequest request, CancellationToken ct) => Run(async () => Ok(await commits.Commit(request, ct)));
    [HttpPost("checkpoint")]
    public Task<IActionResult> Checkpoint(CollaborationCheckpointRequest request, CancellationToken ct) => Run(async () => Ok(new { snapshotSequence = await commits.Checkpoint(request, ct) }));
    [HttpPost("health")]
    public Task<IActionResult> Health(CancellationToken ct) => Run(async () =>
    {
        await db.CollaborationPages.AsNoTracking().Select(p => p.CommittedSequence).Take(1).ToArrayAsync(ct);
        var recovery = await db.CollaborationRecoveryStates.AsNoTracking().SingleOrDefaultAsync(ct);
        return Ok(new { ready = recovery is null || recovery.RequiredEnvironment == options.CurrentValue.Environment, commitsEnabled = options.CurrentValue.CommitsEnabled });
    });

    private async Task<IActionResult> Run(Func<Task<IActionResult>> work)
    {
        Response.Headers.CacheControl = "no-store";
        if (!CollaborationServiceAuthentication.IsAuthenticated(Request, options.CurrentValue)) return Unauthorized();
        try { return await work(); }
        catch (CollaborationSessionDeniedException) { return StatusCode(403, new { code = "collaboration_session_denied", preserveLocalDraft = true }); }
        catch (CollaborationCommitException e) { return Conflict(new { code = e.Code, preserveLocalDraft = true }); }
    }
}
