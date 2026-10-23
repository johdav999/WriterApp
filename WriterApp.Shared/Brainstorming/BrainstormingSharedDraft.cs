namespace WriterApp.Shared.Brainstorming;

public sealed record BrainstormingSharedDraft(Guid Id, Guid DocumentId, string PlainText, DateTimeOffset CreatedAt, bool IsMine);
