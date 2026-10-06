using System.Text.Json;
using System.Text.Json.Serialization;

namespace WriterApp.Device.Shared.Storage;

// A document-local projection: common metadata is hydrated from the independent project record.
// Nodes, synopsis and last page belong to this document and are saved with its writing.
public sealed record LocalProject
{
    public int Version { get; init; } = 1;
    public required Guid ProjectId { get; init; }
    public Guid? ServerProjectId { get; init; }
    public Guid? PrimaryDocumentId { get; init; }
    public long MetadataRevision { get; init; }
    public long? ServerMetadataRevision { get; init; }
    public Guid? ServerPrimaryDocumentId { get; init; }
    public bool MetadataDirty { get; init; }
    public required Guid ManuscriptId { get; init; }
    public required string Title { get; init; }
    public string? Subtitle { get; init; }
    public string? AuthorName { get; init; }
    public string? Language { get; init; }
    public string? Genre { get; init; }
    public string? DefaultExportSettingsJson { get; init; }
    public string? CoverImageUrl { get; init; }
    public string? PreviousCoverImageUrl { get; init; }
    public bool HasCoverRecovery { get; init; }
    public Guid? CoverChangeId { get; init; }
    public IReadOnlyList<LocalProjectNode> Nodes { get; init; } = [];
    public WriterApp.Shared.Sync.SyncSynopsis? Synopsis { get; init; }
    public Guid? LastPageId { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

public sealed record LocalProjectNode
{
    public required Guid NodeId { get; init; }
    public Guid? ServerNodeId { get; init; }
    public Guid? ParentId { get; init; }
    public required string NodeType { get; init; }
    public required string Title { get; init; }
    public int OrderIndex { get; init; }
    public Guid? SectionId { get; init; }
    // A subtree shares one deletion token, allowing restoration without resurrecting older trash.
    public Guid? DeletionId { get; init; }
    public string? MetadataJson { get; init; }
    public string? Notes { get; init; }
    public WriterApp.Shared.Sync.SyncSceneCard? Card { get; init; }
    public IReadOnlyList<LocalSceneAnnotation> Annotations { get; init; } = [];
    [JsonExtensionData] public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

public sealed record LocalSceneAnnotation(Guid LocalId, Guid? ServerId, WriterApp.Shared.Sync.SyncSceneAnnotation Value);
