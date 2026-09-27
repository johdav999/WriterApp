using System.Data.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WriterApp.Application.Documents;
using WriterApp.Application.Security;
using WriterApp.Shared.Sync;

namespace WriterApp.Controllers;

[ApiController]
[Authorize]
[Route("api/sync/v1/documents")]
public sealed class DocumentSyncController(DocumentSyncService sync, IUserIdResolver users) : ControllerBase
{
    [HttpGet("changes")]
    public Task<IActionResult> Changes([FromQuery] string? cursor = null, [FromQuery] int limit = 50, CancellationToken ct = default) =>
        RunAsync(() => sync.ChangesAsync(users.ResolveUserId(User), cursor, limit, ct));

    [HttpGet("{id:guid}")]
    public Task<IActionResult> Download(Guid id, CancellationToken ct) => RunAsync(async () =>
    {
        var result = await sync.DownloadAsync(users.ResolveUserId(User), id, ct);
        Response.Headers.ETag = $"\"{result.State.Version}\"";
        return result;
    });

    [HttpPost("{id:guid}/operations")]
    [RequestSizeLimit(DocumentSyncService.MaxRequestBytes)]
    public Task<IActionResult> Mutate(Guid id, [FromBody] SyncMutation request, CancellationToken ct) => RunAsync(async () =>
    {
        var result = await sync.MutateAsync(users.ResolveUserId(User), id, request, ct);
        Response.Headers.ETag = $"\"{result.State.Version}\"";
        return result;
    });

    private async Task<IActionResult> RunAsync<T>(Func<Task<T>> action)
    {
        Response.Headers.CacheControl = "no-store";
        try { return Ok(await action()); }
        catch (DocumentSyncException error) { return StatusCode(error.Status, error.Error); }
        catch (DeletedUserIdentityException) { return StatusCode(403, new SyncError("account_deleted", "This account has been deleted.")); }
        catch (Exception error) when (error is DbUpdateException or DbException)
        {
            Response.Headers.RetryAfter = "2";
            return StatusCode(503, new SyncError("sync_storage_unavailable", "The transaction was not acknowledged. Retry with the same operation ID and request."));
        }
    }
}
