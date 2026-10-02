namespace WriterApp.Shared.Sync;

// Version 2 sync transports one project/manuscript transaction, never separate parent/child requests.
public sealed record SyncProject(Guid Id, string Title, IReadOnlyList<SyncProjectNode> Nodes,
    string? Subtitle = null, string? AuthorName = null, string? Language = null, string? Genre = null,
    string? DefaultExportSettingsJson = null, string? CoverImageUrl = null, int Version = 1, SyncSynopsis? Synopsis = null,
    Guid? PrimaryDocumentId = null, long MetadataRevision = 0);
public sealed record SyncProjectNode(Guid Id, Guid? ParentId, string NodeType, string Title, int OrderIndex,
    Guid? SectionId, Guid? DeletionId = null, string? MetadataJson = null, string? Notes = null, SyncSceneCard? Card = null, IReadOnlyList<SyncSceneAnnotation>? Annotations = null);
public sealed record SyncSceneCard(string? NarrativePurpose, string? NarrativeRole, string? NarrativeIntent,
    string? EmotionalBeat, string? KeyEvents, string? OpenQuestions, string? Summary, string? Status,
    string? PovCharacterId, string? PlaceId, string? TimelineEventId, string? TimeRef,
    string? TagsJson, string? SubplotTagsJson, string? ReferencesJson);

public sealed record SyncSynopsis(string Logline = "", string Premise = "", string Theme = "", string ProtagonistArc = "",
    string CentralConflict = "", string Stakes = "", string Setting = "", string EndingIntent = "", string OpenQuestions = "", string Notes = "");
public sealed record SyncSceneAnnotation(Guid Id, string Kind, string Status, int AnchorFrom, int AnchorTo,
    string AnchorText, string Content, string AuthorUserId, DateTimeOffset CreatedAt, DateTimeOffset? ResolvedAt = null, bool AnchorDetached = false);
