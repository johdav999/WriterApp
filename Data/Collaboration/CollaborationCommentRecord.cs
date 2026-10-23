namespace WriterApp.Data.Collaboration;

public sealed class CollaborationCommentRecord
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public Guid PageId { get; set; }
    public Guid Generation { get; set; }
    public string AuthorId { get; set; } = "";
    public string? Start { get; set; }
    public string? End { get; set; }
    public string Body { get; set; } = "";
    public bool Resolved { get; set; }
    public long Revision { get; set; } = 1;
    public long CreatedAtUnixMilliseconds { get; set; }
    public string? LegacySource { get; set; }
}
public sealed class CollaborationCommentMessageRecord
{
    public Guid Id { get; set; }
    public Guid ThreadId { get; set; }
    public string AuthorId { get; set; } = "";
    public string Body { get; set; } = "";
    public long CreatedAtUnixMilliseconds { get; set; }
}
public sealed class CollaborationCommentOperationRecord
{
    public Guid Id { get; set; }
    public Guid ThreadId { get; set; }
    public string ActorId { get; set; } = "";
    public string RequestSha256 { get; set; } = "";
    public long ResultRevision { get; set; }
}
