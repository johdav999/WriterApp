namespace WriterApp.Data.AI;

public sealed class BrainstormingSessionRecord
{
    public Guid Id { get; set; }
    public string OwnerUserId { get; set; } = "";
    public string BackendKey { get; set; } = "";
    public Guid DocumentId { get; set; }
    public Guid? ProjectId { get; set; }
    public long Revision { get; set; }
    public bool IsDeleted { get; set; }
    public long UpdatedUnixMilliseconds { get; set; }
    public string SnapshotJson { get; set; } = "";
    public string? RecoveryJson { get; set; }
}
public sealed class BrainstormingSessionOperationRecord
{
    public Guid SessionId { get; set; }
    public Guid OperationId { get; set; }
    public string PayloadHash { get; set; } = "";
    public string ReceiptJson { get; set; } = "";
}
