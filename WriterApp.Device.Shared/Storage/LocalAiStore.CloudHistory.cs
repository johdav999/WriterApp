using System.Security.Cryptography;
using WriterApp.Shared;

namespace WriterApp.Device.Shared.Storage;

public sealed record LocalCloudHistoryCache(int Version, string Scope, Guid LocalDocumentId, DeviceCloudHistory Snapshot);
public sealed record LocalHistoryReceipt(int Version, string Scope, Guid LocalDocumentId, DeviceAiHistoryReceipt Receipt);

public sealed partial class LocalAiStore
{
    private static string SnapshotHash(LocalDocument document) => Convert.ToHexString(SHA256.HashData(LocalDocumentCodec.Encode(document))).ToLowerInvariant();
    private static LocalAiHistory AddHistoryDelivery(LocalAiHistory value, LocalAiHistory? previous)
    {
        if (value.CloudOrigin is null) return value;
        var events = (previous?.Deliveries ?? []).ToList();
        // Only durable terminal states are reportable; an Applying intent is never an applied event.
        if (value.Status is "Reviewed" or "Applied" or "Undone" && events.LastOrDefault()?.State != value.Status)
        {
            LocalDocument? after = value.After;
            if (after is null && value.PageId is { } page && value.AfterHtml is { } html)
                after = value.Before with { Sections = value.Before.Sections.Select(s => s with { Pages = s.Pages.Select(p => p.PageId == page
                    ? p with { Content = html, ContentFormat = LocalContentFormat.Html } : p).ToArray() }).ToArray() };
            // Keep incomplete old records local; never invent evidence for a cloud outcome.
            if (value.Status == "Reviewed" || after is not null)
                events.Add(new(Guid.NewGuid(), events.Count + 1, events.LastOrDefault()?.OperationId, value.Status,
                    DateTimeOffset.UtcNow, SnapshotHash(value.Before), value.Status == "Reviewed" ? null : SnapshotHash(after!)));
        }
        return value with { Deliveries = events };
    }
    private static void ValidateHistoryDelivery(LocalAiHistory value)
    {
        if (value.CloudOrigin is null)
        {
            if (value.Deliveries?.Count > 0) throw new InvalidDataException("History events have no cloud identity.");
            return;
        }
        DeviceAiHistoryContracts.Validate(value.CloudOrigin);
        if (value.CloudOrigin.DocumentId != value.Before.ServerDocumentId || value.CloudOrigin.SourceVersion != value.ServerVersion)
            throw new InvalidDataException("History cloud identity does not match its retained source.");
        var events = value.Deliveries ?? [];
        string beforeHash = SnapshotHash(value.Before);
        if (events.Count > 10000 || events.Select(e => e.OperationId).Distinct().Count() != events.Count) throw new InvalidDataException("Invalid history delivery sequence.");
        for (int i = 0; i < events.Count; i++)
        {
            var evt = events[i];
            if (evt.Sequence != i + 1 || evt.PreviousOperationId != (i == 0 ? null : events[i - 1].OperationId)
                || evt.BeforeHash != beforeHash || i > 0 && (evt.State == events[i - 1].State || evt.State == "Reviewed")
                || evt.State == "Undone" && (i == 0 || events[i - 1].State != "Applied")) throw new InvalidDataException("Invalid history delivery chain.");
            DeviceAiHistoryContracts.Validate(HistoryReport(value, evt));
        }
    }
    public static DeviceAiHistoryReport HistoryReport(LocalAiHistory entry, AiHistoryDelivery evt) => new(1, entry.Id, entry.DocumentId,
        entry.CloudOrigin ?? throw new InvalidDataException("This older local entry has no cloud proposal identity."), entry.Target,
        entry.SectionId ?? entry.Before.Sections.FirstOrDefault(s => s.Pages.Any(p => p.PageId == entry.PageId))?.SectionId,
        entry.PageId, entry.NodeId ?? (entry.Target == "Storyboard:created-scene" ? entry.After?.Project?.Nodes.SingleOrDefault(n => n.NodeType == "scene"
            && n.DeletionId is null && entry.Before.Project!.Nodes.All(old => old.NodeId != n.NodeId))?.NodeId : null), entry.SourceRevision, evt, entry.After?.DocumentId ?? entry.DocumentId);
    private string CloudHistoryPath(string scope, Guid local) => Path.Combine(root, "cloud-history", scope, local.ToString("N") + ".json");
    public async Task<LocalCloudHistoryCache?> CachedHistoryAsync(string scope, Guid local, Guid cloud, CancellationToken ct = default)
    {
        RequireScope(scope); string path = CloudHistoryPath(scope, local); if (!File.Exists(path)) return null;
        if (new FileInfo(path).Length > 16 * 1024 * 1024) throw new InvalidDataException("Cloud history cache exceeds the safe size limit. Its original is retained.");
        var cache = await Read<LocalCloudHistoryCache>(path, ct);
        if (cache.Version != 1 || cache.Scope != scope || cache.LocalDocumentId != local) throw new InvalidDataException("Cloud history cache identity mismatch.");
        DeviceAiHistoryContracts.Validate(cache.Snapshot, cloud); return cache;
    }
    public Task SaveCloudHistoryAsync(LocalCloudHistoryCache cache, CancellationToken ct = default) => PresetWrite(async () =>
    {
        RequireScope(cache.Scope); if (cache.Version != 1 || cache.LocalDocumentId == Guid.Empty) throw new InvalidDataException("Invalid history cache.");
        DeviceAiHistoryContracts.Validate(cache.Snapshot, cache.Snapshot.DocumentId);
        if (System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(cache, Json).Length > 16 * 1024 * 1024) throw new InvalidDataException("Cloud history cache exceeds the safe size limit.");
        await Write(CloudHistoryPath(cache.Scope, cache.LocalDocumentId), cache, ct); return true;
    }, ct);
    private string HistoryReceiptPath(string scope, Guid local, Guid operation) => Path.Combine(root, "history-receipts", scope, local.ToString("N"), operation.ToString("N") + ".json");
    public async Task<bool> HistoryReportedAsync(string scope, DeviceAiHistoryReport report, CancellationToken ct = default)
    {
        RequireScope(scope); string path = HistoryReceiptPath(scope, report.LocalDocumentId, report.Event.OperationId); if (!File.Exists(path)) return false;
        var stored = await Read<LocalHistoryReceipt>(path, ct);
        ValidateReceipt(scope, report, stored); return true;
    }
    private static void ValidateReceipt(string scope, DeviceAiHistoryReport report, LocalHistoryReceipt stored)
    {
        var receipt = stored.Receipt;
        if (stored.Version != 1 || stored.Scope != scope || stored.LocalDocumentId != report.LocalDocumentId || receipt.Version != 1
            || receipt.OperationId != report.Event.OperationId || receipt.ProposalId != report.Origin.ProposalId || receipt.LocalEntryId != report.LocalEntryId
            || receipt.Sequence != report.Event.Sequence || receipt.State != report.Event.State || receipt.RequestHash != DeviceAiHistoryContracts.Hash(report))
            throw new InvalidDataException("The reporting receipt does not confirm this saved event. Its delivery remains pending.");
    }
    public Task SaveHistoryReceiptAsync(string scope, DeviceAiHistoryReport report, DeviceAiHistoryReceipt receipt, CancellationToken ct = default) => PresetWrite(async () =>
    {
        RequireScope(scope); var stored = new LocalHistoryReceipt(1, scope, report.LocalDocumentId, receipt); ValidateReceipt(scope, report, stored);
        string path = HistoryReceiptPath(scope, report.LocalDocumentId, report.Event.OperationId);
        if (File.Exists(path)) ValidateReceipt(scope, report, await Read<LocalHistoryReceipt>(path, ct));
        else await Write(path, stored, ct); return true;
    }, ct);
}
