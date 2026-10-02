using System.Security.Cryptography;
using System.Text.Json;
using WriterApp.Device.Shared.Storage;
using WriterApp.Shared.Sync;

namespace WriterApp.Device.Shared.Services;

internal static partial class DeviceSyncMapping
{
    public static SyncUpload Upload(LocalDocument doc) => new(doc.Title, doc.LanguageCode,
        doc.Sections.Select(s => new SyncSection(s.ServerSectionId ?? s.SectionId, s.Title, s.OrderIndex, s.NarrativePurpose, s.LanguageCode,
            s.Pages.Select(p => new SyncPage(p.ServerPageId ?? p.PageId, p.Title, p.OrderIndex, p.Content,
                p.ContentFormat == LocalContentFormat.Html ? "html" : p.ContentFormat == LocalContentFormat.LegacyJson ? "legacyJson" : "legacyText")).ToArray())).ToArray(),
        new(true, doc.DeletedSections.Select(x => x.Section.ServerSectionId ?? x.Section.SectionId).ToArray(),
            doc.DeletedSections.SelectMany(x => x.Section.Pages).Concat(doc.DeletedPages.Select(x => x.Page))
                .Select(p => p.ServerPageId ?? p.PageId).ToArray()), UploadProject(doc), doc.Kind);
    public static string Fingerprint(LocalDocument doc) => Convert.ToHexString(SHA256.HashData(
        JsonSerializer.SerializeToUtf8Bytes(new { Content = Upload(doc) with { Structure = null,
            Project = UploadProject(doc) is { } p ? p with { PrimaryDocumentId = doc.Project!.PrimaryDocumentId, MetadataRevision = 0 } : null },
            Trashed = doc.DeletedAtUtc is not null }, DeviceSyncJournal.Json)));
    public static string WritingFingerprint(LocalDocument doc) => Hash(Upload(doc) with { Structure = null });
    public static string ContentFingerprint(LocalDocument doc) => Hash(Upload(doc) with { Title = "", Structure = null });
    private static string Hash(SyncUpload upload) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(
        upload with { Project = upload.Project is { } p ? p with { MetadataRevision = 0 } : null }, DeviceSyncJournal.Json)));

    public static LocalDocument Download(SyncSnapshot snapshot, Guid localId, LocalDocument? previous, DateTimeOffset now)
    {
        var doc = snapshot.Document ?? throw new InvalidOperationException("A tombstone has no content.");
        var oldSections = (previous?.Sections ?? []).Concat(previous?.DeletedSections.Select(x => x.Section) ?? []).ToArray();
        var oldPages = oldSections.SelectMany(s => s.Pages).Concat(previous?.DeletedPages.Select(x => x.Page) ?? []).ToArray();
        var activeSectionIds = doc.Sections.Select(s => s.Id).ToHashSet();
        var activePageIds = doc.Sections.SelectMany(s => s.Pages).Select(p => p.Id).ToHashSet();
        var sectionTrash = (previous?.DeletedSections ?? [])
            .Concat((previous?.Sections ?? []).Where(s => !activeSectionIds.Contains(s.ServerSectionId ?? s.SectionId)).Select(s => new LocalDeletedSection(s, now)))
            .Where(x => !activeSectionIds.Contains(x.Section.ServerSectionId ?? x.Section.SectionId))
            .Select(x => x with { Section = x.Section with { Pages = x.Section.Pages.Where(p => !activePageIds.Contains(p.ServerPageId ?? p.PageId)).ToArray() } }).ToArray();
        var archivedPageIds = sectionTrash.SelectMany(x => x.Section.Pages).Select(p => p.ServerPageId ?? p.PageId).ToHashSet();
        var pageTrash = (previous?.DeletedPages ?? []).Concat((previous?.Sections ?? []).SelectMany(s => s.Pages.Select(p => new LocalDeletedPage(s.SectionId, p, now))))
            .Where(x => !activePageIds.Contains(x.Page.ServerPageId ?? x.Page.PageId) && !archivedPageIds.Contains(x.Page.ServerPageId ?? x.Page.PageId))
            .DistinctBy(x => x.Page.PageId).ToArray();
        LocalDocument downloaded = new()
        {
            DocumentId = localId, ServerDocumentId = doc.Id, ServerProjectId = doc.ProjectId,
            Title = doc.Title, Kind = doc.Kind, LanguageCode = doc.LanguageCode,
            LocalRevision = previous?.LocalRevision ?? 1, CreatedAtUtc = doc.CreatedAt, UpdatedAtUtc = doc.UpdatedAt,
            DeletedAtUtc = snapshot.State.IsTrashed ? now : null, ServerVersion = snapshot.State.Version,
            LastSyncedAtUtc = now, SyncState = LocalSyncState.Synced,
            ExtensionData = previous?.ExtensionData, DeletedSections = sectionTrash, DeletedPages = pageTrash,
            Project = DownloadProject(doc, localId, previous),
            Sections = doc.Sections.Select(s =>
            {
                var old = oldSections.FirstOrDefault(x => (x.ServerSectionId ?? x.SectionId) == s.Id);
                return new LocalSection
                {
                    SectionId = old?.SectionId ?? s.Id, ServerSectionId = s.Id, Title = s.Title,
                    NarrativePurpose = s.NarrativePurpose, LanguageCode = s.LanguageCode, OrderIndex = s.OrderIndex,
                    CreatedAtUtc = old?.CreatedAtUtc ?? doc.CreatedAt, UpdatedAtUtc = doc.UpdatedAt,
                    ExtensionData = old?.ExtensionData,
                    Pages = s.Pages.Select(p =>
                    {
                        var oldPage = oldPages.FirstOrDefault(x => (x.ServerPageId ?? x.PageId) == p.Id);
                        return new LocalPage {
                        PageId = oldPage?.PageId ?? p.Id,
                        ServerPageId = p.Id, Title = p.Title, OrderIndex = p.OrderIndex, Content = p.Content,
                        ContentFormat = p.ContentFormat switch { "html" => LocalContentFormat.Html, "legacyText" => LocalContentFormat.LegacyText,
                            "legacyJson" => LocalContentFormat.LegacyJson, _ => throw new JsonException("Unsupported server content format.") },
                        CreatedAtUtc = oldPage?.CreatedAtUtc ?? doc.CreatedAt, UpdatedAtUtc = doc.UpdatedAt,
                        ExtensionData = oldPage?.ExtensionData };
                    }).ToArray()
                };
            }).ToArray()
        };
        return LocalPlanning.Reconcile(previous ?? downloaded, downloaded);
    }
    public static LocalDocument Copy(LocalDocument source, Guid id, string suffix)
    {
        var detached = LocalDocumentStructure.DetachIdentities(source, preserveProject: true);
        return detached with {
        DocumentId = id, Title = source.Title + suffix, LocalRevision = 1, ServerDocumentId = null, ServerProjectId = null,
        Project = detached.Project is { } project ? project with { ManuscriptId = id, PrimaryDocumentId = source.Kind == "manuscript" ? id : null, Title = project.Title + suffix } : null,
        ServerVersion = null, LastSyncedAtUtc = null, SyncState = LocalSyncState.LocalOnly, DeletedAtUtc = null
        };
    }
}
