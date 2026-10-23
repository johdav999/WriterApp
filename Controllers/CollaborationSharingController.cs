using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WriterApp.Application.Collaboration;
using WriterApp.Application.Security;
using WriterApp.Data;
using WriterApp.Shared.Collaboration;

namespace WriterApp.Controllers;

[ApiController, Authorize, RequestSizeLimit(16384), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None), Route("api/collaboration")]
public sealed class CollaborationSharingController(CollaborationSharingService sharing, CollaborationAccessService access,
    AppDbContext db, IUserIdResolver users, Microsoft.Extensions.Options.IOptions<CollaborationInteractionOptions>? interactions = null) : ControllerBase
{
    private string Actor => users.ResolveUserId(User);
    [HttpGet("owned")]
    public async Task<IActionResult> Owned(CancellationToken ct)
    {
        var ids=await access.Candidates(Actor).Where(d=>d.OwnerUserId==Actor && d.CollaborationMode==CollaborationMode.Collaborative && !d.IsArchived && d.DeletedAtUtc==null).Select(d=>new{d.Id,d.Title}).Take(100).ToArrayAsync(ct);
        var result=new List<CollaborationSharedDocument>();
        foreach(var d in ids){try{var grant=await access.Require(Actor,d.Id,CollaborationOperation.Read,ct);result.Add(new(d.Id,d.Title,Actor,grant.Role,grant.MembershipRevision));}catch(CollaborationSessionDeniedException){}}
        return Ok(new{complete=true,documents=result});
    }
    private async Task<CollaborationInvitation[]> Describe(CollaborationInvitation[] invites,CancellationToken ct)
    {
        var ids=invites.Select(i=>i.DocumentId).ToArray();
        var titles=await db.Documents.Where(d=>ids.Contains(d.Id)).ToDictionaryAsync(d=>d.Id,d=>d.Title,ct);
        var users=invites.Select(i=>i.RecipientUserId).ToArray();
        var names=await db.ExternalIdentityLinks.Where(l=>users.Contains(l.UserId)&&l.EmailAtLinkTime!=null).Select(l=>new{l.UserId,l.EmailAtLinkTime}).ToArrayAsync(ct);
        return invites.Select(i=>i with{DocumentTitle=titles.GetValueOrDefault(i.DocumentId),RecipientName=names.FirstOrDefault(n=>n.UserId==i.RecipientUserId)?.EmailAtLinkTime}).ToArray();
    }
    [HttpGet("availability")]
    public IActionResult Availability() => Ok(new { enabled = access.SharingEnabled, interactionsEnabled = interactions?.Value.Enabled == true });
    [HttpGet("shared")]
    public async Task<IActionResult> Shared(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        var candidates = await access.Candidates(Actor).Where(d => d.OwnerUserId != Actor && d.CollaborationMode == CollaborationMode.Collaborative
            && !d.IsArchived && d.DeletedAtUtc == null).OrderBy(d => d.Id).Select(d => new { d.Id, d.Title }).Take(101).ToArrayAsync(ct);
        if (candidates.Length > 100) throw new CollaborationCommitException("collaboration_membership_limit");
        var result = new List<CollaborationSharedDocument>();
        foreach (var d in candidates)
        {
            try { var grant = await access.Require(Actor, d.Id, CollaborationOperation.Read, ct); result.Add(new(d.Id, d.Title, grant.ResourceOwnerId, grant.Role, grant.MembershipRevision)); }
            catch (CollaborationSessionDeniedException) { }
        }
        // A complete authenticated snapshot lets each client remove lost grants without adopting another account's data.
        return Ok(new { complete = true, documents = result });
    }
    [HttpGet("invitations")]
    public async Task<IActionResult> Inbox(CancellationToken ct)
    { Response.Headers.CacheControl = "no-store"; return Ok(await Describe(await sharing.Inbox(CollaborationInviteIdentity.From(User, users), ct),ct)); }
    [HttpPost("invitations/accept")]
    public async Task<IActionResult> Accept(CollaborationAcceptInvitation request, CancellationToken ct) => Ok(await sharing.Accept(CollaborationInviteIdentity.From(User, users), request, ct));
    [HttpPost("invitations/{id:guid}/accept")]
    public async Task<IActionResult> AcceptInbox(Guid id, CancellationToken ct) => Ok(await sharing.AcceptInbox(CollaborationInviteIdentity.From(User, users), id, ct));
    public sealed record EmailInvite(Guid OperationId, string Email, CollaborationRole Role);
    [HttpPost("manuscripts/{documentId:guid}/invitations/by-email")]
    public async Task<IActionResult> InviteEmail(Guid documentId, EmailInvite request, [FromServices] IConfiguration configuration, CancellationToken ct)
    {
        await access.Require(Actor,documentId,CollaborationOperation.Manage,ct);
        string email=CollaborationSharingService.Email(request.Email),issuer=configuration["NativeAuth:Issuer"]??"";
        // Resolve only identities recorded from the configured provider, never editable profile email.
        var recipients=await db.ExternalIdentityLinks.AsNoTracking().Where(l=>l.Issuer==issuer&&l.ObjectIdentifier!=null&&l.EmailAtLinkTime!=null&&l.EmailAtLinkTime.ToLower()==email)
            .Select(l=>l.UserId).Distinct().Take(2).ToArrayAsync(ct);
        if(issuer.Length==0||recipients.Length!=1)return Conflict(new{code="collaboration_recipient_unknown",message="Ask this person to sign in to Prosa on this build first, then invite them again."});
        var result=await sharing.Invite(Actor,documentId,new(request.OperationId,request.Role,RecipientUserId:recipients[0]),ct);
        // The addressed account accepts in its authenticated inbox; the secret is not exposed in this UI.
        return Ok(result with {Token=null});
    }
    [HttpPost("invitations/{id:guid}/decline")]
    public async Task<IActionResult> Decline(Guid id, CancellationToken ct)
    { await sharing.Decline(CollaborationInviteIdentity.From(User, users), id, ct); return NoContent(); }
    [HttpPost("manuscripts/{documentId:guid}/invitations")]
    public async Task<IActionResult> Invite(Guid documentId, CollaborationInviteRequest request, CancellationToken ct)
    { Response.Headers.CacheControl = "no-store"; return Ok(await sharing.Invite(Actor, documentId, request, ct)); }
    [HttpGet("manuscripts/{documentId:guid}/invitations")]
    public async Task<IActionResult> Outgoing(Guid documentId, CancellationToken ct) => Ok(await Describe(await sharing.Outgoing(Actor, documentId, ct),ct));
    [HttpPost("manuscripts/{documentId:guid}/invitations/{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid documentId, Guid id, [FromBody] long revision, CancellationToken ct) => Ok(await sharing.Cancel(Actor, documentId, id, revision, ct));
    [HttpGet("manuscripts/{documentId:guid}/members")]
    public async Task<IActionResult> Members(Guid documentId, CancellationToken ct)
    {
        var members=await sharing.Members(Actor,documentId,ct);var ids=members.Select(m=>m.UserId).ToArray();
        var names=await db.ExternalIdentityLinks.Where(l=>ids.Contains(l.UserId)&&l.EmailAtLinkTime!=null).Select(l=>new{l.UserId,l.EmailAtLinkTime}).ToArrayAsync(ct);
        return Ok(members.Select(m=>m with{DisplayName=names.FirstOrDefault(n=>n.UserId==m.UserId)?.EmailAtLinkTime}));
    }
    [HttpPost("manuscripts/{documentId:guid}/members/{userId}/change")]
    public async Task<IActionResult> Change(Guid documentId, string userId, CollaborationChangeMember request, CancellationToken ct) => Ok(await sharing.Change(Actor, documentId, userId, request, ct));
    [HttpPost("manuscripts/{documentId:guid}/supporting")]
    public async Task<IActionResult> Supporting(Guid documentId, CollaborationSupportingLink request, CancellationToken ct) => Ok(new { revision = await sharing.Supporting(Actor, documentId, request, ct) });
    [HttpGet("manuscripts/{documentId:guid}/supporting")]
    public async Task<IActionResult> SupportingChoices(Guid documentId, CancellationToken ct) => Ok(await sharing.SupportingChoices(Actor, documentId, ct));
    [HttpGet("manuscripts/{documentId:guid}/supporting/{supportingId:guid}")]
    public async Task<IActionResult> ReadSupporting(Guid documentId, Guid supportingId, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        return Ok(await CollaborationSupportingReader.Read(db, access, Actor, documentId, supportingId, CollaborationOperation.Read, ct));
    }
}
