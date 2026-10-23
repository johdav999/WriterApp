namespace WriterApp.Shared.Collaboration;

public sealed record CollaborationAnchor(string Start, string End);
public sealed record CollaborationAnchorState(bool Detached);
public sealed record CollaborationCommentRead(Guid Generation);
public sealed record CollaborationCreateComment(Guid OperationId, Guid Generation, long MembershipRevision,
    long ExpectedSequence, CollaborationAnchor Anchor, string Body);
public sealed record CollaborationCommentMutation(Guid OperationId, Guid Generation, long MembershipRevision,
    long ExpectedRevision, string Action, string? Body = null);
public sealed record CollaborationCommentMessage(Guid Id, string AuthorId, string Body, long CreatedAtUnixMilliseconds);
public sealed record CollaborationCommentThread(Guid Id, Guid PageId, Guid Generation, string AuthorId, long Revision,
    bool Resolved, bool Detached, CollaborationAnchor? Anchor, CollaborationCommentMessage[] Messages);
public sealed record CollaborationCommentSnapshot(Guid PageId, Guid Generation, long Sequence, CollaborationCommentThread[] Threads);
