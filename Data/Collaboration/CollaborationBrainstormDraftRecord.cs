namespace WriterApp.Data.Collaboration;

// A publication contains validated generated prose only. Private conversation
// snapshots, instructions, composer text and provider inspection never enter it.
public sealed class CollaborationBrainstormDraftRecord
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public string BackendKey { get; set; } = "";
    public string ResourceOwnerId { get; set; } = "";
    public string AuthorId { get; set; } = "";
    public long CreatedUnixMilliseconds { get; set; }
    public string PlainText { get; set; } = "";
}
