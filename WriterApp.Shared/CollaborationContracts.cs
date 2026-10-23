namespace WriterApp.Shared.Collaboration;

// Numeric values are persisted. Missing state on existing documents is Legacy.
public enum CollaborationMode { Legacy = 0, Migrating = 1, Collaborative = 2 }
public enum CollaborationMigrationState { None = 0, Preparing = 1, Converting = 2, Failed = 3, Completed = 4 }

public static class CollaborationErrors
{
    public const string MigrationInProgress = "collaboration_migration_in_progress";
    public const string LegacyWriteBlocked = "collaboration_legacy_write_blocked";
    public const string UpgradeRequired = "collaboration_upgrade_required";
    public const string GenerationChanged = "collaboration_generation_changed";
}

public sealed record CollaborationError(string Code, string Message, bool PreserveLocalDraft = true);
public sealed record CollaborationPageCapability(Guid PageId, Guid? Generation, int ContentContractVersion,
    int ProtocolVersion, string? SchemaFingerprint, long CommittedSequence, long ProjectionSequence);
public sealed record CollaborationCapabilities(Guid DocumentId, CollaborationMode Mode,
    CollaborationMigrationState MigrationState, bool FeatureAvailable, bool CanRead, bool CanWriteLegacy,
    bool CanWriteCollaboratively, string EffectiveAccess, string[] AllowedOperations,
    CollaborationError? BlockedReason, IReadOnlyList<CollaborationPageCapability> Pages, long MembershipRevision = 0, bool InteractionsAvailable = false, string? Environment = null);

// Immutable wire identities and durable receipts. These records do not grant authorization.
public sealed record CollaborationUpdateEnvelope(int ProtocolVersion, Guid DocumentId, Guid PageId,
    Guid Generation, Guid InstallationId, Guid UpdateId, byte[] Update, string Sha256);
public sealed record CollaborationDurableReceipt(Guid PageId, Guid Generation, Guid InstallationId,
    Guid UpdateId, string Sha256, long CommitSequence, long CommittedAtUnixMilliseconds);
public sealed record CollaborationWatermark(Guid PageId, Guid Generation, long Sequence);
public sealed record CollaborationReadRequest(long? StructureRevision = null, CollaborationWatermark[]? Required = null, Guid[]? SupportingDocumentIds = null);
