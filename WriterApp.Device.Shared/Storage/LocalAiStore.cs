using System.Text.Json;
using System.Text.Json.Serialization;
using WriterApp.Device.Shared.Services;

namespace WriterApp.Device.Shared.Storage;

public sealed record LocalAiPrompt(int Version, Guid Id, string Name, string Instruction, DateTimeOffset CreatedAt);
public sealed record LocalAiHistory(int Version, Guid Id, Guid DocumentId, string Action, string Target,
    long SourceRevision, string? ServerVersion, DateTimeOffset CreatedAt, string Status,
    LocalDocument Before, string Proposed, Guid? PageId = null, string? AfterHtml = null, string? OriginalText = null,
    Guid? NodeId = null, LocalDocument? After = null);

/// <summary>Local authored prompts and recovery evidence. Never stores credentials or request diagnostics.</summary>
public sealed class LocalAiStore(string root)
{
    private static readonly JsonSerializerOptions Json = new() { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    private readonly AtomicDocumentWriter _writer = new();
    private string HistoryPath(Guid document, Guid id) => Path.Combine(root, "history", document.ToString("N"), id.ToString("N") + ".json");
    public async Task<IReadOnlyList<LocalAiPrompt>> PromptsAsync(CancellationToken ct = default)
    {
        string dir = Path.Combine(root, "prompts"); if (!Directory.Exists(dir)) return [];
        var result = new List<LocalAiPrompt>();
        foreach (var path in Directory.EnumerateFiles(dir, "*.json"))
        {
            var p = await Read<LocalAiPrompt>(path, ct); Validate(p); result.Add(p);
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
        if (p.Version != 1 || p.Id == Guid.Empty || string.IsNullOrWhiteSpace(p.Name) || p.Name.Length > 100
            || string.IsNullOrWhiteSpace(p.Instruction) || p.Instruction.Length > 2000) throw new InvalidDataException("Invalid or unsupported local prompt. Existing files are preserved.");
    }
    public async Task<IReadOnlyList<LocalAiHistory>> HistoryAsync(Guid document, CancellationToken ct = default)
    {
        string dir = Path.Combine(root, "history", document.ToString("N")); if (!Directory.Exists(dir)) return [];
        var records = new List<LocalAiHistory>();
        foreach (var path in Directory.EnumerateFiles(dir, "*.json"))
        {
            var value = await Read<LocalAiHistory>(path, ct); Validate(value);
            if (value.DocumentId != document) throw new InvalidDataException("AI history document mismatch."); records.Add(value);
        }
        return records.OrderByDescending(x => x.CreatedAt).ToArray();
    }
    public async Task SaveHistoryAsync(LocalAiHistory value, CancellationToken ct = default)
    {
        Validate(value);
        string path = HistoryPath(value.DocumentId, value.Id);
        if (File.Exists(path))
        {
            var previous = await Read<LocalAiHistory>(path, ct); Validate(previous);
            if (previous.DocumentId != value.DocumentId || previous.Id != value.Id || previous.Proposed != value.Proposed
                || previous.Action != value.Action || previous.Target != value.Target || previous.PageId != value.PageId
                || previous.NodeId != value.NodeId || previous.OriginalText != value.OriginalText
                || (previous.After is not null && (value.After is null || !LocalDocumentCodec.Encode(previous.After).SequenceEqual(LocalDocumentCodec.Encode(value.After))))
                || (previous.AfterHtml is not null && previous.AfterHtml != value.AfterHtml)
                || !LocalDocumentCodec.Encode(previous.Before).SequenceEqual(LocalDocumentCodec.Encode(value.Before)))
                throw new InvalidDataException("AI history source cannot be replaced.");
        }
        await Write(path, value, ct);
    }
    private static void Validate(LocalAiHistory value)
    {
        if (value.Version != 1 || value.Id == Guid.Empty || value.Before is null || value.DocumentId != value.Before.DocumentId
            || value.Proposed is null || value.Proposed.Length > 100000 || value.Status is not ("Reviewed" or "Applying" or "Applied" or "Undoing" or "Undone" or "Redoing"))
            throw new InvalidDataException("Invalid or unsupported AI history. Existing evidence is preserved.");
        LocalDocumentCodec.Validate(value.Before);
        if (value.After is not null)
        {
            if (value.After.DocumentId != value.DocumentId) throw new InvalidDataException("AI history result document mismatch.");
            LocalDocumentCodec.Validate(value.After);
        }
    }
    private static async Task<T> Read<T>(string path, CancellationToken ct)
    {
        if (new FileInfo(path).Length > 32 * 1024 * 1024) throw new InvalidDataException("AI file exceeds the safe size limit.");
        return JsonSerializer.Deserialize<T>(await File.ReadAllBytesAsync(path, ct), Json) ?? throw new InvalidDataException("Invalid AI file.");
    }
    private async Task Write<T>(string path, T value, CancellationToken ct)
    {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(value, Json);
        if (bytes.Length > 32 * 1024 * 1024) throw new InvalidDataException("AI file exceeds the safe size limit.");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!); await _writer.WriteAsync(path, bytes, ct);
    }
}
