using System.Text.Json;
using System.Text.Json.Serialization;

namespace WriterApp.Device.Shared.Storage;

internal static class LocalDocumentCodec
{
    public const int SchemaVersion = 4;
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };

    private sealed record Envelope(int SchemaVersion, LocalDocument Document);
    private sealed record LegacyDraft
    {
        public required Guid DocumentId { get; init; }
        public required string Title { get; init; }
        public required string ContentJson { get; init; }
        public required DateTimeOffset UpdatedAtUtc { get; init; }
        public string? ServerVersion { get; init; }
    }

    public static byte[] Encode(LocalDocument document)
    {
        Validate(document);
        return JsonSerializer.SerializeToUtf8Bytes(new Envelope(SchemaVersion, document), Options);
    }

    public static (LocalDocument Document, bool Migrated) Decode(byte[] bytes, Guid expectedId, string fileName)
    {
        using JsonDocument json = JsonDocument.Parse(bytes);
        LocalDocument document;
        bool migrated = false;
        if (json.RootElement.ValueKind != JsonValueKind.Object)
            throw new JsonException("Expected a document object.");

        if (json.RootElement.TryGetProperty("schemaVersion", out JsonElement schema))
        {
            if (schema.ValueKind != JsonValueKind.Number || !schema.TryGetInt32(out int version) || version is not (1 or 2 or 3 or SchemaVersion))
                throw new LocalDocumentReadException(new(fileName, LocalDocumentIssueKind.UnsupportedVersion,
                    "This document uses an unsupported schema. Keep the file and open it with a compatible app version."));
            // Future versions must add explicit, stepwise migrations here before writing a new envelope.
            document = JsonSerializer.Deserialize<Envelope>(bytes, Options)?.Document
                ?? throw new JsonException("Missing document.");
            migrated = version < SchemaVersion;
        }
        else
        {
            LegacyDraft legacy = JsonSerializer.Deserialize<LegacyDraft>(bytes, Options)
                ?? throw new JsonException("Missing draft.");
            if (legacy.ContentJson is null) throw new JsonException("Missing legacy content.");
            document = NewDocument(legacy.DocumentId, legacy.Title, legacy.UpdatedAtUtc,
                legacy.ContentJson, DetectLegacyFormat(legacy.ContentJson)) with { ServerVersion = legacy.ServerVersion };
            migrated = true;
        }

        Validate(document);
        if (document.DocumentId != expectedId) throw new JsonException("Document identity does not match its filename.");
        return (Normalize(document), migrated);
    }

    public static LocalDocument NewDocument(Guid id, string title, DateTimeOffset now,
        string content = "", LocalContentFormat format = LocalContentFormat.Html) => new()
    {
        DocumentId = id, Title = title, CreatedAtUtc = now, UpdatedAtUtc = now, LocalRevision = 1,
        Sections = [new LocalSection
        {
            SectionId = Guid.NewGuid(), Title = "Section 1", OrderIndex = 0,
            CreatedAtUtc = now, UpdatedAtUtc = now,
            Pages = [new LocalPage
            {
                PageId = Guid.NewGuid(), Title = "Page 1", OrderIndex = 0, Content = content,
                ContentFormat = format, CreatedAtUtc = now, UpdatedAtUtc = now
            }]
        }]
    };

    public static LocalDocument Normalize(LocalDocument document) => document with
    {
        Sections = document.Sections.OrderBy(section => section.OrderIndex)
            .Select(section => section with { Pages = section.Pages.OrderBy(page => page.OrderIndex).ToArray() }).ToArray()
    };

    public static void Validate(LocalDocument document)
    {
        if (document.DocumentId == Guid.Empty || document.Title is null || string.IsNullOrWhiteSpace(document.Kind)
            || document.LocalRevision < 1 || !Enum.IsDefined(document.SyncState)
            || document.ServerDocumentId == Guid.Empty || document.ServerProjectId == Guid.Empty
            || document.Sections is null || document.DeletedSections is null || document.DeletedPages is null)
            throw new JsonException("Invalid document metadata.");
        ValidateTimes(document.CreatedAtUtc, document.UpdatedAtUtc);
        if (document.DeletedAtUtc is { } deleted && (deleted < document.CreatedAtUtc || deleted > document.UpdatedAtUtc))
            throw new JsonException("Invalid deletion timestamp.");
        var sectionIds = new HashSet<Guid>();
        var pageIds = new HashSet<Guid>();
        var sectionOrder = new HashSet<int>();
        foreach (LocalSection section in document.Sections) ValidateSection(section, sectionOrder);
        foreach (var entry in document.DeletedSections)
        {
            if (entry is null || entry.DeletedAtUtc == default) throw new JsonException("Invalid section trash.");
            ValidateSection(entry.Section, new HashSet<int>());
        }
        foreach (var entry in document.DeletedPages)
        {
            if (entry is null || entry.SectionId == Guid.Empty || entry.DeletedAtUtc == default)
                throw new JsonException("Invalid page trash.");
            ValidatePage(entry.Page, new HashSet<int>());
        }
        void ValidateSection(LocalSection section, HashSet<int> order)
        {
            if (section is null || section.SectionId == Guid.Empty || !sectionIds.Add(section.SectionId)
                || section.ServerSectionId == Guid.Empty || section.Title is null || section.OrderIndex < 0
                || !order.Add(section.OrderIndex) || section.Pages is null)
                throw new JsonException("Invalid section metadata or ordering.");
            ValidateTimes(section.CreatedAtUtc, section.UpdatedAtUtc);
            var pageOrder = new HashSet<int>();
            foreach (LocalPage page in section.Pages) ValidatePage(page, pageOrder);
        }
        void ValidatePage(LocalPage page, HashSet<int> pageOrder)
        {
            if (page is null || page.PageId == Guid.Empty || !pageIds.Add(page.PageId)
                || page.ServerPageId == Guid.Empty || page.Title is null || page.Content is null
                || page.OrderIndex < 0 || !pageOrder.Add(page.OrderIndex) || !Enum.IsDefined(page.ContentFormat))
                throw new JsonException("Invalid page metadata, content format or ordering.");
            ValidateTimes(page.CreatedAtUtc, page.UpdatedAtUtc);
        }
        if (sectionIds.Overlaps(pageIds)) throw new JsonException("Section/page identities must be distinct.");
        Services.LocalProjectStructure.Validate(document);
    }

    private static void ValidateTimes(DateTimeOffset created, DateTimeOffset updated)
    {
        if (created == default || updated < created) throw new JsonException("Invalid timestamps.");
    }

    private static LocalContentFormat DetectLegacyFormat(string content)
    {
        if (content.TrimStart().StartsWith('<')) return LocalContentFormat.Html;
        try
        {
            using JsonDocument json = JsonDocument.Parse(content);
            return LocalContentFormat.LegacyJson;
        }
        catch (JsonException) { return LocalContentFormat.LegacyText; }
    }
}
