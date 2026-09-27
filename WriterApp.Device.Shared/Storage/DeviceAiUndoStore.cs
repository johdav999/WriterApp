using System.Text.Json;

namespace WriterApp.Device.Shared.Storage;

public sealed record DeviceAiUndoRecord(Guid DocumentId, Guid PageId, string BeforeHtml,
    string? AfterHtml, DateTimeOffset CreatedAtUtc);

public sealed class DeviceAiUndoStore(string root)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly AtomicDocumentWriter _writer = new();
    private string PathFor(Guid id) => Path.Combine(root, $"{id:N}.json");
    private string PendingPathFor(Guid id) => Path.Combine(root, $"{id:N}.pending.json");

    public async Task<DeviceAiUndoRecord?> LoadAsync(Guid documentId, CancellationToken ct = default)
    {
        return await LoadFromAsync(PathFor(documentId), documentId, ct);
    }
    public Task<DeviceAiUndoRecord?> LoadPendingAsync(Guid documentId, CancellationToken ct = default) =>
        LoadFromAsync(PendingPathFor(documentId), documentId, ct);
    private static async Task<DeviceAiUndoRecord?> LoadFromAsync(string path, Guid documentId, CancellationToken ct)
    {
        if (!File.Exists(path)) return null;
        var record = JsonSerializer.Deserialize<DeviceAiUndoRecord>(await File.ReadAllBytesAsync(path, ct), Json);
        if (record?.DocumentId != documentId || record.PageId == Guid.Empty || record.BeforeHtml is null)
            throw new JsonException("The AI undo record is invalid. It was preserved.");
        return record;
    }
    public Task SaveBeforeAsync(DeviceAiUndoRecord record, CancellationToken ct = default) =>
        SaveAsync(record, PendingPathFor(record.DocumentId), ct);
    public async Task ConfirmAsync(DeviceAiUndoRecord record, CancellationToken ct = default)
    {
        if (record.AfterHtml is null) throw new ArgumentException("Applied content is required.", nameof(record));
        await SaveAsync(record, PathFor(record.DocumentId), ct);
        try { File.Delete(PendingPathFor(record.DocumentId)); }
        catch (IOException) { } // The committed backup is authoritative.
        catch (UnauthorizedAccessException) { }
    }
    private async Task SaveAsync(DeviceAiUndoRecord record, string path, CancellationToken ct)
    {
        if (record.DocumentId == Guid.Empty || record.PageId == Guid.Empty || record.BeforeHtml is null)
            throw new ArgumentException("Invalid AI undo record.", nameof(record));
        Directory.CreateDirectory(root);
        await _writer.WriteAsync(path, JsonSerializer.SerializeToUtf8Bytes(record, Json), ct);
    }
}
