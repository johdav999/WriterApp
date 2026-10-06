using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WriterApp.Application.Documents;

namespace WriterApp.Shared.Quality;

public sealed record QualityDismissalDecision(string IssueKey, bool Dismissed);
public sealed record QualityDismissalRequest(int Version, Guid DocumentId, string SourceHash,
    IReadOnlyList<QualityDismissalDecision> Decisions);
public sealed record QualityDismissalReceipt(int Version, Guid DocumentId, Guid PageId, string SourceHash,
    IReadOnlyList<QualityDismissalDecision> Decisions, IReadOnlyList<string>? LegacyIssueKeys = null);
public sealed record QualityDismissedFinding(string Id, string Rule, string Message, string? Anchor, string Status, bool Active, bool Dismissed);

/// <summary>Exact source, rule revision and glossary identity. Offsets in IssueKey identify an occurrence.</summary>
public static class QualityDismissalIdentity
{
    public static string Source(string text, IReadOnlyList<string> glossary) => Hash(QualityRuleCatalog.CacheVersion + ":" +
        JsonSerializer.Serialize(new { Text = text, Glossary = glossary }));
    public static string Key(string sourceHash, string issueKey) => "qd1:" + Hash(sourceHash + ":" + issueKey);
    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    public static bool IsHash(string? value) => value?.Length == 64 && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
}
