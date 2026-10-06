using System.Text.Json;
using System.Text.Json.Serialization;
using WriterApp.Device.Shared.Services;

namespace WriterApp.Device.Shared.Storage;

public sealed record LocalAiPrompt(int Version, Guid Id, string Name, string Instruction, DateTimeOffset CreatedAt,
    string? Category = null, string Kind = "custom", string? BuiltinActionId = null, Dictionary<string,object?>? Parameters = null,
    WriterApp.Shared.WritingScope Scope = WriterApp.Shared.WritingScope.Section, bool Pinned = false, long Revision = 1,
    DateTimeOffset? UpdatedAt = null, DateTimeOffset? DeletedAt = null, string? OriginScope = null,
    Guid? OriginCloudId = null, string? OriginVersion = null, Guid? ProjectId = null);
public sealed record LocalAiHistory(int Version, Guid Id, Guid DocumentId, string Action, string Target,
    long SourceRevision, string? ServerVersion, DateTimeOffset CreatedAt, string Status,
    LocalDocument Before, string Proposed, Guid? PageId = null, string? AfterHtml = null, string? OriginalText = null,
    Guid? NodeId = null, LocalDocument? After = null, Guid? SectionId = null,
    IReadOnlyList<WriterApp.Application.Documents.SceneCoachingField>? SceneFields = null,
    IReadOnlyList<WriterApp.Application.Documents.SceneEntity>? SceneEntities = null,
    string? SynopsisCommentary = null, string? SynopsisUserNotes = null, WriterApp.Shared.PromptDefinition? Preset = null,
    WriterApp.Shared.AiHistoryOrigin? CloudOrigin = null, IReadOnlyList<WriterApp.Shared.AiHistoryDelivery>? Deliveries = null, string? RecommendationId = null);

/// <summary>Local authored prompts and recovery evidence. Never stores credentials or request diagnostics.</summary>
public sealed partial class LocalAiStore(string root)
{
    public event Action<Guid>? HistoryChanged;
    private static readonly JsonSerializerOptions Json = new() { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    private readonly AtomicDocumentWriter _writer = new();
    private string HistoryPath(Guid document, Guid id) => Path.Combine(root, "history", document.ToString("N"), id.ToString("N") + ".json");
    public async Task<IReadOnlyList<LocalAiPrompt>> PromptsAsync(CancellationToken ct = default)
    {
        string dir = Path.Combine(root, "prompts"); if (!Directory.Exists(dir)) return [];
        var result = new List<LocalAiPrompt>();
        foreach (var path in Directory.EnumerateFiles(dir, "*.json"))
        {
            var p = await Read<LocalAiPrompt>(path, ct); Validate(p); if(p.DeletedAt is null && p.OriginScope is null)result.Add(p);
        }
        return result.OrderBy(p => p.Name).ToArray();
    }
    public async Task<LocalAiPrompt> SavePromptAsync(string name, string instruction, CancellationToken ct = default)
    {
        var prompt = new LocalAiPrompt(1, Guid.NewGuid(), name.Trim(), instruction.Trim(), DateTimeOffset.UtcNow); Validate(prompt);
        await Write(Path.Combine(root, "prompts", prompt.Id.ToString("N") + ".json"), prompt, ct); return prompt;
    }
    private static void Validate(LocalAiPrompt p)
    {
        if (p.Version is not (1 or 2) || p.Id == Guid.Empty || string.IsNullOrWhiteSpace(p.Name) || p.Name.Length > 100
            || p.Version==1 && (string.IsNullOrWhiteSpace(p.Instruction) || p.Instruction.Length > 2000)) throw new InvalidDataException("Invalid or unsupported local prompt. Existing files are preserved.");
        if(p.Version==2) {
            WriterApp.Shared.ReusablePrompts.ValidateEnvelope(Definition(p));
            if(p.Revision<1||p.CreatedAt==default||p.UpdatedAt<p.CreatedAt||p.DeletedAt<p.CreatedAt
                ||p.OriginScope is null&&(p.OriginCloudId is not null||p.OriginVersion is not null)
                ||p.OriginScope is not null&&(p.OriginScope.Length!=64||p.OriginScope.Any(c=>!Uri.IsHexDigit(c))||p.OriginCloudId is null||p.OriginCloudId==Guid.Empty||string.IsNullOrWhiteSpace(p.OriginVersion)||p.OriginVersion.Length>200))
                throw new InvalidDataException("Invalid preset revision or origin. Existing files are preserved.");
        }
    }
    public async Task<IReadOnlyList<LocalAiHistory>> HistoryAsync(Guid document, CancellationToken ct = default)
    {
        string dir = Path.Combine(root, "history", document.ToString("N")); if (!Directory.Exists(dir)) return [];
        var records = new List<LocalAiHistory>();
        foreach (var path in Directory.EnumerateFiles(dir, "*.json"))
        {
            var value = await Read<LocalAiHistory>(path, ct); Validate(value);
            if (value.DocumentId != document || Path.GetFileNameWithoutExtension(path) != value.Id.ToString("N")) throw new InvalidDataException("AI history document or file identity mismatch."); records.Add(value);
        }
        return records.OrderByDescending(x => x.CreatedAt).ToArray();
    }
    public async Task SaveHistoryAsync(LocalAiHistory value, CancellationToken ct = default) {
        await PresetWrite(async () => { await SaveHistoryCoreAsync(value, ct); return true; }, ct);
        if (value.CloudOrigin is not null && value.Status is "Reviewed" or "Applied" or "Undone")
            foreach (Action<Guid> notify in HistoryChanged?.GetInvocationList() ?? []) try { notify(value.DocumentId); } catch { /* Reporting cannot undo a durable local save. */ }
    }
    private async Task SaveHistoryCoreAsync(LocalAiHistory value, CancellationToken ct)
    {
        Validate(value);
        string path = HistoryPath(value.DocumentId, value.Id);
        LocalAiHistory? previous = null;
        if (File.Exists(path))
        {
            previous = await Read<LocalAiHistory>(path, ct); Validate(previous);
            if (previous.Version != value.Version || previous.DocumentId != value.DocumentId || previous.Id != value.Id || previous.Proposed != value.Proposed
                || previous.SourceRevision != value.SourceRevision || previous.ServerVersion != value.ServerVersion || previous.CreatedAt != value.CreatedAt
                || previous.Action != value.Action || previous.Target != value.Target || previous.PageId != value.PageId || previous.RecommendationId != value.RecommendationId
                || previous.NodeId != value.NodeId || previous.SectionId != value.SectionId || previous.OriginalText != value.OriginalText
                || previous.SceneFields is not null && (value.SceneFields is null || !previous.SceneFields.SequenceEqual(value.SceneFields))
                || JsonSerializer.Serialize(previous.SceneEntities) != JsonSerializer.Serialize(value.SceneEntities)
                || previous.SynopsisCommentary != value.SynopsisCommentary || previous.SynopsisUserNotes != value.SynopsisUserNotes
                || WriterApp.Shared.ReusablePrompts.Canonical(previous.Preset) != WriterApp.Shared.ReusablePrompts.Canonical(value.Preset)
                || WriterApp.Shared.DeviceAiHistoryContracts.Hash(previous.CloudOrigin ?? (object)"") != WriterApp.Shared.DeviceAiHistoryContracts.Hash(value.CloudOrigin ?? (object)"")
                || (previous.After is not null && (value.After is null || !LocalDocumentCodec.Encode(previous.After).SequenceEqual(LocalDocumentCodec.Encode(value.After))))
                || (previous.AfterHtml is not null && previous.AfterHtml != value.AfterHtml)
                || !LocalDocumentCodec.Encode(previous.Before).SequenceEqual(LocalDocumentCodec.Encode(value.Before)))
                throw new InvalidDataException("AI history source cannot be replaced.");
        }
        value = AddHistoryDelivery(value, previous);
        Validate(value);
        await Write(path, value, ct);
    }
    private static void Validate(LocalAiHistory value)
    {
        if (value.Version is not (1 or 2 or 3 or 4 or 5) || value.Version == 2 && (value.Target is not ("Translation:replace" or "Translation:duplicate-section" or "Translation:duplicate-document")
                || value.After is null || value.Action is not ("translate.section" or "translate.document"))
            || value.Id == Guid.Empty || value.Before is null || value.DocumentId != value.Before.DocumentId
            || value.Proposed is null || value.Proposed.Length > 100000 || value.Status is not ("Reviewed" or "Applying" or "Applied" or "Undoing" or "Undone" or "Redoing"))
            throw new InvalidDataException("Invalid or unsupported AI history. Existing evidence is preserved.");
        LocalDocumentCodec.Validate(value.Before);
        ValidateHistoryDelivery(value);
        if (value.Version == 3) LocalWriting.ValidateEntry(value);
        if(value.Version!=3&&value.Preset is not null)throw new InvalidDataException("Unsupported preset recovery metadata.");
        if(value.Version!=3&&value.RecommendationId is not null)throw new InvalidDataException("Unsupported recommendation recovery metadata.");
        if (value.Version == 4) LocalSceneCoaching.ValidateEntry(value);
        if (value.Version == 5) LocalSynopsisCoaching.ValidateEntry(value);
        if (value.Version != 5 && (value.SynopsisCommentary is not null || value.SynopsisUserNotes is not null)) throw new InvalidDataException("Unsupported synopsis coaching metadata.");
        if (value.Version != 4 && (value.SceneFields is not null || value.SceneEntities is not null)) throw new InvalidDataException("Unsupported scene approval metadata.");
        if (value.Version == 2 && (value.Target == "Translation:duplicate-section" && value.Action != "translate.section"
            || value.Target == "Translation:duplicate-document" && (value.Action != "translate.document"
                || value.After!.DocumentId == value.DocumentId || value.After.ServerDocumentId is not null || value.After.ServerProjectId is not null
                || value.After.ServerVersion is not null || value.After.SyncState != LocalSyncState.LocalOnly)
            || value.Target != "Translation:duplicate-document" && value.After!.Project?.ProjectId != value.Before.Project?.ProjectId))
            throw new InvalidDataException("Translation recovery identities do not match the approved target.");
        if (value.After is not null)
        {
            if (value.After.DocumentId != value.DocumentId && !(value.Version == 2 && value.Target == "Translation:duplicate-document")) throw new InvalidDataException("AI history result document mismatch.");
            LocalDocumentCodec.Validate(value.After);
        }
    }
    private static async Task<T> Read<T>(string path, CancellationToken ct)
    {
        if (new FileInfo(path).Length > 32 * 1024 * 1024) throw new InvalidDataException("AI file exceeds the safe size limit.");
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete,
            4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
        if (stream.Length > 32 * 1024 * 1024) throw new InvalidDataException("AI file exceeds the safe size limit.");
        return await JsonSerializer.DeserializeAsync<T>(stream, Json, ct) ?? throw new InvalidDataException("Invalid AI file.");
    }
    private async Task Write<T>(string path, T value, CancellationToken ct)
    {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(value, Json);
        if (bytes.Length > 32 * 1024 * 1024) throw new InvalidDataException("AI file exceeds the safe size limit.");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!); await _writer.WriteAsync(path, bytes, ct);
    }
}
