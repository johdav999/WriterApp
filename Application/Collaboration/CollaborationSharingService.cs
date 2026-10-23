using System.Data;
using System.Net.Mail;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WriterApp.Application.Security;
using WriterApp.Data;
using WriterApp.Data.Collaboration;
using WriterApp.Data.Documents;
using WriterApp.Shared.Collaboration;

namespace WriterApp.Application.Collaboration;

public sealed record CollaborationInviteIdentity(string UserId, string? VerifiedEmail)
{
    public static CollaborationInviteIdentity From(ClaimsPrincipal user, IUserIdResolver users)
    {
        if (user.Identity?.IsAuthenticated != true) throw new CollaborationSessionDeniedException();
        string? email = null;
        var identities = user.Identities.Where(i => i.IsAuthenticated).ToArray();
        if (identities.Length == 1 && identities[0].Claims.Any(c => c.Type == "email_verified" && c.Value.Equals("true", StringComparison.OrdinalIgnoreCase)))
        {
            var emails = identities[0].Claims.Where(c => c.Type is "email" or ClaimTypes.Email).Select(c => CollaborationSharingService.Email(c.Value)).Distinct().ToArray();
            if (emails.Length == 1) email = emails[0];
        }
        return new(users.ResolveUserId(user), email);
    }
}

public sealed class CollaborationSharingService(AppDbContext db, CollaborationAccessService access, TimeProvider clock)
{
    private long Now => clock.GetUtcNow().ToUnixTimeMilliseconds();
    private static CollaborationCommitException Conflict(string code) => new("collaboration_" + code);
    internal static string Email(string value)
    {
        value = value.Trim();
        if (value.Length is < 3 or > 254 || value.Any(char.IsControl) || !MailAddress.TryCreate(value, out var mail) || mail.Address != value)
            throw Conflict("invitation_target_invalid");
        return value.ToLowerInvariant();
    }
    private static void Role(CollaborationRole role) { if (role is not (CollaborationRole.Viewer or CollaborationRole.Editor)) throw Conflict("role_invalid"); }
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private async Task<T> Transaction<T>(Guid id, Func<Task<T>> work, CancellationToken ct) => await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await CollaborationMigrationService.Lock(db, id, ct);
        var result = await work(); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return result;
    });
    private async Task<CollaborationAccess> Manage(string actor, Guid document, CancellationToken ct)
    {
        if (!access.SharingEnabled) throw new CollaborationSessionDeniedException();
        var grant = await access.Require(actor, document, CollaborationOperation.Manage, ct);
        if (!await db.Documents.AnyAsync(d => d.Id == document && d.DocumentKind == DocumentKind.Manuscript && d.CollaborationMode == CollaborationMode.Collaborative, ct)) throw new CollaborationSessionDeniedException();
        return grant;
    }
    public Task<CollaborationCreatedInvitation> Invite(string actor, Guid id, CollaborationInviteRequest request, CancellationToken ct)
    {
        Role(request.Role);
        string? user = request.RecipientUserId, email = request.RecipientEmail is null ? null : Email(request.RecipientEmail);
        if (request.OperationId == Guid.Empty || request.LifetimeHours is < 1 or > 168 || (user is null) == (email is null)
            || user is not null && (string.IsNullOrWhiteSpace(user) || user.Length > 128 || user.Any(char.IsControl) || user == actor)) throw Conflict("invitation_target_invalid");
        string hash = Hash(JsonSerializer.Serialize(new { id, request.OperationId, request.Role, user, email, request.LifetimeHours }));
        return Transaction(id, async () =>
        {
            await Manage(actor, id, ct);
            var prior = await db.CollaborationInvitations.SingleOrDefaultAsync(i => i.Id == request.OperationId, ct);
            if (prior is not null)
            {
                if (prior.DocumentId != id || prior.RequestSha256 != hash) throw Conflict("invitation_identity_reused");
                // Only a hash is stored. An uncertain creator can cancel/recreate, never retrieve an old secret.
                return new CollaborationCreatedInvitation(View(prior), null, true);
            }
            if (await db.CollaborationInvitations.CountAsync(i => i.DocumentId == id && !i.Revoked && i.AcceptedBy == null && i.ExpiresAtUnixMilliseconds > Now, ct) >= 100) throw Conflict("invitation_limit");
            string token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            var invitation = new CollaborationInvitationRecord { Id = request.OperationId, DocumentId = id, Role = request.Role, RecipientUserId = user,
                RecipientEmail = email, RequestSha256 = hash, TokenSha256 = Hash(token), ExpiresAtUnixMilliseconds = Now + request.LifetimeHours * 3600000L };
            db.CollaborationInvitations.Add(invitation); return new CollaborationCreatedInvitation(View(invitation), token, false);
        }, ct);
    }
    private static bool Target(CollaborationInvitationRecord invitation, CollaborationInviteIdentity identity) =>
        invitation.RecipientUserId == identity.UserId || invitation.RecipientEmail is not null && identity.VerifiedEmail == invitation.RecipientEmail;
    public Task<CollaborationMember> Accept(CollaborationInviteIdentity identity, CollaborationAcceptInvitation request, CancellationToken ct)
        => AcceptCore(identity, request, false, ct);
    public Task<CollaborationMember> AcceptInbox(CollaborationInviteIdentity identity, Guid invitationId, CancellationToken ct)
        => AcceptCore(identity, new(invitationId, ""), true, ct);
    private async Task<CollaborationMember> AcceptCore(CollaborationInviteIdentity identity, CollaborationAcceptInvitation request, bool fromInbox, CancellationToken ct)
    {
        if (!access.SharingEnabled || !fromInbox && (request.Token is null || request.Token.Length != 43)) throw new CollaborationSessionDeniedException();
        var id = await db.CollaborationInvitations.AsNoTracking().Where(i => i.Id == request.InvitationId).Select(i => (Guid?)i.DocumentId).SingleOrDefaultAsync(ct)
            ?? throw new CollaborationSessionDeniedException();
        return await Transaction<CollaborationMember>(id, async () =>
        {
            if (string.IsNullOrWhiteSpace(identity.UserId) || identity.UserId.Length > 128 || identity.UserId.Any(char.IsControl)) throw new CollaborationSessionDeniedException();
            var invite = await db.CollaborationInvitations.SingleAsync(i => i.Id == request.InvitationId, ct);
            if (invite.Revoked || invite.AcceptedBy is not null || invite.ExpiresAtUnixMilliseconds <= Now || !Target(invite, identity)
                || !fromInbox && !CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(invite.TokenSha256), Encoding.ASCII.GetBytes(Hash(request.Token)))
                || await db.DeletedUserIdentities.AnyAsync(u => u.UserId == identity.UserId, ct)) throw new CollaborationSessionDeniedException();
            var document = await db.Documents.AsNoTracking().SingleAsync(d => d.Id == id, ct); await Manage(document.OwnerUserId, id, ct);
            if (identity.UserId == document.OwnerUserId) throw Conflict("owner_membership_immutable");
            if (await db.CollaborationMembers.CountAsync(m => m.UserId == identity.UserId && !m.Revoked, ct) >= 100) throw Conflict("membership_limit");
            if (await db.CollaborationMembers.CountAsync(m => m.DocumentId == id && !m.Revoked, ct) >= 100) throw Conflict("membership_limit");
            Role(invite.Role);
            var member = await db.CollaborationMembers.SingleOrDefaultAsync(m => m.DocumentId == id && m.UserId == identity.UserId, ct);
            if (member is { Revoked: false }) throw Conflict("member_already_active");
            if (member is null) { member = new() { DocumentId = id, UserId = identity.UserId }; db.CollaborationMembers.Add(member); }
            else member.Revision = checked(member.Revision + 1);
            member.Role = invite.Role; member.Revoked = false;
            invite.AcceptedBy = identity.UserId; invite.AcceptedAtUnixMilliseconds = Now; invite.Revision++;
            await StopSessions(id, identity.UserId, ct); return new(member.UserId, member.Role, member.Revision, false);
        }, ct);
    }
    public Task<CollaborationMember> Change(string actor, Guid id, string user, CollaborationChangeMember request, CancellationToken ct)
    {
        Role(request.Role);
        return Transaction<CollaborationMember>(id, async () =>
        {
            var grant = await Manage(actor, id, ct);
            if (user == grant.ResourceOwnerId) throw Conflict("owner_membership_immutable");
            var member = await db.CollaborationMembers.SingleOrDefaultAsync(m => m.DocumentId == id && m.UserId == user, ct) ?? throw Conflict("member_missing");
            if (member.Revision != request.ExpectedRevision) throw Conflict("membership_revision_changed");
            if (member.Revoked && !request.Revoke && (await db.CollaborationMembers.CountAsync(m => m.UserId == user && !m.Revoked, ct) >= 100
                || await db.CollaborationMembers.CountAsync(m => m.DocumentId == id && !m.Revoked, ct) >= 100)) throw Conflict("membership_limit");
            member.Role = request.Role; member.Revoked = request.Revoke; member.Revision = checked(member.Revision + 1);
            await StopSessions(id, user, ct); return new(member.UserId, member.Role, member.Revision, member.Revoked);
        }, ct);
    }
    private Task<int> StopSessions(Guid id, string actor, CancellationToken ct) => db.CollaborationSessions.Where(s => s.DocumentId == id && s.OwnerUserId == actor && !s.Revoked)
        .ExecuteUpdateAsync(s => s.SetProperty(v => v.Revoked, true).SetProperty(v => v.Version, v => v.Version + 1), ct);

    public Task<bool> Cancel(string actor, Guid id, Guid invitation, long revision, CancellationToken ct) => Transaction(id, async () =>
    {
        await Manage(actor, id, ct);
        var row = await db.CollaborationInvitations.SingleOrDefaultAsync(i => i.Id == invitation && i.DocumentId == id, ct) ?? throw Conflict("invitation_missing");
        if (row.Revision != revision || row.AcceptedBy is not null) throw Conflict("invitation_revision_changed");
        row.Revoked = true; row.Revision++; return true;
    }, ct);
    public async Task Decline(CollaborationInviteIdentity identity, Guid invitation, CancellationToken ct)
    {
        var row = await db.CollaborationInvitations.AsNoTracking().SingleOrDefaultAsync(i => i.Id == invitation, ct) ?? throw new CollaborationSessionDeniedException();
        await Transaction(row.DocumentId, async () =>
        {
            var current = await db.CollaborationInvitations.SingleAsync(i => i.Id == invitation, ct);
            if (!access.SharingEnabled || !Target(current, identity) || current.AcceptedBy is not null
                || await db.DeletedUserIdentities.AnyAsync(u => u.UserId == identity.UserId, ct)) throw new CollaborationSessionDeniedException();
            current.Revoked = true; current.Revision++; return true;
        }, ct);
    }
    public async Task<CollaborationInvitation[]> Inbox(CollaborationInviteIdentity identity, CancellationToken ct)
    {
        if (!access.SharingEnabled || await db.DeletedUserIdentities.AnyAsync(d => d.UserId == identity.UserId, ct)) return [];
        var invitations = await db.CollaborationInvitations.AsNoTracking().Where(i => !i.Revoked && i.AcceptedBy == null && i.ExpiresAtUnixMilliseconds > Now
            && (i.RecipientUserId == identity.UserId || identity.VerifiedEmail != null && i.RecipientEmail == identity.VerifiedEmail)).Take(101).ToArrayAsync(ct);
        return invitations.Take(100).Select(View).ToArray();
    }
    public async Task<CollaborationMember[]> Members(string actor, Guid id, CancellationToken ct)
    {
        var grant = await Manage(actor, id, ct);
        return new[] { new CollaborationMember(grant.ResourceOwnerId, CollaborationRole.Owner, 0, false) }.Concat(
            await db.CollaborationMembers.AsNoTracking().Where(m => m.DocumentId == id).OrderBy(m => m.UserId).Select(m => new CollaborationMember(m.UserId, m.Role, m.Revision, m.Revoked)).ToArrayAsync(ct)).ToArray();
    }
    public async Task<CollaborationInvitation[]> Outgoing(string actor, Guid id, CancellationToken ct)
    {
        await Manage(actor, id, ct);
        return (await db.CollaborationInvitations.AsNoTracking().Where(i => i.DocumentId == id && !i.Revoked && i.AcceptedBy == null && i.ExpiresAtUnixMilliseconds > Now)
            .OrderByDescending(i => i.ExpiresAtUnixMilliseconds).Take(100).ToArrayAsync(ct)).Select(View).ToArray();
    }
    public Task<long> Supporting(string actor, Guid id, CollaborationSupportingLink request, CancellationToken ct) => Transaction(id, async () =>
    {
        await Manage(actor, id, ct); var manuscript = await db.Documents.AsNoTracking().SingleAsync(d => d.Id == id, ct);
        if (!await db.Documents.AnyAsync(d => d.Id == request.SupportingDocumentId && d.OwnerUserId == actor && d.ProjectId == manuscript.ProjectId
            && d.DocumentKind != DocumentKind.Manuscript && d.CollaborationMode == CollaborationMode.Legacy && !d.IsArchived && d.DeletedAtUtc == null, ct)) throw new CollaborationSessionDeniedException();
        var link = await db.CollaborationSupportingLinks.SingleOrDefaultAsync(l => l.ManuscriptId == id && l.SupportingDocumentId == request.SupportingDocumentId, ct);
        if (link is null)
        {
            if (request.ExpectedRevision != 0 || request.Remove) throw Conflict("link_revision_changed");
            link = new() { ManuscriptId = id, SupportingDocumentId = request.SupportingDocumentId }; db.CollaborationSupportingLinks.Add(link);
        }
        else { if (link.Revision != request.ExpectedRevision) throw Conflict("link_revision_changed"); link.Revision++; link.Revoked = request.Remove; }
        return link.Revision;
    }, ct);
    public async Task<CollaborationSupportingChoice[]> SupportingChoices(string actor, Guid id, CancellationToken ct)
    {
        await Manage(actor, id, ct); var project = await db.Documents.AsNoTracking().Where(d => d.Id == id).Select(d => d.ProjectId).SingleAsync(ct);
        var documents = await db.Documents.AsNoTracking().Where(d => d.OwnerUserId == actor && d.ProjectId == project && d.DocumentKind != DocumentKind.Manuscript
            && d.CollaborationMode == CollaborationMode.Legacy && !d.IsArchived && d.DeletedAtUtc == null).OrderBy(d => d.Title).Take(101).Select(d => new { d.Id, d.Title }).ToArrayAsync(ct);
        if (documents.Length > 100) throw Conflict("supporting_limit");
        var links = await db.CollaborationSupportingLinks.AsNoTracking().Where(l => l.ManuscriptId == id).ToArrayAsync(ct);
        return documents.Select(d => { var link = links.SingleOrDefault(l => l.SupportingDocumentId == d.Id); return new CollaborationSupportingChoice(d.Id, d.Title, link?.Revision ?? 0, link is { Revoked: false }); }).ToArray();
    }
    private CollaborationInvitation View(CollaborationInvitationRecord i) => new(i.Id, i.DocumentId, i.Role, i.RecipientUserId, i.RecipientEmail,
        i.ExpiresAtUnixMilliseconds, i.Revision, i.AcceptedBy is not null ? "accepted" : i.Revoked ? "revoked" : i.ExpiresAtUnixMilliseconds <= Now ? "expired" : "pending");
}
