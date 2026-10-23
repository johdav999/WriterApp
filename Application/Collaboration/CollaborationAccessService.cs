using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WriterApp.Application.Security;
using WriterApp.Application.Subscriptions;
using WriterApp.Data;
using WriterApp.Data.Documents;
using WriterApp.Shared.Collaboration;

namespace WriterApp.Application.Collaboration;

public sealed class CollaborationSharingOptions { public bool Enabled { get; set; } }
public enum CollaborationOperation { Read, WriteText, Export, PrepareAi, Manage }

// No project-wide grant. Resource resolution precedes retrieval of prose, assets, search or AI context.
public sealed class CollaborationAccessService(AppDbContext db, IOptionsMonitor<CollaborationSharingOptions> options,
    IUserEntitlementStore entitlements, IDeletedUserIdentityService deletedUsers)
{
    public bool SharingEnabled => options.CurrentValue.Enabled;
    public IQueryable<DocumentRecord> Candidates(string actor) => db.Documents.AsNoTracking().Where(d => d.OwnerUserId == actor
        || SharingEnabled && d.CollaborationMode == CollaborationMode.Collaborative && db.CollaborationMembers.Any(m => m.DocumentId == d.Id && m.UserId == actor && !m.Revoked)
        || SharingEnabled && d.DocumentKind != DocumentKind.Manuscript && db.CollaborationSupportingLinks.Any(l => l.SupportingDocumentId == d.Id && !l.Revoked
            && db.Documents.Any(manuscript => manuscript.Id == l.ManuscriptId && manuscript.OwnerUserId == d.OwnerUserId && manuscript.ProjectId == d.ProjectId
                && !manuscript.IsArchived && manuscript.DeletedAtUtc == null && manuscript.CollaborationMode == CollaborationMode.Collaborative
                && db.CollaborationMembers.Any(m => m.DocumentId == manuscript.Id && m.UserId == actor && !m.Revoked))));

    public async Task<CollaborationAccess> Require(string actor, Guid id, CollaborationOperation operation, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(actor) || actor.Length > 128 || await deletedUsers.IsDeletedAsync(actor, ct)) throw new CollaborationSessionDeniedException();
        var document = await db.Documents.AsNoTracking().SingleOrDefaultAsync(d => d.Id == id && !d.IsArchived && d.DeletedAtUtc == null, ct)
            ?? throw new CollaborationSessionDeniedException();
        if (await deletedUsers.IsDeletedAsync(document.OwnerUserId, ct)) throw new CollaborationSessionDeniedException();
        var role = CollaborationRole.Owner; long revision = 0;
        if (document.OwnerUserId != actor)
        {
            if (!SharingEnabled || document.DocumentKind != DocumentKind.Manuscript || document.CollaborationMode != CollaborationMode.Collaborative) throw new CollaborationSessionDeniedException();
            var member = await db.CollaborationMembers.AsNoTracking().SingleOrDefaultAsync(m => m.DocumentId == id && m.UserId == actor && !m.Revoked, ct);
            if (member is null || member.Revision < 1 || member.Role is not (CollaborationRole.Editor or CollaborationRole.Viewer)) throw new CollaborationSessionDeniedException();
            role = member.Role; revision = member.Revision;
        }
        // Guests use the manuscript owner's subscription. Ordinary private owner reads retain legacy policy.
        if ((role != CollaborationRole.Owner || operation is CollaborationOperation.WriteText or CollaborationOperation.Manage)
            && !EntitlementAccessEvaluator.Evaluate(await entitlements.GetOrCreateAsync(document.OwnerUserId, ct)).IsPaidAccessActive)
            throw new CollaborationSessionDeniedException();
        bool editor = role is CollaborationRole.Editor or CollaborationRole.Owner;
        if (operation == CollaborationOperation.Manage && role != CollaborationRole.Owner
            || (operation is CollaborationOperation.WriteText or CollaborationOperation.Export or CollaborationOperation.PrepareAi) && !editor)
            throw new CollaborationSessionDeniedException();
        return new(id, document.OwnerUserId, actor, role, revision, editor, editor, editor);
    }

    public async Task<CollaborationAccess> RequireResource(string actor, Guid resourceId, CancellationToken ct)
    {
        var resource = await db.Documents.AsNoTracking().SingleOrDefaultAsync(d => d.Id == resourceId && !d.IsArchived && d.DeletedAtUtc == null, ct)
            ?? throw new CollaborationSessionDeniedException();
        if (resource.OwnerUserId == actor || resource.DocumentKind == DocumentKind.Manuscript) return await Require(actor, resourceId, CollaborationOperation.Read, ct);
        if (!SharingEnabled) throw new CollaborationSessionDeniedException();
        var manuscripts = await db.CollaborationSupportingLinks.AsNoTracking().Where(l => l.SupportingDocumentId == resourceId && !l.Revoked)
            .Join(Candidates(actor).Where(d => d.OwnerUserId == resource.OwnerUserId && d.ProjectId == resource.ProjectId), l => l.ManuscriptId, d => d.Id, (l, d) => d.Id).ToArrayAsync(ct);
        foreach (var id in manuscripts)
        {
            try { return await RequireSupporting(actor, id, resourceId, CollaborationOperation.Read, ct); }
            catch (CollaborationSessionDeniedException) { }
        }
        throw new CollaborationSessionDeniedException();
    }
    public async Task<CollaborationAccess> RequireSupporting(string actor, Guid manuscript, Guid resource, CollaborationOperation operation, CancellationToken ct)
    {
        if (operation is not (CollaborationOperation.Read or CollaborationOperation.PrepareAi)) throw new CollaborationSessionDeniedException();
        var access = await Require(actor, manuscript, operation, ct);
        var document = await db.Documents.AsNoTracking().SingleAsync(d => d.Id == manuscript, ct);
        if (!await db.CollaborationSupportingLinks.AnyAsync(l => l.ManuscriptId == manuscript && l.SupportingDocumentId == resource && !l.Revoked, ct)
            || !await db.Documents.AnyAsync(d => d.Id == resource && d.OwnerUserId == access.ResourceOwnerId && d.ProjectId == document.ProjectId
                && d.DocumentKind != DocumentKind.Manuscript && d.CollaborationMode == CollaborationMode.Legacy && !d.IsArchived && d.DeletedAtUtc == null, ct)) throw new CollaborationSessionDeniedException();
        return access with { DocumentId = resource, CanWriteText = false, CanExport = false };
    }
}
