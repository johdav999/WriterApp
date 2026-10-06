namespace WriterApp.Data.AI;

public sealed class WebAiHistoryOperationRecord
{
    public string OwnerUserId { get; set; } = "";
    public Guid OperationId { get; set; }
    public Guid ApplicationId { get; set; }
    public Guid ProposalId { get; set; }
    public Guid DocumentId { get; set; }
    public int Sequence { get; set; }
    public string Outcome { get; set; } = "";
    public string RequestHash { get; set; } = "";
    public string IntentJson { get; set; } = "";
    public string? SavedResponseJson { get; set; }
    public string? RecoveryJson { get; set; }
    public DateTimeOffset? CommittedAt { get; set; }
    public DateTimeOffset? ReportedAt { get; set; }
}
