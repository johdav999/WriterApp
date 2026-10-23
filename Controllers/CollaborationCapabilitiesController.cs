using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WriterApp.Application.Collaboration;
using WriterApp.Application.Security;
using WriterApp.Data;
using WriterApp.Shared.Collaboration;

namespace WriterApp.Controllers;

[ApiController, Authorize]
[Route("api/documents/{documentId:guid}/collaboration/capabilities")]
public sealed class CollaborationCapabilitiesController(AppDbContext db, IUserIdResolver users, CollaborationAccessService? access = null,
    Microsoft.Extensions.Options.IOptions<CollaborationInteractionOptions>? interactions = null,
    Microsoft.Extensions.Options.IOptionsMonitor<CollaborationSessionOptions>? sessionOptions = null, CollaborationStagedTestGate? stagedTest = null) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<CollaborationCapabilities>> Get(Guid documentId,
        [FromQuery] int? protocolVersion = null, [FromQuery] string? schemaFingerprint = null,
        [FromQuery] Guid? pageId = null, [FromQuery] Guid? generation = null, CancellationToken ct = default)
    {
        Response.Headers.CacheControl = "no-store";
        string owner = users.ResolveUserId(User);
        CollaborationAccess? grant = null;
        if (access is not null) grant = await access.Require(owner, documentId, CollaborationOperation.Read, ct);
        var document = await db.Documents.AsNoTracking().SingleOrDefaultAsync(d => d.Id == documentId && (access != null || d.OwnerUserId == owner), ct);
        if (document is null) return NotFound();
        var pages = await db.Pages.AsNoTracking().Where(p => p.DocumentId == documentId && (pageId == null || p.Id == pageId))
            .OrderBy(p => p.Id).Select(p => p.Id).ToArrayAsync(ct);
        if (pageId.HasValue && pages.Length == 0) return NotFound();
        var states = await db.CollaborationPages.AsNoTracking().Where(p => p.DocumentId == documentId && pages.Contains(p.PageId) && !p.IsDeleted).ToArrayAsync(ct);
        // Ambiguous/missing state is never permission to load a writable collaborative editor.
        var capabilities = pages.Select(id =>
        {
            var matches = states.Where(p => p.PageId == id).ToArray();
            var state = matches.Length == 1 ? matches[0] : null;
            return new CollaborationPageCapability(id, state?.Generation, state?.ContentContractVersion ?? 0,
                state?.ProtocolVersion ?? 0, state?.SchemaFingerprint, state?.CommittedSequence ?? 0, state?.ProjectionSequence ?? 0);
        }).ToArray();
        bool legacy = document.CollaborationMode == CollaborationMode.Legacy;
        bool available = document.DeletedAtUtc is null && !document.IsArchived;
        bool feature = stagedTest?.Enabled == true;
        CollaborationError? reason = !available
            ? new("collaboration_document_unavailable", "Restore this manuscript before editing.")
            : document.CollaborationMode == CollaborationMode.Migrating
                ? CollaborationWriteException.ForMode(document.CollaborationMode)
                : legacy || feature ? null
                    : new(CollaborationErrors.UpgradeRequired, "Collaborative editing is not available in this build. Keep your local draft.");
        if (!legacy && document.CollaborationMode != CollaborationMode.Migrating && generation.HasValue && pageId.HasValue
            && capabilities.Single().Generation != generation)
            reason = new(CollaborationErrors.GenerationChanged, "The page generation changed. Preserve your local draft for recovery.");
        // The local staging editor must match every requested page's schema/protocol.
        else if (!legacy && document.CollaborationMode != CollaborationMode.Migrating &&
            capabilities.Any(p => p.Generation is null || p.ProtocolVersion != protocolVersion || p.SchemaFingerprint != schemaFingerprint))
            reason = new(CollaborationErrors.UpgradeRequired, "This editor does not support the manuscript's collaboration schema and protocol. Preserve your local draft.");
        var recovery = await db.CollaborationRecoveryStates.AsNoTracking().SingleOrDefaultAsync(ct);
        bool writable = feature && available && document.CollaborationMode == CollaborationMode.Collaborative && reason is null
            && (grant is null || grant.Role is CollaborationRole.Owner or CollaborationRole.Editor);
        return Ok(new CollaborationCapabilities(documentId, document.CollaborationMode, document.CollaborationMigrationState,
            feature, true, legacy && available && (grant is null || grant.Role == CollaborationRole.Owner), writable,
            grant?.Role.ToString().ToLowerInvariant() ?? "owner", legacy && available ? ["read", "legacy-write"] : writable ? ["read","collaborative-write"] : ["read"], reason, capabilities, grant?.MembershipRevision ?? 0,
            interactions?.Value.Enabled == true && document.CollaborationMode == CollaborationMode.Collaborative, recovery?.RequiredEnvironment ?? sessionOptions?.CurrentValue.Environment));
    }
}
