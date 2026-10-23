using System.Data;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WriterApp.Data;
using WriterApp.Data.AI;
using WriterApp.Shared.Brainstorming;
using WriterApp.Application.Collaboration;
using WriterApp.Shared.Collaboration;

namespace WriterApp.Application.AI;

public sealed class BrainstormingAccessException : Exception;
/// <summary>Conversation-only storage. Never dispatches a provider or writes manuscript/planning/history.</summary>
public sealed class BrainstormingSessions(AppDbContext db, string owner, string backendKey, CollaborationAccessService? access = null)
{
    private string AccountKey => BrainstormingValidation.Fingerprint(owner);
    private IQueryable<BrainstormingSessionRecord> Owned(Guid document) => db.BrainstormingSessions
        .Where(s => s.OwnerUserId == owner && s.BackendKey == backendKey && s.DocumentId == document);
    private async Task RequireDocument(Guid id, Guid? project, CancellationToken ct)
    {
        var doc = await db.Documents.AsNoTracking().SingleOrDefaultAsync(d => d.Id == id && (d.OwnerUserId == owner || access != null)
            && d.DeletedAtUtc == null && !d.IsArchived, ct);
        if (doc?.CollaborationMode == CollaborationMode.Collaborative && access is not null) {
            try { await access.Require(owner, id, CollaborationOperation.PrepareAi, ct); }
            catch (CollaborationSessionDeniedException) { throw new BrainstormingAccessException(); }
        } else if (doc?.OwnerUserId != owner) throw new BrainstormingAccessException();
        if (doc is null || project is not null && doc.ProjectId != project || await db.DeletedUserIdentities.AnyAsync(u => u.UserId == owner, ct)
            || !await db.Projects.AnyAsync(p => p.Id == doc.ProjectId && p.OwnerUserId == doc.OwnerUserId, ct)
            // Older shared sessions were manuscript-only until the first chapter.
            // Adding outline structure must not revoke access to their private history.
            || project is null && doc.CollaborationMode != CollaborationMode.Collaborative && (await db.Projects.AnyAsync(p => p.Id == doc.ProjectId && p.SyncEnabled, ct)
                || await db.ProjectNodes.AnyAsync(n => n.DocumentId == id && n.SyncDeletionId == null, ct)))
            throw new BrainstormingAccessException();
    }
    private async Task RequireScope(BrainstormingScope scope, CancellationToken ct)
    {
        BrainstormingValidation.Validate(scope);
        if (scope.Space != BrainstormingIdentitySpace.Server || scope.AccountKey != AccountKey || scope.BackendKey != backendKey)
            throw new BrainstormingAccessException();
        await RequireDocument(scope.DocumentId, scope.ProjectId, ct);
    }
    private async Task RequireSourceDocuments(BrainstormingSession session, CancellationToken ct)
    {
        foreach (var id in session.Contexts.SelectMany(c => c.Sources).Select(s => s.DocumentId)
            .Concat((session.EnabledSources ?? []).Select(s => s.DocumentId)).Distinct())
            if (id == session.Scope.DocumentId) await RequireDocument(id, session.Scope.ProjectId, ct);
            else if (!await db.Documents.AnyAsync(d => d.Id == id && d.OwnerUserId == owner && d.DeletedAtUtc == null && !d.IsArchived
                && db.Projects.Any(p => p.Id == d.ProjectId && p.OwnerUserId == owner), ct))
                throw new BrainstormingAccessException();
    }
    private static BrainstormingSession Decode(BrainstormingSessionRecord row) => BrainstormingValidation.ReadSession(Encoding.UTF8.GetBytes(row.SnapshotJson));
    public async Task<BrainstormingSession?> ReadAsync(Guid document, Guid session, CancellationToken ct)
    {
        var row = await Owned(document).AsNoTracking().SingleOrDefaultAsync(s => s.Id == session, ct);
        if (row is null) return null;
        var result = Decode(row); await RequireScope(result.Scope, ct); await RequireSourceDocuments(result, ct); return result;
    }
    public async Task<BrainstormingReceipt?> InspectAsync(Guid document, Guid session, Guid operation, CancellationToken ct)
    {
        var source = await ReadAsync(document, session, ct); if (source is null) return null;
        var row = await db.BrainstormingSessionOperations.AsNoTracking().SingleOrDefaultAsync(o => o.SessionId == session && o.OperationId == operation, ct);
        return row is null ? null : JsonSerializer.Deserialize<BrainstormingReceipt>(row.ReceiptJson, BrainstormingValidation.Json);
    }
    public async Task<BrainstormingSessionPage> ListAsync(Guid document, Guid? after, int limit, CancellationToken ct)
    {
        if (limit is < 1 or > 50 || after == Guid.Empty) throw new InvalidDataException("Use a session page size of 1–50.");
        var doc = await db.Documents.AsNoTracking().SingleOrDefaultAsync(d => d.Id == document && (d.OwnerUserId == owner || access != null), ct)
            ?? throw new BrainstormingAccessException();
        await RequireDocument(document, doc.ProjectId, ct);
        var query = Owned(document).AsNoTracking();
        // Guid comparison translates for both supported providers; cursor is constrained to this owned document.
        if (after is { } cursor) {
            if (!await query.AnyAsync(s => s.Id == cursor, ct)) throw new BrainstormingAccessException();
            query = query.Where(s => s.Id.CompareTo(cursor) > 0);
        }
        var rows = await query.OrderBy(s => s.Id).Take(limit + 1).Select(s => new { s.Id, s.ProjectId, s.Revision, s.IsDeleted, s.UpdatedUnixMilliseconds }).ToListAsync(ct);
        // Do not read private snapshot content just to list summaries.
        var scopes = rows.Take(limit).Select(r => new BrainstormingSummary(new(1, r.Id, document, r.ProjectId,
            BrainstormingIdentitySpace.Server, backendKey, AccountKey, null), r.Revision,
            r.IsDeleted ? BrainstormingConversationState.Deleted : BrainstormingConversationState.Saved,
            DateTimeOffset.FromUnixTimeMilliseconds(r.UpdatedUnixMilliseconds))).ToArray();
        return new(1, scopes, rows.Count > limit ? scopes[^1].Scope.SessionId : null);
    }
    private async Task RequireNewReferences(BrainstormingSession next, BrainstormingSession? previous, CancellationToken ct)
    {
        foreach (var t in next.Targets.Where(t => previous?.Targets.Any(old => BrainstormingPersistence.Json(old) == BrainstormingPersistence.Json(t)) != true))
        {
            if (t.SectionId is { } section && !await db.Sections.AnyAsync(s => s.Id == section && s.DocumentId == next.Scope.DocumentId, ct)
                || t.BeforeSectionId is { } before && !await db.Sections.AnyAsync(s => s.Id == before && s.DocumentId == next.Scope.DocumentId, ct)
                || t.PageId is { } page && !await db.Pages.AnyAsync(p => p.Id == page && p.SectionId == t.SectionId && p.DocumentId == next.Scope.DocumentId, ct))
                throw new BrainstormingAccessException();
            foreach (var node in new[] { t.ChapterNodeId, t.SceneNodeId, t.ParentNodeId, t.BeforeNodeId }.OfType<Guid>())
                if (!await db.ProjectNodes.AnyAsync(n => n.Id == node && n.DocumentId == next.Scope.DocumentId && n.ProjectId == next.Scope.ProjectId && n.SyncDeletionId == null, ct))
                    throw new BrainstormingAccessException();
        }
        foreach (var c in next.Contexts.Where(c => previous?.Contexts.Any(old => BrainstormingPersistence.Json(old) == BrainstormingPersistence.Json(c)) != true))
            foreach (var source in c.Sources) {
                if (source.SectionId is { } section && !await db.Sections.AnyAsync(s => s.Id == section && s.DocumentId == source.DocumentId, ct)
                    || source.PageId is { } page && !await db.Pages.AnyAsync(p => p.Id == page && p.DocumentId == source.DocumentId && p.SectionId == source.SectionId, ct))
                    throw new BrainstormingAccessException();
                foreach (var part in source.Parts ?? []) if (!await db.Pages.AnyAsync(p => p.Id == part.PageId && p.DocumentId == source.DocumentId && p.SectionId == part.SectionId, ct)) throw new BrainstormingAccessException();
            }
    }
    private static void RetainHistory(BrainstormingSession old, BrainstormingSession next)
    {
        void Same<T>(IReadOnlyList<T> before, IReadOnlyList<T> after, Func<T, Guid> key) {
            foreach (var item in before) if (!after.Any(n => key(n) == key(item) && BrainstormingPersistence.Json(n) == BrainstormingPersistence.Json(item)))
                throw new InvalidDataException("Historical context, targets and draft ancestry are immutable. Create a new version.");
        }
        Same(old.Contexts, next.Contexts, x => x.Id); Same(old.Targets, next.Targets, x => x.Id); Same(old.Drafts, next.Drafts, x => x.Id);
        Same(old.Planning, next.Planning, x => x.Id); Same(old.Approvals, next.Approvals, x => x.Id);
        foreach (var turn in old.Turns) {
            var updated = next.Turns.SingleOrDefault(t => t.Id == turn.Id);
            if (updated is null || updated.Sequence != turn.Sequence || updated.ParentTurnId != turn.ParentTurnId || updated.OperationId != turn.OperationId
                || turn.State is BrainstormingTurnState.Complete && updated != turn
                || turn.Role == BrainstormingRole.User && turn.State != BrainstormingTurnState.Unsent && updated.Text != turn.Text)
                throw new InvalidDataException("Accepted message order/content cannot be rewritten.");
        }
        foreach (var request in old.Requests) if (!next.Requests.Any(r => r.Id == request.Id && r.OperationId == request.OperationId
            && r.UserTurnId == request.UserTurnId && r.ContextId == request.ContextId && r.TargetId == request.TargetId))
            throw new InvalidDataException("Accepted request provenance cannot be rewritten.");
        foreach (var application in old.Applications.Where(a => a.State is BrainstormingApplicationState.Committed or BrainstormingApplicationState.Undone))
            if (!next.Applications.Any(a => a == application)) throw new InvalidDataException("Committed application receipts are immutable in conversation storage.");
        if (next.CreatedAt != old.CreatedAt || next.BranchedFromSessionId != old.BranchedFromSessionId || next.UpdatedAt < old.UpdatedAt)
            throw new InvalidDataException("Session lineage/timestamps changed.");
    }
    public Task<BrainstormingReceipt> WriteAsync(BrainstormingWrite write, CancellationToken ct) =>
        db.Database.CreateExecutionStrategy().ExecuteAsync(async () => {
            // A retry starts from durable rows, including the exact operation receipt
            // after an uncertain commit; never reuse tracked entities from a failed attempt.
            db.ChangeTracker.Clear();
            return await WriteCoreAsync(write, ct);
        });
    private async Task<BrainstormingReceipt> WriteCoreAsync(BrainstormingWrite write, CancellationToken ct)
    {
        BrainstormingPersistence.Validate(write); await RequireScope(write.Session.Scope, ct);
        if (write.Session.Turns.Any(t => t.Role == BrainstormingRole.User && t.State == BrainstormingTurnState.Unsent))
            throw new InvalidDataException("Server writes must explicitly accept the persisted user turns; draft composer text stays local.");
        var scope = write.Session.Scope; var hash = BrainstormingPersistence.Hash(write);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var row = await Owned(scope.DocumentId).SingleOrDefaultAsync(s => s.Id == scope.SessionId, ct);
        if (row is null && await db.BrainstormingSessions.AnyAsync(s => s.Id == scope.SessionId, ct)) throw new BrainstormingAccessException();
        await RequireSourceDocuments(write.Session, ct);
        var receipt = await db.BrainstormingSessionOperations.AsNoTracking().SingleOrDefaultAsync(o => o.SessionId == scope.SessionId && o.OperationId == write.OperationId, ct);
        if (receipt is not null) {
            if (receipt.PayloadHash != hash) throw new InvalidDataException("Session operation payload changed.");
            return JsonSerializer.Deserialize<BrainstormingReceipt>(receipt.ReceiptJson, BrainstormingValidation.Json)!;
        }
        if ((row?.Revision ?? 0) != write.ExpectedRevision) throw new BrainstormingConcurrencyException(row is null ? null : Decode(row));
        var previous = row is null ? null : Decode(row);
        if(previous is not null) {
            var generated = await db.BrainstormingGenerations.AsNoTracking().Where(g=>g.SessionId==scope.SessionId).Select(g=>g.OperationId).ToArrayAsync(ct);
            foreach(var request in previous.Requests.Where(r=>generated.Contains(r.OperationId))) {
                if(!write.Session.Requests.Any(r=>r==request)
                    || previous.Turns.Where(t=>t.RequestId==request.Id).Any(t=>!write.Session.Turns.Any(n=>n==t))
                    || previous.Drafts.Where(d=>d.RequestId==request.Id).Any(d=>!write.Session.Drafts.Any(n=>BrainstormingPersistence.Json(n)==BrainstormingPersistence.Json(d))))
                    throw new InvalidDataException("Server generation provenance and terminal outcomes cannot be changed through conversation CRUD.");
            }
            if(previous.Requests.Any(r=>generated.Contains(r.OperationId) && r.State is BrainstormingGenerationState.Accepted or BrainstormingGenerationState.Streaming or BrainstormingGenerationState.Unknown))
                throw new InvalidDataException("Inspect or stop the active generation before changing this session.");
        }
        if (row?.IsDeleted == true) throw new BrainstormingConcurrencyException(previous); // permanent logical tombstone; branch explicitly
        if (previous is not null) { BrainstormingValidation.RequireScope(previous.Scope, scope); RetainHistory(previous, write.Session); }
        else if (write.Session.BranchedFromSessionId is { } parent && !await Owned(scope.DocumentId).AnyAsync(s => s.Id == parent, ct))
            throw new BrainstormingAccessException();
        await RequireNewReferences(write.Session, previous, ct);
        foreach(var draft in write.Session.Drafts.Where(d=>d.Origin==BrainstormingDraftOrigin.AuthorDictated && previous?.Drafts.Any(p=>p.Id==d.Id)!=true)) {
            var outcomes=await db.BrainstormingTranscriptions.AsNoTracking().Where(t=>t.OperationId==draft.DictationOperationId && db.BrainstormingSessions.Any(s=>s.Id==t.SessionId && s.DocumentId==scope.DocumentId && s.OwnerUserId==owner && s.BackendKey==backendKey)).Select(t=>t.ResultJson).ToArrayAsync(ct);
            if(!outcomes.Any(json=>System.Text.Json.JsonSerializer.Deserialize<BrainstormingSpeechResult>(json,BrainstormingValidation.Json)?.State==BrainstormingSpeechState.Completed))throw new InvalidDataException("Dictated prose requires a completed owned transcription. No AI or application receipt is manufactured.");
        }
        // Prompt 7 must add the authoritative mutation-to-session receipt bridge. Conversation CRUD cannot manufacture it.
        foreach (var app in write.Session.Applications.Where(a => a.State is BrainstormingApplicationState.Committed or BrainstormingApplicationState.Undone))
            if (previous?.Applications.Any(a => a == app) != true)
                throw new InvalidDataException("Application confirmation requires the authoritative brainstorming mutation bridge; conversation persistence alone cannot confirm application.");
        var saved = new BrainstormingReceipt(1, write.OperationId, scope, write.Session.Revision, hash, DateTimeOffset.UtcNow);
        if (row is null) { row = new() { Id = scope.SessionId, OwnerUserId = owner, BackendKey = backendKey, DocumentId = scope.DocumentId, ProjectId = scope.ProjectId }; db.BrainstormingSessions.Add(row); }
        row.Revision = saved.Revision; row.IsDeleted = write.Session.State == BrainstormingConversationState.Deleted;
        row.UpdatedUnixMilliseconds = write.Session.UpdatedAt.ToUnixTimeMilliseconds(); row.SnapshotJson = BrainstormingPersistence.Json(write.Session);
        db.BrainstormingSessionOperations.Add(new() { SessionId = row.Id, OperationId = saved.OperationId, PayloadHash = hash, ReceiptJson = BrainstormingPersistence.Json(saved) });
        await RequireScope(scope, ct); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return saved;
    }
}
