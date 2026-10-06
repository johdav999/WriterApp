using System.Text.Json;

namespace WriterApp.Shared;

public sealed record AiHistoryOrigin(string Scope, Guid ProposalId, Guid DocumentId, Guid? SectionId,
    Guid? PageId, string ActionKey, string SourceVersion);
public sealed record AiHistoryDelivery(Guid OperationId, int Sequence, Guid? PreviousOperationId,
    string State, DateTimeOffset OccurredAt, string BeforeHash, string? AfterHash);
public sealed record DeviceAiHistoryReport(int Version, Guid LocalEntryId, Guid LocalDocumentId,
    AiHistoryOrigin Origin, string Target, Guid? LocalSectionId, Guid? LocalPageId, Guid? LocalNodeId,
    long SourceRevision, AiHistoryDelivery Event, Guid? TargetDocumentId = null);
public sealed record DeviceAiHistoryReceipt(int Version, Guid OperationId, Guid ProposalId,
    Guid LocalEntryId, int Sequence, string State, string RequestHash);
public sealed record DeviceCloudHistoryEntry(Guid ProposalId, Guid DocumentId, Guid? SectionId, Guid? PageId,
    string ActionKey, string? SourceVersion, DateTimeOffset CreatedAt, string Original, string Proposed,
    string State, int AppliedCount, string? Target = null);
public sealed record DeviceCloudHistory(int Version, Guid DocumentId, DateTimeOffset CheckedAt,
    IReadOnlyList<DeviceCloudHistoryEntry> Entries, bool Truncated = false);

public static class DeviceAiHistoryContracts
{
    public const int MaxEntries = 200;
    public static string Hash(object value) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
        System.Text.Encoding.UTF8.GetBytes(ReusablePrompts.Canonical(value)))).ToLowerInvariant();
    public static bool IsHash(string? value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);
    public static void Validate(AiHistoryOrigin origin)
    {
        if (!IsHash(origin.Scope) || origin.ProposalId == Guid.Empty || origin.DocumentId == Guid.Empty
            || origin.SectionId == Guid.Empty || origin.PageId == Guid.Empty || string.IsNullOrWhiteSpace(origin.ActionKey)
            || origin.ActionKey.Length > 128 || string.IsNullOrWhiteSpace(origin.SourceVersion) || origin.SourceVersion.Length > 200)
            throw new InvalidDataException("Invalid cloud history identity. Original local recovery is retained.");
    }
    public static void Validate(DeviceAiHistoryReport report)
    {
        Validate(report.Origin);
        var evt = report.Event;
        if (report.Version != 1 || report.LocalEntryId == Guid.Empty || report.LocalDocumentId == Guid.Empty
            || report.SourceRevision < 0 || string.IsNullOrWhiteSpace(report.Target) || report.Target.Length > 200
            || report.LocalSectionId == Guid.Empty || report.LocalPageId == Guid.Empty || report.LocalNodeId == Guid.Empty
            || report.TargetDocumentId is null || report.TargetDocumentId == Guid.Empty
            || evt.OperationId == Guid.Empty || evt.Sequence < 1 || evt.Sequence > 10000
            || (evt.Sequence == 1 ? evt.PreviousOperationId is not null : evt.PreviousOperationId is null || evt.PreviousOperationId == Guid.Empty)
            || evt.State is not ("Reviewed" or "Applied" or "Undone") || evt.OccurredAt == default
            || !IsHash(evt.BeforeHash) || evt.State != "Reviewed" && !IsHash(evt.AfterHash))
            throw new InvalidDataException("Invalid history reporting intent. No writing is changed.");
    }
    public static void Validate(DeviceCloudHistory history, Guid document)
    {
        if (history.Version != 1 || history.DocumentId != document || history.CheckedAt == default
            || history.Entries is null || history.Entries.Count > MaxEntries || history.Entries.Any(e => e is null)
            || history.Entries.Select(e => e.ProposalId).Distinct().Count() != history.Entries.Count)
            throw new InvalidDataException("Invalid cloud history. The previous cache is retained.");
        foreach (var entry in history.Entries)
            if (entry is null || entry.ProposalId == Guid.Empty || entry.DocumentId != document || entry.CreatedAt == default
                || string.IsNullOrWhiteSpace(entry.ActionKey) || entry.ActionKey.Length > 128
                || entry.Original is null || entry.Proposed is null || entry.Original.Length > 100000 || entry.Proposed.Length > 100000
                || entry.State is not ("Generated" or "Reviewed" or "Applied" or "Undone") || entry.AppliedCount < 0
                || entry.SectionId == Guid.Empty || entry.PageId == Guid.Empty || entry.Target?.Length > 200 || entry.SourceVersion?.Length > 200
                || entry.SourceVersion is not null && string.IsNullOrWhiteSpace(entry.SourceVersion))
                throw new InvalidDataException("Malformed cloud history entry. The previous cache is retained.");
    }
}
