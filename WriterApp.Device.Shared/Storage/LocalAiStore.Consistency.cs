namespace WriterApp.Device.Shared.Storage;

public sealed partial class LocalAiStore
{
    private string ConsistencyDecisionPath(Guid document, string scope, string key)
    {
        RequireScope(scope);
        if (document == Guid.Empty || key.Length != 64 || !key.All(Uri.IsHexDigit))
            throw new InvalidDataException("Invalid consistency decision identity.");
        return Path.Combine(root, "consistency-decisions", scope, document.ToString("N"), key + ".json");
    }
    public Task SetConsistencyIntentionalAsync(Guid document, string scope, string key, bool intentional, CancellationToken ct = default) =>
        PresetWrite(async () => { await Write(ConsistencyDecisionPath(document, scope, key), intentional, ct); return true; }, ct);

    public async Task<bool> IsConsistencyIntentionalAsync(Guid document, string scope, string key, CancellationToken ct = default)
    {
        string path = ConsistencyDecisionPath(document, scope, key);
        return File.Exists(path) && await Read<bool>(path, ct);
    }
}
