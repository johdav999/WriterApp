using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using WriterApp.Application.Collaboration;
using WriterApp.Application.Security;
using WriterApp.Application.Subscriptions;
using WriterApp.Data.Collaboration;
using WriterApp.Data.Documents;
using WriterApp.Shared.Collaboration;
using Xunit;

namespace WriterApp.Tests;

public sealed partial class CollaborationConversionTests
{
    private sealed class SharingSettings : IOptionsMonitor<CollaborationSharingOptions>
    {
        public CollaborationSharingOptions CurrentValue { get; } = new() { Enabled = true };
        public CollaborationSharingOptions Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<CollaborationSharingOptions, string?> listener) => null;
    }
    private readonly SharingSettings _sharingSettings = new();
    private CollaborationAccessService Access => new(_db, _sharingSettings, _plans, new DeletedUserIdentityService(_db));
    private CollaborationSharingService Sharing => new(_db, Access, TimeProvider.System);
    private async Task<CollaborationMember> InviteMember(string actor, CollaborationRole role)
    {
        var invitation = await Sharing.Invite("owner", _document, new(Guid.NewGuid(), role, RecipientUserId: actor), default);
        return await Sharing.Accept(new(actor, null), new(invitation.Invitation.Id, invitation.Token!), default);
    }
    private async Task<(Guid Linked, Guid Private)> SupportingFixtures()
    {
        var doc = await _db.Documents.AsNoTracking().SingleAsync(d => d.Id == _document);
        Guid linked = Guid.NewGuid(), secret = Guid.NewGuid();
        foreach (var id in new[] { linked, secret })
        {
            var section = Guid.NewGuid(); _db.Documents.Add(new() { Id = id, ProjectId = doc.ProjectId, OwnerUserId = "owner", DocumentKind = DocumentKind.Notes, Title = id == linked ? "Linked notes" : "Private notes" });
            _db.Sections.Add(new() { Id = section, DocumentId = id });
            _db.Pages.Add(new() { Id = Guid.NewGuid(), DocumentId = id, SectionId = section, Content = id == linked ? "<p>Shared supporting fixture</p>" : "<p>PRIVATE SUPPORTING SECRET</p>" });
        }
        await _db.SaveChangesAsync(); _version = (await _db.DocumentSyncRecords.AsNoTracking().SingleAsync(s => s.DocumentId == _document)).Version; return (linked, secret);
    }

    [Fact]
    public async Task EmailInvitationResolvesOnlyOneTrustedProviderIdentityAndReturnsNoSecret()
    {
        await Convert();
        var issuer="https://trusted.fixture/";
        var config=new Microsoft.Extensions.Configuration.ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"NativeAuth:Issuer",issuer}}).Build();
        var controller=new WriterApp.Controllers.CollaborationSharingController(Sharing,Access,_db,new Users())
        { ControllerContext=new(){HttpContext=new Microsoft.AspNetCore.Http.DefaultHttpContext{User=new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,"owner")],"fixture"))}} };
        var request=new WriterApp.Controllers.CollaborationSharingController.EmailInvite(Guid.NewGuid(),"Editor@example.test",CollaborationRole.Viewer);
        Assert.IsType<Microsoft.AspNetCore.Mvc.ConflictObjectResult>(await controller.InviteEmail(_document,request,config,default));
        _db.ExternalIdentityLinks.Add(new(){UserId="wrong-provider",Issuer="https://untrusted.fixture/",ObjectIdentifier="wrong-provider",EmailAtLinkTime="editor@example.test"});
        await _db.SaveChangesAsync();
        Assert.IsType<Microsoft.AspNetCore.Mvc.ConflictObjectResult>(await controller.InviteEmail(_document,request,config,default));
        _db.ExternalIdentityLinks.Add(new(){UserId="editor",Issuer=issuer,ObjectIdentifier="editor",EmailAtLinkTime="editor@example.test"});
        await _db.SaveChangesAsync();
        var result=Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(await controller.InviteEmail(_document,request,config,default));
        var invite=Assert.IsType<CollaborationCreatedInvitation>(result.Value);Assert.Null(invite.Token);Assert.Equal("editor",invite.Invitation.RecipientUserId);
        var replay=Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(await controller.InviteEmail(_document,request,config,default));
        Assert.Equal(invite.Invitation.Id,Assert.IsType<CollaborationCreatedInvitation>(replay.Value).Invitation.Id);
        _db.ExternalIdentityLinks.Add(new(){UserId="ambiguous",Issuer=issuer,ObjectIdentifier="ambiguous",EmailAtLinkTime="editor@example.test"});
        await _db.SaveChangesAsync();
        Assert.IsType<Microsoft.AspNetCore.Mvc.ConflictObjectResult>(await controller.InviteEmail(_document,request with{OperationId=Guid.NewGuid()},config,default));
    }
    [Fact]
    public async Task InboxAcceptNeedsMatchingAccountAndCannotReplayAfterRevocation()
    {
        await Convert();
        var invite=await Sharing.Invite("owner",_document,new(Guid.NewGuid(),CollaborationRole.Editor,RecipientUserId:"editor"),default);
        await Assert.ThrowsAsync<CollaborationSessionDeniedException>(()=>Sharing.AcceptInbox(new("other",null),invite.Invitation.Id,default));
        var member=await Sharing.AcceptInbox(new("editor",null),invite.Invitation.Id,default);
        Assert.Equal(CollaborationRole.Editor,member.Role);
        await Sharing.Change("owner",_document,"editor",new(member.Revision,member.Role,true),default);
        await Assert.ThrowsAsync<CollaborationSessionDeniedException>(()=>Sharing.AcceptInbox(new("editor",null),invite.Invitation.Id,default));
        var viewer=await Sharing.Invite("owner",_document,new(Guid.NewGuid(),CollaborationRole.Viewer,RecipientUserId:"editor"),default);
        Assert.Equal(CollaborationRole.Viewer,(await Sharing.AcceptInbox(new("editor",null),viewer.Invitation.Id,default)).Role);
    }
    [Fact]
    public async Task InboxAcceptPreservesExpiryCancellationAndVerifiedEmailBoundaries()
    {
        await Convert();
        var invite=await Sharing.Invite("owner",_document,new(Guid.NewGuid(),CollaborationRole.Viewer,RecipientEmail:"editor@example.test"),default);
        await Assert.ThrowsAsync<CollaborationSessionDeniedException>(()=>Sharing.AcceptInbox(new("editor",null),invite.Invitation.Id,default));
        await Sharing.Cancel("owner",_document,invite.Invitation.Id,invite.Invitation.Revision,default);
        await Assert.ThrowsAsync<CollaborationSessionDeniedException>(()=>Sharing.AcceptInbox(new("editor","editor@example.test"),invite.Invitation.Id,default));
        var expired=await Sharing.Invite("owner",_document,new(Guid.NewGuid(),CollaborationRole.Viewer,RecipientUserId:"editor"),default);
        await _db.CollaborationInvitations.Where(i=>i.Id==expired.Invitation.Id).ExecuteUpdateAsync(s=>s.SetProperty(i=>i.ExpiresAtUnixMilliseconds,1));
        await Assert.ThrowsAsync<CollaborationSessionDeniedException>(()=>Sharing.AcceptInbox(new("editor",null),expired.Invitation.Id,default));
        var valid=await Sharing.Invite("owner",_document,new(Guid.NewGuid(),CollaborationRole.Viewer,RecipientUserId:"editor"),default);
        _sharingSettings.CurrentValue.Enabled=false;
        await Assert.ThrowsAsync<CollaborationSessionDeniedException>(()=>Sharing.AcceptInbox(new("editor",null),valid.Invitation.Id,default));
    }
    [Fact]
    public async Task InvitationIsIdentityBoundSingleUseAndSecretIsOnlyHashed()
    {
        await Convert(); var service = Sharing;
        var request = new CollaborationInviteRequest(Guid.NewGuid(), CollaborationRole.Editor, RecipientEmail: "Editor@example.test");
        var created = await service.Invite("owner", _document, request, default);
        Assert.NotNull(created.Token); Assert.Null((await service.Invite("owner", _document, request, default)).Token);
        var stored = await _db.CollaborationInvitations.AsNoTracking().SingleAsync();
        Assert.DoesNotContain(created.Token!, JsonSerializer.Serialize(stored));
        await Assert.ThrowsAsync<CollaborationSessionDeniedException>(() => service.Accept(new("other", "other@example.test"), new(created.Invitation.Id, created.Token!), default));
        await Assert.ThrowsAsync<CollaborationSessionDeniedException>(() => service.Accept(new("editor", null), new(created.Invitation.Id, created.Token!), default));
        await Assert.ThrowsAsync<CollaborationSessionDeniedException>(() => service.Accept(new("editor", "editor@example.test"), new(created.Invitation.Id, new string('x', 43)), default));
        var accepted = await service.Accept(new("editor", "editor@example.test"), new(created.Invitation.Id, created.Token!), default);
        Assert.Equal(CollaborationRole.Editor, accepted.Role);
        await Assert.ThrowsAsync<CollaborationSessionDeniedException>(() => service.Accept(new("editor", "editor@example.test"), new(created.Invitation.Id, created.Token!), default));
        await Assert.ThrowsAsync<CollaborationCommitException>(() => service.Invite("owner", _document, request with { Role = CollaborationRole.Viewer }, default));
        Assert.False(_db.Database.HasPendingModelChanges());
    }
    [Fact]
    public async Task EmailVerificationCannotUseDisplayOrPreferredUsernameClaims()
    {
        var users = new Users();
        ClaimsPrincipal User(params Claim[] claims) => new(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "editor") }.Concat(claims), "verified-test"));
        Assert.Null(CollaborationInviteIdentity.From(User(new Claim("preferred_username", "e@example.test"), new Claim("email_verified", "true")), users).VerifiedEmail);
        Assert.Null(CollaborationInviteIdentity.From(User(new Claim("email", "e@example.test")), users).VerifiedEmail);
        Assert.Equal("e@example.test", CollaborationInviteIdentity.From(User(new Claim("email", "E@example.test"), new Claim("email_verified", "true")), users).VerifiedEmail);
    }
    [Fact]
    public async Task ExpiryCancellationDeclineAndDisabledSharingNeverGrantAccess()
    {
        await Convert();
        var expired = await Sharing.Invite("owner", _document, new(Guid.NewGuid(), CollaborationRole.Editor, RecipientUserId: "expired"), default);
        await _db.CollaborationInvitations.Where(i => i.Id == expired.Invitation.Id).ExecuteUpdateAsync(s => s.SetProperty(i => i.ExpiresAtUnixMilliseconds, 1));
        await Assert.ThrowsAsync<CollaborationSessionDeniedException>(() => Sharing.Accept(new("expired", null), new(expired.Invitation.Id, expired.Token!), default));
        var cancelled = await Sharing.Invite("owner", _document, new(Guid.NewGuid(), CollaborationRole.Editor, RecipientUserId: "cancelled"), default);
        await Sharing.Cancel("owner", _document, cancelled.Invitation.Id, 1, default);
        await Assert.ThrowsAsync<CollaborationSessionDeniedException>(() => Sharing.Accept(new("cancelled", null), new(cancelled.Invitation.Id, cancelled.Token!), default));
        var declined = await Sharing.Invite("owner", _document, new(Guid.NewGuid(), CollaborationRole.Viewer, RecipientUserId: "declined"), default);
        await Sharing.Decline(new("declined", null), declined.Invitation.Id, default);
        await Assert.ThrowsAsync<CollaborationSessionDeniedException>(() => Sharing.Accept(new("declined", null), new(declined.Invitation.Id, declined.Token!), default));
        await InviteMember("editor", CollaborationRole.Editor); _sharingSettings.CurrentValue.Enabled = false;
        await Assert.ThrowsAsync<CollaborationSessionDeniedException>(() => Access.Require("editor", _document, CollaborationOperation.Read, default));
        Assert.Equal(CollaborationRole.Owner, (await Access.Require("owner", _document, CollaborationOperation.Read, default)).Role);
    }
    [Fact]
    public async Task OwnerFundedEditorsAndViewersObeyEveryOperationAndCannotManageOrReachPrivateResources()
    {
        var support = await SupportingFixtures(); await Convert();
        _plans.UnpaidUsers.UnionWith(["editor", "viewer"]);
        await InviteMember("editor", CollaborationRole.Editor); await InviteMember("viewer", CollaborationRole.Viewer);
        Assert.True((await Access.Require("editor", _document, CollaborationOperation.WriteText, default)).CanWriteText);
        Assert.True((await Access.Require("editor", _document, CollaborationOperation.Export, default)).CanExport);
        await Access.Require("viewer", _document, CollaborationOperation.Read, default);
        foreach (var op in new[] { CollaborationOperation.WriteText, CollaborationOperation.Export, CollaborationOperation.PrepareAi, CollaborationOperation.Manage })
            await Assert.ThrowsAsync<CollaborationSessionDeniedException>(() => Access.Require("viewer", _document, op, default));
        await Assert.ThrowsAsync<CollaborationSessionDeniedException>(() => Sharing.Invite("editor", _document, new(Guid.NewGuid(), CollaborationRole.Editor, RecipientUserId: "stranger"), default));
        await Assert.ThrowsAsync<CollaborationSessionDeniedException>(() => Access.RequireResource("editor", support.Private, default));
        await Sharing.Supporting("owner", _document, new(support.Linked), default);
        await Access.RequireSupporting("viewer", _document, support.Linked, CollaborationOperation.Read, default);
        await Access.RequireSupporting("editor", _document, support.Linked, CollaborationOperation.PrepareAi, default);
        await Assert.ThrowsAsync<CollaborationSessionDeniedException>(() => Access.RequireSupporting("editor", _document, support.Private, CollaborationOperation.Read, default));
        await Sharing.Supporting("owner", _document, new(support.Linked, 1, true), default);
        await Assert.ThrowsAsync<CollaborationSessionDeniedException>(() => Access.RequireResource("editor", support.Linked, default));
        _plans.Paid = false;
        await Assert.ThrowsAsync<CollaborationSessionDeniedException>(() => Access.Require("editor", _document, CollaborationOperation.Read, default));
    }
    [Fact]
    public async Task DowngradeAndRegrantFenceTicketsReceiptsAndOriginalIdentity()
    {
        await Convert(); var member = await InviteMember("editor", CollaborationRole.Editor);
        _plans.UnpaidUsers.Add("editor");
        var state = await _db.CollaborationPages.AsNoTracking().SingleAsync(p => p.PageId == _page);
        var settings = new SessionOptions(); var protection = new EphemeralDataProtectionProvider();
        var sessions = new CollaborationSessionService(_db, protection, settings, _plans, new DeletedUserIdentityService(_db), TimeProvider.System, Access);
        var request = new CollaborationSessionRequest(state.Generation, Guid.NewGuid(), 2, 1, state.SchemaFingerprint, true, member.Revision);
        var issued = await sessions.Issue("editor", _document, _page, request, default);
        var scope = new CollaborationServiceRequest(issued.Ticket, issued.RoomName, Guid.NewGuid()); await sessions.Authorize(scope with { Acquire = true }, default);
        var commits = new CollaborationCommitService(_db, sessions, settings, TimeProvider.System); var worker = Guid.NewGuid(); var lease = await commits.Lease(new(scope, worker), default);
        byte[] bytes = [0, 0];
        var receipt = await commits.Commit(new(scope, worker, lease.Epoch, 0, Guid.NewGuid(), bytes, System.Convert.ToHexString(SHA256.HashData(bytes))), default);
        Assert.Equal("editor", (await _db.CollaborationUpdates.AsNoTracking().SingleAsync()).AuthorUserId);
        await Sharing.Change("owner", _document, "editor", new(member.Revision, CollaborationRole.Viewer), default);
        await Assert.ThrowsAsync<CollaborationSessionDeniedException>(() => sessions.Authorize(scope, default));
        await Assert.ThrowsAsync<CollaborationSessionDeniedException>(() => sessions.Issue("editor", _document, _page, request, default));
        var read = await sessions.Issue("editor", _document, _page, request with { RequestCommit = false, MembershipRevision = 2 }, default); Assert.True(read.ReadOnly);
        await Assert.ThrowsAsync<CollaborationCommitException>(() => Sharing.Change("owner", _document, "editor", new(1, CollaborationRole.Editor), default));
        await Sharing.Change("owner", _document, "editor", new(2, CollaborationRole.Editor, true), default);
        await Assert.ThrowsAsync<CollaborationSessionDeniedException>(async () =>
        { var authorized = await sessions.Authorize(new(read.Ticket, read.RoomName, Guid.NewGuid(), true), default); await sessions.Bootstrap(authorized, default); });
        var regranted = await InviteMember("editor", CollaborationRole.Editor);
        await Assert.ThrowsAsync<CollaborationSessionDeniedException>(() => sessions.Issue("editor", _document, _page, request, default));
        Assert.False((await sessions.Issue("editor", _document, _page, request with { MembershipRevision = regranted.Revision }, default)).ReadOnly);
        await Assert.ThrowsAsync<CollaborationSessionDeniedException>(() => sessions.Authorize(scope, default));
        Assert.Single(await _db.CollaborationReceipts.ToArrayAsync()); Assert.Equal(1, receipt.Receipt.CommitSequence);
    }
    [Fact]
    public async Task SharedHttpDiscoveryExportAiAndSupportingAccessAreScopedAndRevocationRemovesDiscovery()
    {
        var support = await SupportingFixtures(); await Convert(); await InviteMember("editor", CollaborationRole.Editor); await InviteMember("viewer", CollaborationRole.Viewer);
        await Sharing.Supporting("owner", _document, new(support.Linked), default);
        using var server = Api(sharing: true); using var client = server.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Owner", "editor");
        string root = $"/api/documents/{_document}/collaboration";
        var readResponse = await client.PostAsJsonAsync(root + "/read", new CollaborationReadRequest()); Assert.Equal(HttpStatusCode.OK, readResponse.StatusCode);
        var snapshot = (await readResponse.Content.ReadFromJsonAsync<CollaborationReadSnapshot>())!;
        var vector = new CollaborationReadRequest(snapshot.StructureRevision, snapshot.Pages.Select(p => new CollaborationWatermark(p.PageId, p.Generation, p.Sequence)).ToArray(), [support.Linked]);
        var exported = await client.PostAsJsonAsync($"/api/documents/{_document}/export/print", new WriterApp.Application.Exporting.ExportDocumentRequest(_document, "html", null, "document", Collaboration: vector));
        Assert.Equal(HttpStatusCode.OK, exported.StatusCode); Assert.DoesNotContain("PRIVATE SUPPORTING SECRET", await exported.Content.ReadAsStringAsync());
        var ai = await client.PostAsJsonAsync(root + "/ai-context", vector); Assert.Equal(HttpStatusCode.OK, ai.StatusCode); Assert.Contains("Shared supporting fixture", await ai.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(root + "/ai-context", vector with { SupportingDocumentIds = [support.Private] })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/collaboration/manuscripts/{_document}/supporting/{support.Private}")).StatusCode);
        string shared = await client.GetStringAsync("/api/collaboration/shared"); Assert.Contains(_document.ToString(), shared); Assert.DoesNotContain("Private notes", shared);
        client.DefaultRequestHeaders.Remove("X-Test-Owner"); client.DefaultRequestHeaders.Add("X-Test-Owner", "viewer");
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(root + "/read", vector)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(root + "/ai-context", vector)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync($"/api/documents/{_document}/export/print", new WriterApp.Application.Exporting.ExportDocumentRequest(_document, "html", null, "document", Collaboration: vector))).StatusCode);
        await Sharing.Change("owner", _document, "viewer", new(1, CollaborationRole.Viewer, true), default);
        Assert.DoesNotContain(_document.ToString(), await client.GetStringAsync("/api/collaboration/shared"));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(root + "/read", vector)).StatusCode);
    }
}
