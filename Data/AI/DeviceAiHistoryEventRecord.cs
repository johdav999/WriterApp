namespace WriterApp.Data.AI;

// Reports local outcomes; deliberately separate from legacy cloud undo snapshots.
public sealed class DeviceAiHistoryEventRecord
{
    public string OwnerUserId { get; set; } = "";
    public Guid OperationId { get; set; }
    public Guid ProposalId { get; set; }
    public Guid DocumentId { get; set; }
    public Guid LocalEntryId { get; set; }
    public int Sequence { get; set; }
    public string State { get; set; } = "";
    public DateTimeOffset ReceivedAt { get; set; }
    public string RequestHash { get; set; } = "";
    public string ReportJson { get; set; } = "";
}
