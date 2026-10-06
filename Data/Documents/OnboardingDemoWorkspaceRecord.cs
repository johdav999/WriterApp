namespace WriterApp.Data.Documents;

// Owner-scoped immutable sample identities survive deletion and retries. Authored projects are never inferred by title.
public sealed class OnboardingDemoWorkspaceRecord
{
    public string OwnerUserId { get; set; } = "";
    public int Version { get; set; } = 1;
    public Guid OperationId { get; set; }
    public string Intent { get; set; } = "";
    public Guid ProjectId { get; set; }
    public Guid DocumentId { get; set; }
    public Guid SectionId { get; set; }
    public Guid SceneNodeId { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public bool RequestUsed { get; set; }
    public Guid? ProposalId { get; set; }
}

public sealed class OnboardingProgressOperationRecord
{
    public string OwnerUserId { get; set; } = "";
    public Guid OperationId { get; set; }
    public string RequestHash { get; set; } = "";
    public string ReceiptJson { get; set; } = "";
}
