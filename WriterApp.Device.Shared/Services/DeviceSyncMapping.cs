using System.Security.Cryptography;
using System.Text.Json;
using WriterApp.Device.Shared.Storage;
using WriterApp.Shared.Sync;

namespace WriterApp.Device.Shared.Services;

internal static class DeviceSyncMapping
{
    public static SyncUpload Upload(LocalDocument doc) => new(doc.Title, doc.LanguageCode,
        doc.Sections.Select(s => new SyncSection(s.ServerSectionId ?? s.SectionId, s.Title, s.OrderIndex, s.NarrativePurpose, s.LanguageCode,
            s.Pages.Select(p => new SyncPage(p.ServerPageId ?? p.PageId, p.Title, p.OrderIndex, p.Content,
                p.ContentFormat == LocalContentFormat.Html ? "html" : p.ContentFormat == LocalContentFormat.LegacyJson ? "legacyJson" : "legacyText")).ToArray())).ToArray());
    public static string Fingerprint(LocalDocument doc) => Convert.ToHexString(SHA256.HashData(
        JsonSerializer.SerializeToUtf8Bytes(new { Content = Upload(doc), Trashed = doc.DeletedAtUtc is not null }, DeviceSyncJournal.Json)));
    public static string WritingFingerprint(LocalDocument doc) => Hash(Upload(doc));
    public static string ContentFingerprint(LocalDocument doc) => Hash(Upload(doc) with { Title = "" });
    private static string Hash(SyncUpload upload) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(upload, DeviceSyncJournal.Json)));

    public static LocalDocument Download(SyncSnapshot snapshot, Guid localId, LocalDocument? previous, DateTimeOffset now)
    {
        var doc = snapshot.Document ?? throw new InvalidOperationException("A tombstone has no content.");
        return new()
        {
            DocumentId = localId, ServerDocumentId = doc.Id, ServerProjectId = doc.ProjectId,
            Title = doc.Title, Kind = doc.Kind, LanguageCode = doc.LanguageCode,
            LocalRevision = previous?.LocalRevision ?? 1, CreatedAtUtc = doc.CreatedAt, UpdatedAtUtc = doc.UpdatedAt,
            DeletedAtUtc = snapshot.State.IsTrashed ? now : null, ServerVersion = snapshot.State.Version,
            LastSyncedAtUtc = now, SyncState = LocalSyncState.Synced,
            Sections = doc.Sections.Select(s =>
            {
                var old = previous?.Sections.FirstOrDefault(x => (x.ServerSectionId ?? x.SectionId) == s.Id);
                return new LocalSection
                {
                    SectionId = old?.SectionId ?? s.Id, ServerSectionId = s.Id, Title = s.Title,
                    NarrativePurpose = s.NarrativePurpose, LanguageCode = s.LanguageCode, OrderIndex = s.OrderIndex,
                    CreatedAtUtc = old?.CreatedAtUtc ?? doc.CreatedAt, UpdatedAtUtc = doc.UpdatedAt,
                    Pages = s.Pages.Select(p => new LocalPage
                    {
                        PageId = old?.Pages.FirstOrDefault(x => (x.ServerPageId ?? x.PageId) == p.Id)?.PageId ?? p.Id,
                        ServerPageId = p.Id, Title = p.Title, OrderIndex = p.OrderIndex, Content = p.Content,
                        ContentFormat = p.ContentFormat switch { "html" => LocalContentFormat.Html, "legacyText" => LocalContentFormat.LegacyText,
                            "legacyJson" => LocalContentFormat.LegacyJson, _ => throw new JsonException("Unsupported server content format.") },
                        CreatedAtUtc = doc.CreatedAt, UpdatedAtUtc = doc.UpdatedAt
                    }).ToArray()
                };
            }).ToArray()
        };
    }
    public static LocalDocument Copy(LocalDocument source, Guid id, string suffix) => source with
    {
        DocumentId = id, Title = source.Title + suffix, LocalRevision = 1, ServerDocumentId = null, ServerProjectId = null,
        ServerVersion = null, LastSyncedAtUtc = null, SyncState = LocalSyncState.LocalOnly, DeletedAtUtc = null,
        Sections = source.Sections.Select(s => s with { SectionId = Guid.NewGuid(), ServerSectionId = null,
            Pages = s.Pages.Select(p => p with { PageId = Guid.NewGuid(), ServerPageId = null }).ToArray() }).ToArray()
    };
}
