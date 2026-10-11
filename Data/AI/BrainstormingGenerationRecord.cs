namespace WriterApp.Data.AI;

public sealed class BrainstormingGenerationRecord
{
    public Guid SessionId { get; set; }
    public Guid OperationId { get; set; }
    public string PayloadHash { get; set; } = "";
    public long Revision { get; set; }
    public string SnapshotJson { get; set; } = "";
}
