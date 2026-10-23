namespace WriterApp.Data.Collaboration;

// Tickets are never stored. Version is a compare-and-swap fence for refresh and revocation.
public sealed class CollaborationSessionRecord
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public Guid PageId { get; set; }
    public Guid Generation { get; set; }
    public Guid InstallationId { get; set; }
    // Historical column name: this is the authenticated actor, including an invited editor/viewer.
    public string OwnerUserId { get; set; } = "";
    public string ResourceOwnerUserId { get; set; } = "";
    public long MembershipRevision { get; set; }
    public string Environment { get; set; } = "";
    public string SchemaFingerprint { get; set; } = "";
    public int ProtocolVersion { get; set; }
    public int ContentContractVersion { get; set; }
    public long Version { get; set; }
    public long ExpiresAtUnixMilliseconds { get; set; }
    public bool Revoked { get; set; }
    public bool CommitAllowed { get; set; }
    public Guid? ConnectionId { get; set; }
}
