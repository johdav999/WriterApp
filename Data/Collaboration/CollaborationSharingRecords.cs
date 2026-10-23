using WriterApp.Shared.Collaboration;

namespace WriterApp.Data.Collaboration;

public sealed class CollaborationMemberRecord
{
    public Guid DocumentId { get; set; }
    public string UserId { get; set; } = "";
    public CollaborationRole Role { get; set; }
    public long Revision { get; set; } = 1;
    public bool Revoked { get; set; }
}
public sealed class CollaborationInvitationRecord
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public CollaborationRole Role { get; set; }
    public string? RecipientUserId { get; set; }
    public string? RecipientEmail { get; set; }
    public string TokenSha256 { get; set; } = "";
    public string RequestSha256 { get; set; } = "";
    public long ExpiresAtUnixMilliseconds { get; set; }
    public long Revision { get; set; } = 1;
    public bool Revoked { get; set; }
    public string? AcceptedBy { get; set; }
    public long? AcceptedAtUnixMilliseconds { get; set; }
}
public sealed class CollaborationSupportingRecord
{
    public Guid ManuscriptId { get; set; }
    public Guid SupportingDocumentId { get; set; }
    public long Revision { get; set; } = 1;
    public bool Revoked { get; set; }
}
