using System.Text.Json;
using System.Text.Json.Serialization;

namespace WriterApp.Device.Shared.Storage;

public enum LocalContentFormat { Html, LegacyJson, LegacyText }
public enum LocalSyncState { LocalOnly, PendingUpload, Synced, Conflict, Error }
public enum LocalDocumentScope { Active, Trash, All }

// IDs are device identities; server IDs are separate and assigned only by synchronization.
public sealed record LocalDocument
{
    public required Guid DocumentId { get; init; }
    public Guid? ServerDocumentId { get; init; }
    public Guid? ServerProjectId { get; init; }
    public LocalProject? Project { get; init; }
    public required string Title { get; init; }
    public string Kind { get; init; } = "manuscript";
    public string? LanguageCode { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
    public required DateTimeOffset UpdatedAtUtc { get; init; }
    public DateTimeOffset? DeletedAtUtc { get; init; }
    public required long LocalRevision { get; init; }
    public LocalSyncState SyncState { get; init; } = LocalSyncState.LocalOnly;
    public string? ServerVersion { get; init; }
    public DateTimeOffset? LastSyncedAtUtc { get; init; }
    public required IReadOnlyList<LocalSection> Sections { get; init; }
    public IReadOnlyList<LocalDeletedSection> DeletedSections { get; init; } = [];
    public IReadOnlyList<LocalDeletedPage> DeletedPages { get; init; } = [];
    [JsonExtensionData] public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

public sealed record LocalDeletedSection(LocalSection Section, DateTimeOffset DeletedAtUtc);
public sealed record LocalDeletedPage(Guid SectionId, LocalPage Page, DateTimeOffset DeletedAtUtc);

public sealed record LocalSection
{
    public required Guid SectionId { get; init; }
    public Guid? ServerSectionId { get; init; }
    public required string Title { get; init; }
    public string? NarrativePurpose { get; init; }
    public string? LanguageCode { get; init; }
    public required int OrderIndex { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
    public required DateTimeOffset UpdatedAtUtc { get; init; }
    public required IReadOnlyList<LocalPage> Pages { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

public sealed record LocalPage
{
    public required Guid PageId { get; init; }
    public Guid? ServerPageId { get; init; }
    public required string Title { get; init; }
    public required int OrderIndex { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
    public required DateTimeOffset UpdatedAtUtc { get; init; }
    public required LocalContentFormat ContentFormat { get; init; }
    public required string Content { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

public enum LocalDocumentIssueKind { Corrupt, UnsupportedVersion, Unavailable }
public sealed record LocalDocumentIssue(string FileName, LocalDocumentIssueKind Kind, string Message);
public sealed record LocalDocumentList(IReadOnlyList<LocalDocument> Documents, IReadOnlyList<LocalDocumentIssue> Issues);
public sealed record LocalSearchDocuments(IReadOnlyList<LocalDocument> Documents, IReadOnlyList<LocalDocumentIssue> Issues, bool Limited);

public sealed class LocalDocumentReadException(LocalDocumentIssue issue, Exception? inner = null)
    : IOException(issue.Message, inner)
{
    public LocalDocumentIssue Issue { get; } = issue;
}

public sealed class LocalDocumentConflictException(Guid documentId)
    : InvalidOperationException($"Document {documentId} has changed. Reload it before saving.");
