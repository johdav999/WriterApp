namespace WriterApp.Device.Shared.Storage;

public sealed record LocalAiHistoryDismissal(int Version, Guid DocumentId, Guid EntryId, string? Scope);

public sealed partial class LocalAiStore
{
    private string DismissalDirectory(Guid document, string? scope) =>
        Path.Combine(root, "history-dismissals", document.ToString("N"), scope ?? "local");

    // Visibility is separate from recovery evidence and pending cloud delivery.
    public Task DismissHistoryAsync(Guid document, Guid entry, string? scope = null, CancellationToken ct = default) => PresetWrite(async () =>
    {
        if (document == Guid.Empty || entry == Guid.Empty) throw new InvalidDataException("Invalid history identity.");
        if (scope is not null) RequireScope(scope);
        await Write(Path.Combine(DismissalDirectory(document, scope), entry.ToString("N") + ".json"),
            new LocalAiHistoryDismissal(1, document, entry, scope), ct);
        return true;
    }, ct);

    public async Task<IReadOnlySet<Guid>> DismissedHistoryAsync(Guid document, string? scope = null, CancellationToken ct = default)
    {
        if (document == Guid.Empty) throw new InvalidDataException("Invalid history document.");
        if (scope is not null) RequireScope(scope);
        var result = new HashSet<Guid>();
        foreach (var owner in scope is null ? new string?[] { null } : new string?[] { null, scope })
        {
            string directory = DismissalDirectory(document, owner);
            if (!Directory.Exists(directory)) continue;
            foreach (var path in Directory.EnumerateFiles(directory, "*.json"))
            {
                var value = await Read<LocalAiHistoryDismissal>(path, ct);
                if (value.Version != 1 || value.DocumentId != document || value.Scope != owner || value.EntryId == Guid.Empty
                    || Path.GetFileNameWithoutExtension(path) != value.EntryId.ToString("N"))
                    throw new InvalidDataException("History dismissal identity mismatch.");
                result.Add(value.EntryId);
            }
        }
        return result;
    }
}
