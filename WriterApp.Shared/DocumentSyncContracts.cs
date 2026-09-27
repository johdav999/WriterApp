namespace WriterApp.Shared.Sync;

public sealed record SyncPage(Guid Id, string Title, int OrderIndex, string Content, string ContentFormat = "html");
public sealed record SyncSection(Guid Id, string Title, int OrderIndex, string? NarrativePurpose, string? LanguageCode, IReadOnlyList<SyncPage> Pages);
public sealed record SyncDocument(Guid Id, Guid ProjectId, string Title, string? LanguageCode, string Kind, bool IsArchived,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, IReadOnlyList<SyncSection> Sections);
public sealed record SyncChange(Guid DocumentId, string Version, bool IsDeleted, bool IsTrashed);
public sealed record SyncChanges(IReadOnlyList<SyncChange> Changes, string Cursor, bool HasMore);
public sealed record SyncSnapshot(SyncChange State, SyncDocument? Document);
public sealed record SyncUpload(string Title, string? LanguageCode, IReadOnlyList<SyncSection> Sections);
public sealed record SyncMutation(Guid OperationId, string? ExpectedVersion, string Action, SyncUpload? Document = null, string? Title = null);
public sealed record SyncMutationResult(Guid OperationId, SyncChange State);
public sealed record SyncError(string Code, string Message, SyncChange? Current = null);
