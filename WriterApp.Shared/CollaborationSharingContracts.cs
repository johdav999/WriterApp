namespace WriterApp.Shared.Collaboration;

public enum CollaborationRole { Viewer = 1, Editor = 2, Owner = 3 }
public sealed record CollaborationAccess(Guid DocumentId, string ResourceOwnerId, string ActorId,
    CollaborationRole Role, long MembershipRevision, bool CanWriteText, bool CanExport, bool CanPrepareAi);
public sealed record CollaborationInviteRequest(Guid OperationId, CollaborationRole Role, string? RecipientUserId = null,
    string? RecipientEmail = null, int LifetimeHours = 168);
public sealed record CollaborationInvitation(Guid Id, Guid DocumentId, CollaborationRole Role, string? RecipientUserId,
    string? RecipientEmail, long ExpiresAtUnixMilliseconds, long Revision, string State, string? DocumentTitle = null, string? RecipientName = null);
public sealed record CollaborationCreatedInvitation(CollaborationInvitation Invitation, string? Token, bool Replayed);
public sealed record CollaborationAcceptInvitation(Guid InvitationId, string Token);
public sealed record CollaborationChangeMember(long ExpectedRevision, CollaborationRole Role, bool Revoke = false);
public sealed record CollaborationMember(string UserId, CollaborationRole Role, long Revision, bool Revoked, string? DisplayName = null);
public sealed record CollaborationSharedDocument(Guid DocumentId, string Title, string OwnerId, CollaborationRole Role, long MembershipRevision);
public sealed record CollaborationSupportingLink(Guid SupportingDocumentId, long ExpectedRevision = 0, bool Remove = false);
public sealed record CollaborationSupportingChoice(Guid DocumentId, string Title, long Revision, bool Shared);
