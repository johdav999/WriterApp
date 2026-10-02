using System.Text.Json.Serialization;

namespace WriterApp.Shared.Sync;

public sealed record SyncPage(Guid Id, string Title, int OrderIndex, string Content, string ContentFormat = "html");
public sealed record SyncSection(Guid Id, string Title, int OrderIndex, string? NarrativePurpose, string? LanguageCode, IReadOnlyList<SyncPage> Pages);
public sealed record SyncDocument(Guid Id, Guid ProjectId, string Title, string? LanguageCode, string Kind, bool IsArchived,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, IReadOnlyList<SyncSection> Sections,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] SyncProject? Project = null);
public sealed record SyncChange(Guid DocumentId, string Version, bool IsDeleted, bool IsTrashed);
public sealed record SyncChanges(IReadOnlyList<SyncChange> Changes, string Cursor, bool HasMore);
public sealed record SyncSnapshot(SyncChange State, SyncDocument? Document);
public sealed record SyncStructureChange(bool AllowPageMoves, IReadOnlyList<Guid> RemovedSectionIds, IReadOnlyList<Guid> RemovedPageIds);
public sealed record SyncUpload(string Title, string? LanguageCode, IReadOnlyList<SyncSection> Sections,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] SyncStructureChange? Structure = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] SyncProject? Project = null,
    string Kind = "manuscript");
public sealed record SyncMutation(Guid OperationId, string? ExpectedVersion, string Action, SyncUpload? Document = null, string? Title = null);
public sealed record SyncMutationResult(Guid OperationId, SyncChange State, long? ProjectMetadataRevision = null, Guid? PrimaryDocumentId = null);
public sealed record SyncError(string Code, string Message, SyncChange? Current = null);
