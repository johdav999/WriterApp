namespace WriterApp.Data.Documents;

// A durable approval journal and immutable receipt. Never used as page-HTML Undo data.
public sealed class WebTranslationOperation
{
    public string OwnerUserId { get; set; } = "";
    public Guid OperationId { get; set; }
    public Guid ProposalId { get; set; }
    public Guid DocumentId { get; set; }
    public string RequestHash { get; set; } = "";
    public string ApprovalJson { get; set; } = "";
    public string RecoveryJson { get; set; } = "";
    public string ReceiptJson { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}
