using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WriterApp.Application.AI;
using WriterApp.Data;
using WriterApp.Shared;
using WriterApp.Shared.Collaboration;

namespace WriterApp.Application.Collaboration;

public sealed record CollaborationCheckedEdit(Guid OperationId, WebAiSource Source, string BeforeContent,
    string AfterContent, Guid? HistoryOperationId = null);
public sealed record CollaborationCheckedEditResult(Guid OperationId, Guid PageId, Guid Generation, string Html);

// Replacement is an explicit compare-and-swap, never a legacy HTML save. Retiring the
// generation fences leases and offline updates while retaining the original CRDT bytes.
public sealed class CollaborationCheckedEdits(AppDbContext db, ICollaborationCodec codec, CollaborationAccessService access)
{
    public async Task<CollaborationCheckedEditResult> Apply(string actor, Guid document, CollaborationCheckedEdit request, CancellationToken ct)
    {
        if (request.OperationId == Guid.Empty || request.Source.DocumentId != document || request.Source.PageId is not { } pageId
            || request.BeforeContent.Length > 1_000_000 || request.AfterContent.Length > 1_000_000)
            throw new CollaborationCommitException("collaboration_checked_edit_invalid");
        await access.Require(actor, document, CollaborationOperation.WriteText, ct);
        var hash = CollaborationMigrationService.Hash(JsonSerializer.SerializeToUtf8Bytes(new { actor, request }));
        var seed = await codec.Seed(request.AfterContent, "Html", ct);
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var tx = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
            await CollaborationMigrationService.Lock(db, document, ct);
            await access.Require(actor, document, CollaborationOperation.WriteText, ct);
            var prior = await db.CollaborationCommands.AsNoTracking().SingleOrDefaultAsync(c => c.Id == request.OperationId, ct);
            if (prior is not null)
            {
                if (prior.DocumentId != document || prior.RequestSha256 != hash) throw new CollaborationCommitException("collaboration_command_identity_reused");
                var saved = await db.CollaborationProjections.AsNoTracking().SingleAsync(p => p.PageId == pageId && p.Generation == request.OperationId, ct);
                if(tx is not null)await tx.CommitAsync(ct);
                return new CollaborationCheckedEditResult(request.OperationId, pageId, request.OperationId, seed.Html);
            }
            await new WebAiSourceService(db, actor, access).Require(request.Source, ct);
            var page = await db.CollaborationPages.SingleAsync(p => p.DocumentId == document && p.PageId == pageId && !p.IsDeleted, ct);
            var projection = await db.CollaborationProjections.SingleAsync(p => p.PageId == pageId && p.Generation == page.Generation, ct);
            if (projection.Sequence != page.CommittedSequence || projection.Html != request.BeforeContent)
                throw new CollaborationCommitException("collaboration_source_changed");
            WriterApp.Data.AI.WebAiHistoryOperationRecord? history = null;
            if (request.HistoryOperationId is { } operation)
            {
                if(seed.Html!=request.AfterContent)throw new CollaborationCommitException("collaboration_review_canonical_content");
                history = await db.WebAiHistoryOperations.SingleOrDefaultAsync(h => h.OwnerUserId == actor && h.OperationId == operation, ct)
                    ?? throw new CollaborationCommitException("collaboration_history_intent_missing");
                var intent = WebAiHistoryOperations.Intent(history);
                if (history.CommittedAt is not null || intent.Source != request.Source || intent.TargetKind != "Page"
                    || intent.BeforeContent != request.BeforeContent || intent.AfterContent != request.AfterContent || operation != request.OperationId)
                    throw new CollaborationCommitException("collaboration_history_intent_changed");
            }
            page.IsDeleted = true; page.LeaseEpoch++; page.LeaseOwner = null; page.LeaseExpiresAtUnixMilliseconds = 0;
            await db.CollaborationSessions.Where(s => s.PageId == pageId && s.Generation == page.Generation && !s.Revoked)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Revoked, true).SetProperty(x => x.Version, x => x.Version + 1), ct);
            await db.CollaborationOutboxEvents.Where(e => e.PageId == pageId && e.Generation == page.Generation && e.CompletedAtUnixMilliseconds == null)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.CompletedAtUnixMilliseconds, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()), ct);
            db.CollaborationPages.Add(new() { DocumentId = document, PageId = pageId, Generation = request.OperationId,
                ContentContractVersion = seed.Support.ContentContractVersion, ProtocolVersion = seed.Support.ProtocolVersion, SchemaFingerprint = seed.Support.SchemaFingerprint });
            db.CollaborationSnapshots.Add(new() { PageId = pageId, Generation = request.OperationId, State = seed.State, Sha256 = seed.Sha256, CreatedAtUnixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() });
            db.CollaborationProjections.Add(new() { PageId = pageId, Generation = request.OperationId, Html = seed.Html, Json = seed.Json.GetRawText(), Text = seed.Text, StateSha256 = seed.Sha256 });
            var manifest = await db.CollaborationManifests.SingleAsync(m => m.DocumentId == document, ct);
            db.CollaborationCommands.Add(new() { Id = request.OperationId, DocumentId = document, RequestSha256 = hash, StructureRevision = manifest.StructureRevision });
            var result = new CollaborationCheckedEditResult(request.OperationId, pageId, request.OperationId, seed.Html);
            if (history is not null) { history.CommittedAt = DateTimeOffset.UtcNow; history.SavedResponseJson = JsonSerializer.Serialize(result, WebAiHistoryContracts.Json); }
            await db.SaveChangesAsync(ct); if(tx is not null)await tx.CommitAsync(ct); return result;
        });
    }
}
