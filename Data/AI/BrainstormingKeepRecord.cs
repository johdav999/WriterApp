namespace WriterApp.Data.AI;

public sealed class BrainstormingKeepRecord
{
    public Guid SessionId { get; set; }
    public Guid OperationId { get; set; }
    public Guid CandidateId { get; set; }
    public string ReviewJson { get; set; } = "";
    public string ReceiptJson { get; set; } = "";
    public string MovesJson { get; set; } = "[]";
}
