namespace WriterApp.Shared.Collaboration;

public sealed record CollaborationJournalSupport(int ContentContractVersion, int ProtocolVersion, string SchemaFingerprint);
public sealed record CollaborationJournalScope(string Backend, string OwnerId, string Environment, Guid DocumentId,
    Guid PageId, Guid Generation, CollaborationJournalSupport Support, long MembershipRevision = 0);
public sealed record CollaborationPendingEnvelope(string Type, int ProtocolVersion, Guid DocumentId, Guid PageId,
    Guid Generation, Guid InstallationId, Guid UpdateId, string Update, string Sha256);
public sealed record CollaborationJournalPage(int Version, CollaborationJournalScope Scope, Guid InstallationId,
    long Revision, string State, string StateSha256, List<CollaborationPendingEnvelope> Pending,
    List<CollaborationDurableReceipt> Receipts, string? RecoveryReason = null);
public sealed record CollaborationJournalMutation(long Revision, string State, string StateSha256, CollaborationPendingEnvelope? Envelope = null);
