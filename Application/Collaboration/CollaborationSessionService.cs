using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WriterApp.Application.Security;
using WriterApp.Application.Subscriptions;
using WriterApp.Data;
using WriterApp.Data.Collaboration;
using WriterApp.Shared.Collaboration;

namespace WriterApp.Application.Collaboration;

public sealed class CollaborationSessionService(AppDbContext db, IDataProtectionProvider protection,
    IOptionsMonitor<CollaborationSessionOptions> options, IUserEntitlementStore entitlements,
    IDeletedUserIdentityService deletedUsers, TimeProvider clock, CollaborationAccessService? access = null)
{
    private sealed record Ticket(Guid SessionId, long Version, string Environment, Guid DocumentId, Guid PageId, Guid Generation,
        Guid InstallationId, string Owner, string Schema, int Protocol, int Contract, long Expires, bool CommitAllowed, string ResourceOwner, long MembershipRevision);
    private IDataProtector Protector => protection.CreateProtector("Prosa.Collaboration.Session.v3", options.CurrentValue.Environment);
    private long Now => clock.GetUtcNow().ToUnixTimeMilliseconds();
    public async Task<string> PresenceDisplayName(CollaborationSessionRecord session, CancellationToken ct)
    {
        var name = (await db.UserProfiles.AsNoTracking().Where(p => p.UserId == session.OwnerUserId)
            .Select(p => p.DisplayName).SingleOrDefaultAsync(ct))?.Trim();
        return string.IsNullOrWhiteSpace(name) || string.Equals(name, session.OwnerUserId, StringComparison.Ordinal)
            ? "Collaborator" : name[..Math.Min(name.Length, 256)];
    }
    public static string Room(CollaborationSessionRecord s) => $"{s.Environment}/{s.DocumentId:D}/{s.PageId:D}/{s.Generation:D}";
    private void Configured() { if (!options.CurrentValue.IsConfigured) throw new CollaborationSessionDeniedException(); }

    private async Task RequireAccess(CollaborationSessionRecord s, CancellationToken ct)
    {
        Configured();
        var recovery = await db.CollaborationRecoveryStates.AsNoTracking().SingleOrDefaultAsync(ct);
        if (recovery is not null && s.Environment != recovery.RequiredEnvironment) throw new CollaborationSessionDeniedException();
        if (s.Environment != options.CurrentValue.Environment || s.Revoked || s.ExpiresAtUnixMilliseconds <= Now)
            throw new CollaborationSessionDeniedException();
        if (access is not null)
        {
            var grant = await access.Require(s.OwnerUserId, s.DocumentId, s.CommitAllowed ? CollaborationOperation.WriteText : CollaborationOperation.Read, ct);
            if (grant.ResourceOwnerId != s.ResourceOwnerUserId || grant.MembershipRevision != s.MembershipRevision) throw new CollaborationSessionDeniedException();
            // Require already checks both identities and paid access for writers/guests.
            // Owner reads retain the additional paid collaboration requirement.
            if (!s.CommitAllowed && grant.Role == CollaborationRole.Owner
                && !EntitlementAccessEvaluator.Evaluate(await entitlements.GetOrCreateAsync(s.ResourceOwnerUserId, ct)).IsPaidAccessActive)
                throw new CollaborationSessionDeniedException();
        }
        else if (s.ResourceOwnerUserId != s.OwnerUserId || s.MembershipRevision != 0
            || await deletedUsers.IsDeletedAsync(s.OwnerUserId, ct)
            || await deletedUsers.IsDeletedAsync(s.ResourceOwnerUserId, ct)
            || !EntitlementAccessEvaluator.Evaluate(await entitlements.GetOrCreateAsync(s.ResourceOwnerUserId, ct)).IsPaidAccessActive)
            throw new CollaborationSessionDeniedException();
        // One database snapshot of topology/lifecycle instead of three round trips.
        var pages = await db.CollaborationPages.AsNoTracking().Where(p => p.PageId == s.PageId && !p.IsDeleted
            && db.Documents.Any(d => d.Id == s.DocumentId && d.OwnerUserId == s.ResourceOwnerUserId
                && d.DeletedAtUtc == null && !d.IsArchived && d.CollaborationMode == CollaborationMode.Collaborative)
            && db.Pages.Any(legacy => legacy.Id == s.PageId && legacy.DocumentId == s.DocumentId)).ToArrayAsync(ct);
        var page = pages.Length == 1 ? pages[0] : null;
        if (page is null || page.DocumentId != s.DocumentId || page.Generation != s.Generation
            || page.SchemaFingerprint != s.SchemaFingerprint || page.ProtocolVersion != s.ProtocolVersion || page.ContentContractVersion != s.ContentContractVersion
            || s.ProtocolVersion != 1 || s.ContentContractVersion != 2)
            throw new CollaborationSessionDeniedException();
    }

    private CollaborationSessionTicket Encode(CollaborationSessionRecord s)
    {
        var payload = new Ticket(s.Id, s.Version, s.Environment, s.DocumentId, s.PageId, s.Generation, s.InstallationId,
            s.OwnerUserId, s.SchemaFingerprint, s.ProtocolVersion, s.ContentContractVersion, s.ExpiresAtUnixMilliseconds, s.CommitAllowed, s.ResourceOwnerUserId, s.MembershipRevision);
        return new(s.Id, s.Version, Protector.Protect(JsonSerializer.Serialize(payload)), Room(s), options.CurrentValue.WebSocketUrl, s.ExpiresAtUnixMilliseconds, !s.CommitAllowed);
    }

    public async Task<CollaborationSessionTicket> Issue(string owner, Guid documentId, Guid pageId, CollaborationSessionRequest request, CancellationToken ct)
    {
        Configured();
        var recovery = await db.CollaborationRecoveryStates.AsNoTracking().SingleOrDefaultAsync(ct);
        if (request.Environment is not null && request.Environment != options.CurrentValue.Environment
            || recovery is not null && (request.Environment != recovery.RequiredEnvironment || options.CurrentValue.Environment != recovery.RequiredEnvironment)) throw new CollaborationSessionDeniedException();
        if (request.RequestCommit && !options.CurrentValue.CommitsEnabled) throw new CollaborationSessionDeniedException();
        if (request.Generation == Guid.Empty || request.InstallationId == Guid.Empty || request.SchemaFingerprint is null
            || request.SchemaFingerprint.Length != 64 || !request.SchemaFingerprint.All(Uri.IsHexDigit)) throw new CollaborationSessionDeniedException();
        var s = new CollaborationSessionRecord { Id = Guid.NewGuid(), DocumentId = documentId, PageId = pageId, Generation = request.Generation,
            InstallationId = request.InstallationId, OwnerUserId = owner, Environment = options.CurrentValue.Environment,
            ResourceOwnerUserId = owner,
            SchemaFingerprint = request.SchemaFingerprint, ProtocolVersion = request.ProtocolVersion, ContentContractVersion = request.ContentContractVersion,
            Version = 1, CommitAllowed = request.RequestCommit, ExpiresAtUnixMilliseconds = Now + CollaborationSessionOptions.LifetimeSeconds * 1000 };
        if (access is not null)
        {
            var grant = await access.Require(owner, documentId, request.RequestCommit ? CollaborationOperation.WriteText : CollaborationOperation.Read, ct);
            // A fresh invitation must never adopt an outbox created under an older grant.
            if (request.MembershipRevision != grant.MembershipRevision) throw new CollaborationSessionDeniedException();
            s.ResourceOwnerUserId = grant.ResourceOwnerId; s.MembershipRevision = grant.MembershipRevision;
        }
        else if (request.MembershipRevision != 0) throw new CollaborationSessionDeniedException();
        await RequireAccess(s, ct);
        // Refuse missing/corrupt history at issuance as well as on socket bootstrap.
        await db.Database.CreateExecutionStrategy().ExecuteAsync(() => ReadBootstrap(s, ct, issuing: true));
        db.CollaborationSessions.Add(s); await db.SaveChangesAsync(ct); return Encode(s);
    }

    public async Task<CollaborationSessionTicket> Refresh(string owner, Guid documentId, Guid pageId, Guid id, long version, CancellationToken ct)
    {
        var s = await Owned(owner, documentId, pageId, id, ct);
        if (s.Version != version || s.Revoked) throw new CollaborationSessionDeniedException();
        // An expired ticket can only be renewed using the authenticated owner endpoint.
        s.ExpiresAtUnixMilliseconds = Now + CollaborationSessionOptions.LifetimeSeconds * 1000;
        await RequireAccess(s, ct);
        // Force clearing the connection even if it was acquired after this request read the row.
        int changed = await db.CollaborationSessions.Where(x => x.Id == s.Id && x.Version == version && !x.Revoked)
            .ExecuteUpdateAsync(x => x.SetProperty(v => v.Version, version + 1).SetProperty(v => v.ConnectionId, (Guid?)null)
                .SetProperty(v => v.ExpiresAtUnixMilliseconds, s.ExpiresAtUnixMilliseconds), ct);
        if (changed != 1) throw new CollaborationSessionDeniedException();
        s.Version++; s.ConnectionId = null;
        db.Entry(s).State = EntityState.Detached;
        return Encode(s);
    }

    public async Task Revoke(string owner, Guid documentId, Guid pageId, Guid id, CancellationToken ct)
    {
        var s = await Owned(owner, documentId, pageId, id, ct);
        if (s.Revoked) return;
        s.Revoked = true; s.Version++; await Save(ct);
    }

    private async Task<CollaborationSessionRecord> Owned(string owner, Guid documentId, Guid pageId, Guid id, CancellationToken ct) =>
        await db.CollaborationSessions.SingleOrDefaultAsync(s => s.Id == id && s.OwnerUserId == owner && s.DocumentId == documentId && s.PageId == pageId && s.Environment == options.CurrentValue.Environment, ct)
        ?? throw new CollaborationSessionDeniedException();
    private async Task Save(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw new CollaborationSessionDeniedException(); }
    }

    private async Task<CollaborationSessionRecord> ReadTicketSession(CollaborationServiceRequest request, CancellationToken ct)
    {
        Configured();
        if (request.ConnectionId == Guid.Empty || request.Ticket is null || request.Ticket.Length > 8192) throw new CollaborationSessionDeniedException();
        Ticket payload;
        try { payload = JsonSerializer.Deserialize<Ticket>(Protector.Unprotect(request.Ticket)) ?? throw new CryptographicException(); }
        catch (Exception e) when (e is CryptographicException or JsonException or ArgumentException or FormatException) { throw new CollaborationSessionDeniedException(); }
        var s = await db.CollaborationSessions.AsNoTracking().SingleOrDefaultAsync(s => s.Id == payload.SessionId, ct) ?? throw new CollaborationSessionDeniedException();
        if (payload != new Ticket(s.Id, s.Version, s.Environment, s.DocumentId, s.PageId, s.Generation, s.InstallationId, s.OwnerUserId,
                s.SchemaFingerprint, s.ProtocolVersion, s.ContentContractVersion, s.ExpiresAtUnixMilliseconds, s.CommitAllowed, s.ResourceOwnerUserId, s.MembershipRevision) || request.RoomName != Room(s))
            throw new CollaborationSessionDeniedException();
        return s;
    }

    // Resolve only the protected identities needed for lock ordering. This is NOT
    // authorization: commit policy is re-read after taking the database barriers.
    internal async Task<CollaborationSessionRecord> ResolveCommitScope(CollaborationServiceRequest request, CancellationToken ct)
    {
        var s = await ReadTicketSession(request, ct);
        if (request.Acquire || s.ConnectionId != request.ConnectionId) throw new CollaborationSessionDeniedException();
        return s;
    }

    public async Task<CollaborationSessionRecord> Authorize(CollaborationServiceRequest request, CancellationToken ct)
    {
        var s = await ReadTicketSession(request, ct);
        await RequireAccess(s, ct);
        if (s.ConnectionId is null && request.Acquire)
        {
            int changed = await db.CollaborationSessions.Where(x => x.Id == s.Id && x.Version == s.Version && !x.Revoked && x.ConnectionId == null)
                .ExecuteUpdateAsync(x => x.SetProperty(v => v.ConnectionId, request.ConnectionId), ct);
            if (changed != 1) throw new CollaborationSessionDeniedException();
            s.ConnectionId = request.ConnectionId;
        }
        if (s.ConnectionId != request.ConnectionId) throw new CollaborationSessionDeniedException();
        return s;
    }

    public Task<CollaborationBootstrap> Bootstrap(CollaborationSessionRecord s, CancellationToken ct) =>
        db.Database.CreateExecutionStrategy().ExecuteAsync(() => ReadBootstrap(s, ct));

    private async Task<CollaborationBootstrap> ReadBootstrap(CollaborationSessionRecord s, CancellationToken ct, bool issuing = false)
    {
        // A consistent read prevents mixing a moving snapshot pointer with a different tail.
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        await RequireAccess(s, ct);
        if (!issuing && !await db.CollaborationSessions.AsNoTracking().AnyAsync(x => x.Id == s.Id && x.Version == s.Version && !x.Revoked, ct)) throw new CollaborationSessionDeniedException();
        var page = await db.CollaborationPages.AsNoTracking().SingleAsync(p => p.PageId == s.PageId && p.Generation == s.Generation, ct);
        var snapshot = await db.CollaborationSnapshots.AsNoTracking().SingleOrDefaultAsync(x => x.PageId == s.PageId && x.Generation == s.Generation && x.Sequence == page.SnapshotSequence, ct);
        var tail = await db.CollaborationUpdates.AsNoTracking().Where(x => x.PageId == s.PageId && x.Generation == s.Generation && x.Sequence > page.SnapshotSequence && x.Sequence <= page.CommittedSequence).OrderBy(x => x.Sequence).Take(4097).ToArrayAsync(ct);
        if (snapshot is null || page.CommittedSequence - page.SnapshotSequence != tail.Length || tail.Length > 4096) throw new CollaborationSessionDeniedException();
        var bytes = new List<byte[]> { snapshot.State };
        if (!Valid(snapshot.State, snapshot.Sha256)) throw new CollaborationSessionDeniedException();
        long sequence = page.SnapshotSequence;
        foreach (var update in tail)
        {
            if (update.Sequence != ++sequence || !Valid(update.Update, update.Sha256)) throw new CollaborationSessionDeniedException();
            bytes.Add(update.Update);
        }
        if (bytes.Sum(b => (long)b.Length) > 16 * 1024 * 1024) throw new CollaborationSessionDeniedException();
        await transaction.CommitAsync(ct);
        return new(bytes.Select(Convert.ToBase64String).ToArray(), page.CommittedSequence, page.SnapshotSequence);
    }
    private static bool Valid(byte[] bytes, string hash) => bytes.Length is > 0 and <= 16 * 1024 * 1024
        && Convert.ToHexString(SHA256.HashData(bytes)).Equals(hash, StringComparison.OrdinalIgnoreCase);
}
