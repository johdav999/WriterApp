namespace WriterApp.Data.Collaboration;

public sealed class CollaborationRecoveryStateRecord
{
    public string Id { get; set; } = "database";
    public Guid OperationId { get; set; }
    public string RequiredEnvironment { get; set; } = "";
    public long SealedAtUnixMilliseconds { get; set; }
}
