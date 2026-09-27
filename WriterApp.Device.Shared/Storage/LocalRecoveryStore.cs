using System.Text.Json;

namespace WriterApp.Device.Shared.Storage;

public sealed record LocalRecoveryRecord(int SchemaVersion, Guid RecoveryId, DateTimeOffset SavedAtUtc, LocalDocument Document);
public sealed record LocalRecoveryList(IReadOnlyList<LocalRecoveryRecord> Records, IReadOnlyList<string> Issues);

/// <summary>Separate, atomically replaced journal; never depends on a browser or network.</summary>
public sealed class LocalRecoveryStore
{
    private readonly string _root;
    private readonly AtomicDocumentWriter _writer;
    private readonly TimeProvider _time;
    public LocalRecoveryStore(string root, TimeProvider? time = null)
        : this(root, time ?? TimeProvider.System, new AtomicDocumentWriter()) { }
    internal LocalRecoveryStore(string root, TimeProvider time, AtomicDocumentWriter writer)
    { _root = Path.GetFullPath(root); _time = time; _writer = writer; }
    private string PathFor(Guid id) => Path.Combine(_root, $"{id:N}.json");

    public async Task WriteAsync(LocalDocument document, Guid? recoveryId = null)
    {
        LocalDocumentCodec.Validate(document);
        Directory.CreateDirectory(_root);
        Guid id = recoveryId ?? document.DocumentId;
        await _writer.WriteAsync(PathFor(id),
            JsonSerializer.SerializeToUtf8Bytes(new LocalRecoveryRecord(1, id, _time.GetUtcNow(), document)), CancellationToken.None);
    }

    public Task DiscardAsync(Guid id)
    { if (Directory.Exists(_root)) File.Delete(PathFor(id)); return Task.CompletedTask; }

    public async Task<LocalRecoveryList> ListAsync()
    {
        var records = new List<LocalRecoveryRecord>();
        var issues = new List<string>();
        if (!Directory.Exists(_root)) return new(records, issues);
        foreach (string path in Directory.EnumerateFiles(_root, "*.json"))
        {
            try
            {
                var record = JsonSerializer.Deserialize<LocalRecoveryRecord>(await File.ReadAllBytesAsync(path));
                if (record is null || record.SchemaVersion != 1 || record.Document is null
                    || record.RecoveryId == Guid.Empty || Path.GetFileName(path) != $"{record.RecoveryId:N}.json"
                    || record.Document.Sections is null || record.Document.Sections.Any(s => s is null || s.Pages is null
                        || s.Pages.Any(p => p is null || p.Content is null)))
                    throw new JsonException();
                LocalDocumentCodec.Validate(record.Document);
                records.Add(record);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
            { issues.Add($"Recovery file {Path.GetFileName(path)} could not be read. It has been preserved."); }
        }
        return new(records, issues);
    }

    // A crash after the main commit but before journal cleanup needs no restore prompt.
    public static bool Matches(LocalDocument left, LocalDocument right) =>
        left.Sections.SelectMany(s => s.Pages).Select(p => (p.PageId, p.ContentFormat, p.Content))
            .SequenceEqual(right.Sections.SelectMany(s => s.Pages).Select(p => (p.PageId, p.ContentFormat, p.Content)));
}
