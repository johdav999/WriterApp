namespace WriterApp.Data.Collaboration;

public sealed class CollaborationProjectionRecord
{
    public Guid PageId { get; set; }
    public Guid Generation { get; set; }
    public long Sequence { get; set; }
    public string Html { get; set; } = "";
    public string Json { get; set; } = "";
    public string Text { get; set; } = "";
    public string StateSha256 { get; set; } = "";
}
public sealed class CollaborationManifestRecord
{
    public Guid DocumentId { get; set; }
    public long StructureRevision { get; set; } = 1;
}
public sealed class CollaborationCommandRecord
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public string RequestSha256 { get; set; } = "";
    public long StructureRevision { get; set; }
}

public sealed class CollaborationPageRecord
{
    public Guid PageId { get; set; }
    public Guid Generation { get; set; }
    public Guid DocumentId { get; set; }
    public int ContentContractVersion { get; set; }
    public int ProtocolVersion { get; set; }
    public string SchemaFingerprint { get; set; } = "";
    public long LeaseEpoch { get; set; }
    public long LeaseExpiresAtUnixMilliseconds { get; set; }
    public string? LeaseOwner { get; set; }
    public long CommittedSequence { get; set; }
    public long SnapshotSequence { get; set; }
    public long ProjectionSequence { get; set; }
    public bool IsDeleted { get; set; }
}

public sealed class CollaborationUpdateRecord
{
    public Guid PageId { get; set; }
    public Guid Generation { get; set; }
    public long Sequence { get; set; }
    public Guid InstallationId { get; set; }
    public Guid UpdateId { get; set; }
    public string AuthorUserId { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public byte[] Update { get; set; } = [];
    public long CommittedAtUnixMilliseconds { get; set; }
}

// Receipts deliberately outlive compacted update rows.
public sealed class CollaborationReceiptRecord
{
    public Guid PageId { get; set; }
    public Guid Generation { get; set; }
    public Guid InstallationId { get; set; }
    public Guid UpdateId { get; set; }
    public string Sha256 { get; set; } = "";
    public long CommitSequence { get; set; }
    public long CommittedAtUnixMilliseconds { get; set; }
}

public sealed class CollaborationSnapshotRecord
{
    public Guid PageId { get; set; }
    public Guid Generation { get; set; }
    public long Sequence { get; set; }
    public byte[] State { get; set; } = [];
    public string Sha256 { get; set; } = "";
    public long CreatedAtUnixMilliseconds { get; set; }
}

public sealed class CollaborationOutboxEventRecord
{
    public Guid Id { get; set; }
    public Guid PageId { get; set; }
    public Guid Generation { get; set; }
    public long CommitSequence { get; set; }
    public long CreatedAtUnixMilliseconds { get; set; }
    public long? CompletedAtUnixMilliseconds { get; set; }
    public int Attempts { get; set; }
}

public sealed class CollaborationMigrationJobRecord
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public string SourceVersion { get; set; } = "";
    public byte[] RecoverySnapshot { get; set; } = [];
    public string SourceSha256 { get; set; } = "";
    public long CreatedAtUnixMilliseconds { get; set; }
    public long? CompletedAtUnixMilliseconds { get; set; }
    public string? FailureCode { get; set; }
}
