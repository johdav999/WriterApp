using Microsoft.EntityFrameworkCore;
using WriterApp.Data;
using WriterApp.Shared.Brainstorming;
using WriterApp.Shared.Collaboration;

namespace WriterApp.Application.Collaboration;

// Used only inside the Brainstorm receipt transaction, after acquiring the same
// document lock as commits, membership changes and structure commands.
internal sealed class CollaborationBrainstormingWrites(AppDbContext db, string actor, CollaborationAccessService? access, ICollaborationCodec? codec)
{
    public async Task<bool> Begin(Guid document, Guid operation, CancellationToken ct)
    {
        await CollaborationMigrationService.Lock(db, document, ct);
        var mode = await db.Documents.AsNoTracking().Where(d => d.Id == document).Select(d => d.CollaborationMode).SingleAsync(ct);
        if (mode == CollaborationMode.Legacy) return false;
        if (mode != CollaborationMode.Collaborative) throw new CollaborationWriteException(CollaborationWriteException.ForMode(mode));
        if (access is null) throw new CollaborationWriteException(CollaborationWriteException.ForMode(mode));
        await access.Require(actor, document, CollaborationOperation.WriteText, ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO CollaborationPlanningPermits(DocumentId,OperationId) VALUES({document},{operation})", ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO CollaborationStructurePermits(DocumentId,OperationId) VALUES({document},{operation})", ct);
        return true;
    }
    public async Task End(Guid document, Guid operation, CancellationToken ct)
    {
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM CollaborationStructurePermits WHERE DocumentId={document} AND OperationId={operation}", ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM CollaborationPlanningPermits WHERE DocumentId={document} AND OperationId={operation}", ct);
    }
    public async Task Retire(Guid document, Guid pageId, CancellationToken ct)
    {
        var page = await db.CollaborationPages.SingleAsync(p => p.DocumentId == document && p.PageId == pageId && !p.IsDeleted, ct);
        page.IsDeleted = true; page.LeaseEpoch++; page.LeaseOwner = null; page.LeaseExpiresAtUnixMilliseconds = 0;
        await db.CollaborationSessions.Where(s => s.PageId == pageId && s.Generation == page.Generation && !s.Revoked)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Revoked, true).SetProperty(x => x.Version, x => x.Version + 1), ct);
        await db.CollaborationOutboxEvents.Where(e => e.PageId == pageId && e.Generation == page.Generation && e.CompletedAtUnixMilliseconds == null)
            .ExecuteUpdateAsync(e => e.SetProperty(x => x.CompletedAtUnixMilliseconds, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()), ct);
    }
    public async Task Publish(Guid document, Guid pageId, Guid operation, string html, string? before, CancellationToken ct)
    {
        if (codec is null) throw new InvalidOperationException("Shared manuscript codec is unavailable.");
        var seed = await codec.Seed(html, "Html", ct);
        if (Canonical(seed.Html) != Canonical(html)) throw new InvalidOperationException("Review the canonical shared passage before applying.");
        if (before is not null)
        {
            var active = await db.CollaborationPages.AsNoTracking().SingleAsync(p => p.DocumentId == document && p.PageId == pageId && !p.IsDeleted, ct);
            var projection = await db.CollaborationProjections.AsNoTracking().SingleAsync(p => p.PageId == pageId && p.Generation == active.Generation, ct);
            if (projection.Sequence != active.CommittedSequence || Canonical(projection.Html) != Canonical(before)) throw new InvalidOperationException("Affected writing changed. Refresh the review; later writing is retained.");
            await Retire(document, pageId, ct);
        }
        else if (await db.CollaborationPages.AnyAsync(p => p.PageId == pageId && !p.IsDeleted, ct)) throw new InvalidOperationException("Page identity is already active.");
        db.CollaborationPages.Add(new() { DocumentId = document, PageId = pageId, Generation = operation, ContentContractVersion = seed.Support.ContentContractVersion, ProtocolVersion = seed.Support.ProtocolVersion, SchemaFingerprint = seed.Support.SchemaFingerprint });
        db.CollaborationSnapshots.Add(new() { PageId = pageId, Generation = operation, State = seed.State, Sha256 = seed.Sha256, CreatedAtUnixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() });
        db.CollaborationProjections.Add(new() { PageId = pageId, Generation = operation, Html = seed.Html, Json = seed.Json.GetRawText(), Text = seed.Text, StateSha256 = seed.Sha256 });
    }
    internal static string Canonical(string html)=>new AngleSharp.Html.Parser.HtmlParser().ParseDocument(html).Body!.InnerHtml;
    public async Task Record(Guid document, Guid operation, string request, bool structure, CancellationToken ct)
    {
        var manifest = await db.CollaborationManifests.SingleAsync(m => m.DocumentId == document, ct);
        if (structure) manifest.StructureRevision++;
        db.CollaborationCommands.Add(new() { Id = operation, DocumentId = document, RequestSha256 = CollaborationMigrationService.Hash(System.Text.Encoding.UTF8.GetBytes(actor + request)), StructureRevision = manifest.StructureRevision });
    }
    public async Task AddNode(Guid id, Guid document, Guid project, Guid? parent, string title, int order, Guid? section, string? metadata, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO ProjectNodes(Id,DocumentId,ProjectId,ParentId,NodeType,Title,OrderIndex,LinkedSectionId,MetadataJson,WordCountCache,UpdatedUtc) VALUES({id},{document},{project},{parent},{(int)WriterApp.Data.Documents.ProjectNodeType.Scene},{title},{order},{section},{metadata},{0},{now})", ct);
    }
}
