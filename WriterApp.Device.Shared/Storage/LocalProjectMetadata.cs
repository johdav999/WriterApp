namespace WriterApp.Device.Shared.Storage;

public sealed record LocalProjectMetadata
{
    public int Version { get; init; } = 1;
    public required Guid ProjectId { get; init; }
    public Guid? ServerProjectId { get; init; }
    public required Guid PrimaryDocumentId { get; init; }
    public required string Title { get; init; }
    public string? Subtitle { get; init; }
    public string? AuthorName { get; init; }
    public string? Language { get; init; }
    public string? Genre { get; init; }
    public string? DefaultExportSettingsJson { get; init; }
    public string? CoverImageUrl { get; init; }
    public long MetadataRevision { get; init; }
    public long? ServerMetadataRevision { get; init; }
    public Guid? ServerPrimaryDocumentId { get; init; }
    public bool MetadataDirty { get; init; }

    public static LocalProjectMetadata From(LocalProject p) => new()
    {
        ProjectId = p.ProjectId, ServerProjectId = p.ServerProjectId,
        PrimaryDocumentId = p.PrimaryDocumentId ?? p.ManuscriptId, Title = p.Title,
        Subtitle = p.Subtitle, AuthorName = p.AuthorName, Language = p.Language, Genre = p.Genre,
        DefaultExportSettingsJson = p.DefaultExportSettingsJson, CoverImageUrl = p.CoverImageUrl,
        MetadataRevision = p.MetadataRevision, ServerMetadataRevision = p.ServerMetadataRevision,
        ServerPrimaryDocumentId = p.ServerPrimaryDocumentId, MetadataDirty = p.MetadataDirty
    };

    public LocalProject Apply(LocalProject p) => p with
    {
        ProjectId = ProjectId, ServerProjectId = ServerProjectId, PrimaryDocumentId = PrimaryDocumentId,
        Title = Title, Subtitle = Subtitle, AuthorName = AuthorName, Language = Language, Genre = Genre,
        DefaultExportSettingsJson = DefaultExportSettingsJson, CoverImageUrl = CoverImageUrl,
        MetadataRevision = MetadataRevision, ServerMetadataRevision = ServerMetadataRevision,
        ServerPrimaryDocumentId = ServerPrimaryDocumentId, MetadataDirty = MetadataDirty
    };
}
