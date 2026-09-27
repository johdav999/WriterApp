namespace WriterApp.Data.Documents;

public sealed class DocumentSyncRecord
{
    public Guid DocumentId { get; set; }
    public string OwnerUserId { get; set; } = "";
    public long Sequence { get; set; }
    public string Version { get; set; } = "";
    public bool IsDeleted { get; set; }
    public bool IsTrashed { get; set; }
}

public sealed class DocumentSyncClock
{
    public int Id { get; set; }
    public long Sequence { get; set; }
}

public sealed class DocumentSyncOperation
{
    public string OwnerUserId { get; set; } = "";
    public Guid OperationId { get; set; }
    public string RequestHash { get; set; } = "";
    public string ResultJson { get; set; } = "";
}
