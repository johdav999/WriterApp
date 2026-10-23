using System.Data;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WriterApp.Data;
using WriterApp.Data.Collaboration;
using WriterApp.Shared.Collaboration;

namespace WriterApp.Application.Collaboration;

public sealed class CollaborationInteractionOptions { public bool Enabled { get; set; } }

public sealed class CollaborationCommentService(AppDbContext db, CollaborationAccessService access, ICollaborationCodec codec,
    IOptions<CollaborationInteractionOptions> options, TimeProvider clock)
{
    private static CollaborationCommitException Fail(string code) => new("collaboration_" + code);
    private void Enabled() { if (!options.Value.Enabled) throw new CollaborationSessionDeniedException(); }
    private static void Body(string? value) { if (string.IsNullOrWhiteSpace(value) || value.Length > 4000 || value.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t'))) throw Fail("comment_body_invalid"); }
    private async Task<CollaborationPageRecord> Page(Guid document, Guid page, Guid generation, CancellationToken ct, bool active = true)
    {
        if (!await db.Documents.AnyAsync(d => d.Id == document && d.CollaborationMode == CollaborationMode.Collaborative, ct)) throw new CollaborationSessionDeniedException();
        return await db.CollaborationPages.AsNoTracking().SingleOrDefaultAsync(p => p.DocumentId == document && p.PageId == page && p.Generation == generation && (!active || !p.IsDeleted), ct) ?? throw Fail("generation_changed");
    }
    private static CollaborationJournalSupport Support(CollaborationPageRecord page) => new(page.ContentContractVersion, page.ProtocolVersion, page.SchemaFingerprint);
    private async Task<byte[][]> History(CollaborationPageRecord page, CancellationToken ct)
    {
        var snapshot = await db.CollaborationSnapshots.AsNoTracking().SingleOrDefaultAsync(s => s.PageId == page.PageId && s.Generation == page.Generation && s.Sequence == page.SnapshotSequence, ct) ?? throw Fail("comment_source_invalid");
        var query = db.CollaborationUpdates.AsNoTracking().Where(u => u.PageId == page.PageId && u.Generation == page.Generation && u.Sequence > snapshot.Sequence);
        if (snapshot.State.Length + await query.SumAsync(u => (long)u.Update.Length, ct) > 16 * 1024 * 1024) throw Fail("comment_source_limit");
        var tail = await query.OrderBy(u => u.Sequence).Take(4097).ToArrayAsync(ct);
        if (tail.Length > 4096 || tail.Length != page.CommittedSequence - snapshot.Sequence
            || CollaborationMigrationService.Hash(snapshot.State) != snapshot.Sha256.ToLowerInvariant()
            || tail.Where((u, i) => u.Sequence != snapshot.Sequence + i + 1 || CollaborationMigrationService.Hash(u.Update) != u.Sha256.ToLowerInvariant()).Any()) throw Fail("comment_source_invalid");
        return [snapshot.State, .. tail.Select(u => u.Update)];
    }
    private Task<T> Transaction<T>(Guid document, Func<Task<T>> work, CancellationToken ct) => db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
    {
        Enabled(); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await CollaborationMigrationService.Lock(db, document, ct);
        var result = await work(); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return result;
    });
    private async Task<CollaborationAccess> Writer(string actor, Guid document, long revision, CancellationToken ct)
    {
        var grant = await access.Require(actor, document, CollaborationOperation.WriteText, ct);
        if (grant.MembershipRevision != revision) throw new CollaborationSessionDeniedException(); return grant;
    }
    private static string Hash(string actor, Guid document, Guid page, Guid thread, object request) =>
        CollaborationMigrationService.Hash(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { actor, document, page, thread, request })));
    private async Task<long?> Replay(Guid operation, string actor, Guid thread, string hash, CancellationToken ct)
    {
        if (operation == Guid.Empty) throw Fail("comment_operation_invalid");
        var prior = await db.CollaborationCommentOperations.AsNoTracking().SingleOrDefaultAsync(o => o.Id == operation, ct);
        if (prior is null) return null;
        if (prior.ActorId != actor || prior.ThreadId != thread || prior.RequestSha256 != hash) throw Fail("comment_operation_reused");
        return prior.ResultRevision;
    }
    private void Receipt(Guid operation, string actor, Guid thread, string hash, long revision) => db.CollaborationCommentOperations.Add(new()
        { Id = operation, ActorId = actor, ThreadId = thread, RequestSha256 = hash, ResultRevision = revision });

    public Task<long> Create(string actor, Guid document, Guid pageId, CollaborationCreateComment request, CancellationToken ct) => Transaction(document, async () =>
    {
        await Writer(actor, document, request.MembershipRevision, ct); var page = await Page(document, pageId, request.Generation, ct); Body(request.Body);
        string hash = Hash(actor, document, pageId, request.OperationId, request);
        if (await Replay(request.OperationId, actor, request.OperationId, hash, ct) is { } replay) return replay;
        if (request.ExpectedSequence != page.CommittedSequence) throw Fail("comment_sequence_changed");
        if (await db.CollaborationComments.CountAsync(t => t.PageId == pageId && t.Generation == page.Generation, ct) >= 200) throw Fail("comment_limit");
        if (request.Anchor is null || request.Anchor.Start is null || request.Anchor.End is null || request.Anchor.Start.Length > 1024 || request.Anchor.End.Length > 1024) throw Fail("anchor_invalid");
        var anchors = await codec.Anchors(await History(page, ct), [request.Anchor], ct, Support(page));
        if (anchors.Length != 1 || anchors[0].Detached) throw Fail("anchor_detached");
        db.CollaborationComments.Add(new() { Id = request.OperationId, DocumentId = document, PageId = pageId, Generation = page.Generation,
            AuthorId = actor, Start = request.Anchor.Start, End = request.Anchor.End, Body = request.Body, CreatedAtUnixMilliseconds = clock.GetUtcNow().ToUnixTimeMilliseconds() });
        Receipt(request.OperationId, actor, request.OperationId, hash, 1); return 1;
    }, ct);

    public Task<long> Change(string actor, Guid document, Guid pageId, Guid threadId, CollaborationCommentMutation request, CancellationToken ct) => Transaction(document, async () =>
    {
        var grant = await Writer(actor, document, request.MembershipRevision, ct); await Page(document, pageId, request.Generation, ct);
        var thread = await db.CollaborationComments.SingleOrDefaultAsync(t => t.Id == threadId && t.DocumentId == document && t.PageId == pageId && t.Generation == request.Generation, ct) ?? throw new CollaborationSessionDeniedException();
        string hash = Hash(actor, document, pageId, threadId, request);
        if (await Replay(request.OperationId, actor, threadId, hash, ct) is { } replay) return replay;
        if (thread.Revision != request.ExpectedRevision) throw Fail("comment_revision_changed");
        if (request.Action == "reply")
        {
            Body(request.Body); if (thread.Resolved) throw Fail("comment_resolved");
            if (await db.CollaborationCommentMessages.CountAsync(m => m.ThreadId == threadId, ct) >= 100) throw Fail("comment_reply_limit");
            var threadIds = db.CollaborationComments.Where(t => t.PageId == pageId && t.Generation == request.Generation).Select(t => t.Id);
            if (await db.CollaborationCommentMessages.CountAsync(m => threadIds.Contains(m.ThreadId), ct) >= 1000) throw Fail("comment_reply_limit");
            db.CollaborationCommentMessages.Add(new() { Id = request.OperationId, ThreadId = threadId, AuthorId = actor, Body = request.Body!, CreatedAtUnixMilliseconds = clock.GetUtcNow().ToUnixTimeMilliseconds() });
        }
        else if (request.Action is "resolve" or "reopen")
        {
            if (request.Body is not null || grant.Role != CollaborationRole.Owner && thread.AuthorId != actor) throw new CollaborationSessionDeniedException();
            thread.Resolved = request.Action == "resolve";
        }
        else throw Fail("comment_action_invalid");
        thread.Revision++; Receipt(request.OperationId, actor, threadId, hash, thread.Revision); return thread.Revision;
    }, ct);

    public Task<CollaborationCommentSnapshot> Read(string actor, Guid document, Guid pageId, Guid generation, CancellationToken ct) => Transaction(document, async () =>
    {
        await access.Require(actor, document, CollaborationOperation.Read, ct); var page = await Page(document, pageId, generation, ct, active: false);
        var threads = await db.CollaborationComments.AsNoTracking().Where(t => t.DocumentId == document && t.PageId == pageId && t.Generation == generation).OrderBy(t => t.CreatedAtUnixMilliseconds).ThenBy(t => t.Id).Take(201).ToArrayAsync(ct);
        if (threads.Length > 200) throw Fail("comment_limit");
        var attached = threads.Where(t => !page.IsDeleted && t.Start is not null && t.End is not null).ToArray();
        var states = attached.Length == 0 ? [] : await codec.Anchors(await History(page, ct), attached.Select(t => new CollaborationAnchor(t.Start!, t.End!)).ToArray(), ct, Support(page));
        if (states.Length != attached.Length) throw Fail("anchor_invalid");
        var detached = attached.Select((t, i) => (t.Id, states[i].Detached)).ToDictionary(t => t.Id, t => t.Detached);
        var ids = threads.Select(t => t.Id).ToArray();
        var replies = await db.CollaborationCommentMessages.AsNoTracking().Where(m => ids.Contains(m.ThreadId)).OrderBy(m => m.CreatedAtUnixMilliseconds).ThenBy(m => m.Id).Take(1001).ToArrayAsync(ct);
        if (replies.Length > 1000) throw Fail("comment_reply_limit");
        return new CollaborationCommentSnapshot(pageId, generation, page.CommittedSequence, threads.Select(t => new CollaborationCommentThread(t.Id, pageId, generation, t.AuthorId, t.Revision,
            t.Resolved, page.IsDeleted || t.Start is null || detached[t.Id], t.Start is null ? null : new(t.Start, t.End!),
            new[] { new CollaborationCommentMessage(t.Id, t.AuthorId, t.Body, t.CreatedAtUnixMilliseconds) }.Concat(replies.Where(m => m.ThreadId == t.Id).Select(m => new CollaborationCommentMessage(m.Id, m.AuthorId, m.Body, m.CreatedAtUnixMilliseconds))).ToArray())).ToArray());
    }, ct);

    // Integer offsets refer to retained legacy HTML/scene text, not the new Yjs item identities.
    // Import explicitly as detached; never guess a passage or rewrite the original annotations.
    public Task<int> ImportLegacy(string actor, Guid document, Guid pageId, Guid generation, CancellationToken ct) => Transaction(document, async () =>
    {
        var grant = await access.Require(actor, document, CollaborationOperation.Manage, ct); await Page(document, pageId, generation, ct);
        var page = await db.Pages.AsNoTracking().SingleAsync(p => p.Id == pageId && p.DocumentId == document, ct);
        var annotations = await db.PageAnnotations.AsNoTracking().Where(a => a.DocumentId == document && a.PageId == pageId).Take(201).ToArrayAsync(ct);
        var sources = annotations.Select(a => (Source: "page:" + a.Id, a.AuthorUserId, Content: string.IsNullOrWhiteSpace(a.Content) ? "Legacy " + a.Kind + ": " + a.AnchorText : a.Content, a.Status, a.CreatedAt)).ToList();
        var firstPage = await db.Pages.Where(p => p.DocumentId == document && p.SectionId == page.SectionId && db.CollaborationPages.Any(c => c.PageId == p.Id && !c.IsDeleted)).OrderBy(p => p.OrderIndex).ThenBy(p => p.Id).Select(p => p.Id).FirstAsync(ct);
        if (pageId == firstPage)
        {
            var projectId = await db.Documents.Where(d => d.Id == document).Select(d => d.ProjectId).SingleAsync(ct);
            var sceneIds = db.ProjectNodes.Where(n => (n.DocumentId == document || n.DocumentId == null) && n.ProjectId == projectId && n.LinkedSectionId == page.SectionId).Select(n => n.Id);
            var scenes = await db.SceneAnnotations.AsNoTracking().Where(a => sceneIds.Contains(a.SceneNodeId)).Take(201).ToArrayAsync(ct);
            sources.AddRange(scenes.Select(a => ("scene:" + a.Id, a.AuthorUserId, string.IsNullOrWhiteSpace(a.Content) ? "Legacy " + a.Kind + ": " + a.AnchorText : a.Content, a.Status, a.CreatedAt)));
        }
        if (sources.Count > 200) throw Fail("comment_limit");
        var existing = await db.CollaborationComments.Where(t => t.PageId == pageId && t.Generation == generation).Select(t => t.LegacySource).ToArrayAsync(ct);
        int imported = 0;
        foreach (var source in sources.Where(s => !existing.Contains(s.Source)))
        {
            if (existing.Length + imported >= 200) throw Fail("comment_limit"); Body(source.Content);
            if (source.AuthorUserId.Length > 128) throw Fail("comment_author_invalid");
            db.CollaborationComments.Add(new() { Id = Guid.NewGuid(), DocumentId = document, PageId = pageId, Generation = generation,
                AuthorId = source.AuthorUserId, Body = source.Content, Resolved = source.Status == "resolved", LegacySource = source.Source, CreatedAtUnixMilliseconds = source.CreatedAt.ToUnixTimeMilliseconds() }); imported++;
        }
        return imported;
    }, ct);
}
