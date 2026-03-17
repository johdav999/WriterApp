using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WriterApp.Application.Documents;
using WriterApp.Data;
using WriterApp.Data.Documents;

namespace WriterApp.Application.Search;

public interface ISearchIndexService
{
    Task UpsertDocumentAsync(DocumentRecord document, CancellationToken ct);
    Task UpsertSectionAsync(SectionRecord section, CancellationToken ct);
    Task UpsertPageAsync(PageRecord page, CancellationToken ct);
    Task UpsertPageNotesAsync(PageRecord page, PageNoteRecord notes, CancellationToken ct);
    Task UpsertSceneCardAsync(SectionRecord section, SectionSceneCardRecord card, CancellationToken ct);
    Task ReplaceOutlineAsync(DocumentRecord document, string outlineText, IReadOnlyList<DocumentOutlineNodeRecord> nodes, CancellationToken ct);
    Task DeleteByEntityAsync(string entityType, Guid entityId, CancellationToken ct);
    Task<IReadOnlyList<SearchResultDto>> SearchAsync(string userId, Guid projectId, string query, bool includeMeta, int limit, string? correlationId, CancellationToken ct);
    Task<int> GetProjectEntryCountAsync(string ownerUserId, Guid projectId, CancellationToken ct);
    Task RebuildProjectIndexAsync(string ownerUserId, Guid projectId, CancellationToken ct);
    string? DisabledReason { get; }
    Task<bool> TryProbeAndRecoverAsync(CancellationToken ct = default);
    Task RebuildSearchIndexAsync(CancellationToken ct = default);
}

public interface ISearchIndexBackfillWorker
{
    Task BackfillUserAsync(string userId, CancellationToken ct);
}

public sealed class SearchIndexService : ISearchIndexService, ISearchIndexBackfillWorker
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<SearchIndexService> _logger;
    private readonly ISearchIndexBackfillQueue _backfillQueue;
    private volatile bool _disabled;
    private string? _disabledReason;

    public SearchIndexService(AppDbContext dbContext, ILogger<SearchIndexService> logger, ISearchIndexBackfillQueue backfillQueue)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _backfillQueue = backfillQueue ?? throw new ArgumentNullException(nameof(backfillQueue));
    }

    public string? DisabledReason => _disabledReason;

    public Task UpsertDocumentAsync(DocumentRecord document, CancellationToken ct) =>
        UpsertEntryAsync(new SearchIndexEntry(SearchEntityTypes.Document, document.Id, document.Id, document.ProjectId, null, null, document.Title ?? string.Empty, document.Title ?? string.Empty, document.UpdatedAt), ct);

    public async Task UpsertSectionAsync(SectionRecord section, CancellationToken ct)
    {
        Guid projectId = section.Document?.ProjectId ?? await ResolveProjectIdForDocumentAsync(section.DocumentId, ct);
        await UpsertEntryAsync(new SearchIndexEntry(SearchEntityTypes.Section, section.Id, section.DocumentId, projectId, section.Id, null, section.Title ?? string.Empty, section.Title ?? string.Empty, section.UpdatedAt), ct);
    }

    public async Task UpsertPageAsync(PageRecord page, CancellationToken ct)
    {
        Guid projectId = page.Document?.ProjectId ?? await ResolveProjectIdForDocumentAsync(page.DocumentId, ct);
        await UpsertEntryAsync(new SearchIndexEntry(SearchEntityTypes.Page, page.Id, page.DocumentId, projectId, page.SectionId, page.Id, page.Title ?? string.Empty, NormalizeText(page.Content), page.UpdatedAt), ct);
    }

    public async Task UpsertPageNotesAsync(PageRecord page, PageNoteRecord notes, CancellationToken ct)
    {
        Guid projectId = page.Document?.ProjectId ?? await ResolveProjectIdForDocumentAsync(page.DocumentId, ct);
        await UpsertEntryAsync(new SearchIndexEntry(SearchEntityTypes.Note, notes.PageId, page.DocumentId, projectId, page.SectionId, page.Id, $"Notes: {page.Title ?? "Page"}", NormalizeText(notes.Notes), notes.UpdatedAt), ct);
    }

    public async Task UpsertSceneCardAsync(SectionRecord section, SectionSceneCardRecord card, CancellationToken ct)
    {
        string content = string.Join(
            "\n",
            new[]
            {
                card.Summary,
                card.Status,
                card.NarrativePurpose,
                card.EmotionalBeat,
                card.KeyEvents,
                card.OpenQuestions
            }.Where(v => !string.IsNullOrWhiteSpace(v)));
        Guid projectId = section.Document?.ProjectId ?? await ResolveProjectIdForDocumentAsync(section.DocumentId, ct);
        await UpsertEntryAsync(new SearchIndexEntry(SearchEntityTypes.SceneCard, card.SectionId, section.DocumentId, projectId, section.Id, null, $"Scene card: {section.Title ?? "Section"}", NormalizeText(content), card.UpdatedUtc), ct);
    }

    public async Task ReplaceOutlineAsync(DocumentRecord document, string outlineText, IReadOnlyList<DocumentOutlineNodeRecord> nodes, CancellationToken ct)
    {
        string normalizedDocumentId = IdNorm.Norm(document.Id);
        List<SearchIndexEntryRecord> existing = await _dbContext.SearchIndexEntries
            .Where(e => e.EntityType == SearchEntityTypes.Outline && e.DocumentId == normalizedDocumentId)
            .ToListAsync(ct);
        if (existing.Count > 0)
        {
            _dbContext.SearchIndexEntries.RemoveRange(existing);
            await _dbContext.SaveChangesAsync(ct);
        }

        if (!string.IsNullOrWhiteSpace(outlineText))
        {
            await UpsertEntryAsync(new SearchIndexEntry(SearchEntityTypes.Outline, document.Id, document.Id, document.ProjectId, null, null, $"Outline: {document.Title ?? "Document"}", NormalizeText(outlineText), document.UpdatedAt), ct);
        }

        if (nodes is null || nodes.Count == 0)
        {
            return;
        }

        foreach (DocumentOutlineNodeRecord node in nodes)
        {
            string content = string.Join("\n", new[] { node.Title, node.Notes }.Where(v => !string.IsNullOrWhiteSpace(v)));
            await UpsertEntryAsync(new SearchIndexEntry(SearchEntityTypes.Outline, node.Id, document.Id, document.ProjectId, node.LinkedSectionId, null, $"Outline: {node.Title}", NormalizeText(content), document.UpdatedAt), ct);
        }
    }

    public async Task DeleteByEntityAsync(string entityType, Guid entityId, CancellationToken ct)
    {
        string normalizedEntityId = IdNorm.Norm(entityId);
        List<SearchIndexEntryRecord> matches = await _dbContext.SearchIndexEntries.Where(e => e.EntityType == entityType && e.EntityId == normalizedEntityId).ToListAsync(ct);
        if (matches.Count == 0)
        {
            return;
        }

        _dbContext.SearchIndexEntries.RemoveRange(matches);
        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<SearchResultDto>> SearchAsync(string userId, Guid projectId, string query, bool includeMeta, int limit, string? correlationId, CancellationToken ct)
    {
        if (IdNorm.TryNormGuidString(userId, out string normalizedUserId))
        {
            userId = normalizedUserId;
        }

        string normalizedQuery = NormalizeText(query);
        if (string.IsNullOrWhiteSpace(normalizedQuery) || !await EnsureSearchIndexAsync(ct))
        {
            return Array.Empty<SearchResultDto>();
        }

        List<DocumentLookup> docs = await _dbContext.Documents.AsNoTracking()
            .Where(d => d.OwnerUserId == userId && d.ProjectId == projectId)
            .Select(d => new DocumentLookup(IdNorm.Norm(d.Id), d.Title))
            .ToListAsync(ct);
        if (docs.Count == 0)
        {
            return Array.Empty<SearchResultDto>();
        }

        HashSet<string> docIds = docs.Select(d => d.DocumentId).ToHashSet(StringComparer.Ordinal);
        string normalizedProjectId = IdNorm.Norm(projectId);
        bool hasEntries = await _dbContext.SearchIndexEntries.AsNoTracking().AnyAsync(e => e.ProjectId == normalizedProjectId && docIds.Contains(e.DocumentId), ct);
        if (!hasEntries)
        {
            _backfillQueue.Enqueue(userId);
            try
            {
                await BackfillUserAsync(userId, ct);
            }
            catch (OperationCanceledException)
            {
                return Array.Empty<SearchResultDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Search backfill failed. CorrelationId={CorrelationId} UserId={UserId} ProjectId={ProjectId}. Returning available index data only.",
                    correlationId ?? string.Empty,
                    userId,
                    projectId);
            }
        }

        string like = $"%{normalizedQuery.ToLowerInvariant()}%";
        int clampedLimit = Math.Clamp(limit, 1, 200);
        List<SearchIndexEntryRecord> rows = await _dbContext.SearchIndexEntries.AsNoTracking()
            .Where(e => e.ProjectId == normalizedProjectId && docIds.Contains(e.DocumentId))
            .Where(e =>
                (e.EntityType.ToLower() == SearchEntityTypes.Page && EF.Functions.Like((e.Content ?? string.Empty).ToLower(), like)) ||
                (includeMeta && e.EntityType.ToLower() != SearchEntityTypes.Page &&
                 (EF.Functions.Like((e.Title ?? string.Empty).ToLower(), like) || EF.Functions.Like((e.Content ?? string.Empty).ToLower(), like))))
            .OrderBy(e =>
                e.EntityType.ToLower() == SearchEntityTypes.Page ? 0 :
                (e.EntityType.ToLower() == SearchEntityTypes.Document || e.EntityType.ToLower() == SearchEntityTypes.Section) ? 1 :
                e.EntityType.ToLower() == SearchEntityTypes.Note ? 2 :
                e.EntityType.ToLower() == SearchEntityTypes.SceneCard ? 3 :
                e.EntityType.ToLower() == SearchEntityTypes.Outline ? 4 : 5)
            .ThenByDescending(e => e.UpdatedAt)
            .Take(clampedLimit)
            .ToListAsync(ct);

        Dictionary<string, string> docTitles = docs.ToDictionary(d => d.DocumentId, d => d.Title ?? string.Empty, StringComparer.Ordinal);
        return rows.Select(row =>
        {
            bool contentMatch = string.Equals(row.EntityType, SearchEntityTypes.Page, StringComparison.OrdinalIgnoreCase);
            return new SearchResultDto(
                ParseGuid(row.DocumentId),
                ParseNullableGuid(row.SectionId),
                ParseNullableGuid(row.PageId),
                row.EntityType,
                row.EntityId,
                row.Title ?? string.Empty,
                BuildSnippet(row.Title ?? string.Empty, row.Content ?? string.Empty, normalizedQuery, contentMatch),
                contentMatch ? 0 : 1,
                docTitles.TryGetValue(row.DocumentId, out string? title) ? title ?? string.Empty : string.Empty,
                contentMatch ? "content" : "meta");
        }).ToList();
    }

    public async Task<int> GetProjectEntryCountAsync(string ownerUserId, Guid projectId, CancellationToken ct)
    {
        if (!await EnsureSearchIndexAsync(ct))
        {
            return 0;
        }

        List<string> docIds = await _dbContext.Documents.AsNoTracking()
            .Where(d => d.OwnerUserId == ownerUserId && d.ProjectId == projectId)
            .Select(d => IdNorm.Norm(d.Id))
            .ToListAsync(ct);
        if (docIds.Count == 0)
        {
            return 0;
        }

        string normalizedProjectId = IdNorm.Norm(projectId);
        return await _dbContext.SearchIndexEntries.AsNoTracking().CountAsync(e => e.ProjectId == normalizedProjectId && docIds.Contains(e.DocumentId), ct);
    }

    public async Task RebuildProjectIndexAsync(string ownerUserId, Guid projectId, CancellationToken ct)
    {
        if (!await EnsureSearchIndexAsync(ct))
        {
            throw new InvalidOperationException("Search index table is not available.");
        }

        List<DocumentRecord> docs = await _dbContext.Documents.AsNoTracking().Where(d => d.OwnerUserId == ownerUserId && d.ProjectId == projectId).ToListAsync(ct);
        List<string> docIds = docs.Select(d => IdNorm.Norm(d.Id)).ToList();
        if (docIds.Count > 0)
        {
            List<SearchIndexEntryRecord> old = await _dbContext.SearchIndexEntries.Where(e => docIds.Contains(e.DocumentId)).ToListAsync(ct);
            if (old.Count > 0)
            {
                _dbContext.SearchIndexEntries.RemoveRange(old);
                await _dbContext.SaveChangesAsync(ct);
            }
        }

        foreach (DocumentRecord doc in docs)
        {
            await UpsertDocumentAsync(doc, ct);
            List<SectionRecord> sections = await _dbContext.Sections.AsNoTracking().Where(s => s.DocumentId == doc.Id).ToListAsync(ct);
            foreach (SectionRecord section in sections)
            {
                await UpsertSectionAsync(section, ct);
                SectionSceneCardRecord? card = await _dbContext.SectionSceneCards.AsNoTracking().FirstOrDefaultAsync(c => c.SectionId == section.Id, ct);
                if (card is not null)
                {
                    await UpsertSceneCardAsync(section, card, ct);
                }
            }

            List<PageRecord> pages = await _dbContext.Pages.AsNoTracking().Where(p => p.DocumentId == doc.Id).ToListAsync(ct);
            foreach (PageRecord page in pages)
            {
                await UpsertPageAsync(page, ct);
                PageNoteRecord? note = await _dbContext.PageNotes.AsNoTracking().FirstOrDefaultAsync(n => n.PageId == page.Id, ct);
                if (note is not null)
                {
                    await UpsertPageNotesAsync(page, note, ct);
                }
            }

            DocumentOutlineRecord? outline = await _dbContext.DocumentOutlines.AsNoTracking().FirstOrDefaultAsync(o => o.DocumentId == doc.Id, ct);
            List<DocumentOutlineNodeRecord> nodes = await _dbContext.DocumentOutlineNodes.AsNoTracking().Where(n => n.DocumentId == doc.Id).ToListAsync(ct);
            if (outline is not null || nodes.Count > 0)
            {
                await ReplaceOutlineAsync(doc, outline?.Outline ?? string.Empty, nodes, ct);
            }
        }
    }

    public async Task<bool> TryProbeAndRecoverAsync(CancellationToken ct = default)
    {
        if (!_disabled)
        {
            return true;
        }

        return await EnsureSearchIndexAsync(ct);
    }

    public async Task RebuildSearchIndexAsync(CancellationToken ct = default)
    {
        List<SearchIndexEntryRecord> all = await _dbContext.SearchIndexEntries.ToListAsync(ct);
        if (all.Count > 0)
        {
            _dbContext.SearchIndexEntries.RemoveRange(all);
            await _dbContext.SaveChangesAsync(ct);
        }

        _disabled = false;
        _disabledReason = null;
        List<string> userIds = await _dbContext.Documents.AsNoTracking().Where(d => !string.IsNullOrWhiteSpace(d.OwnerUserId)).Select(d => d.OwnerUserId).Distinct().ToListAsync(ct);
        foreach (string userId in userIds)
        {
            await BackfillUserAsync(userId, ct);
        }
    }

    public async Task BackfillUserAsync(string userId, CancellationToken ct)
    {
        List<DocumentRecord> docs = await _dbContext.Documents.AsNoTracking().Where(d => d.OwnerUserId == userId).ToListAsync(ct);
        foreach (DocumentRecord doc in docs)
        {
            await UpsertDocumentAsync(doc, ct);
            List<SectionRecord> sections = await _dbContext.Sections.AsNoTracking().Where(s => s.DocumentId == doc.Id).ToListAsync(ct);
            foreach (SectionRecord section in sections)
            {
                await UpsertSectionAsync(section, ct);
                SectionSceneCardRecord? card = await _dbContext.SectionSceneCards.AsNoTracking().FirstOrDefaultAsync(c => c.SectionId == section.Id, ct);
                if (card is not null)
                {
                    await UpsertSceneCardAsync(section, card, ct);
                }
            }

            List<PageRecord> pages = await _dbContext.Pages.AsNoTracking().Where(p => p.DocumentId == doc.Id).ToListAsync(ct);
            foreach (PageRecord page in pages)
            {
                await UpsertPageAsync(page, ct);
                PageNoteRecord? note = await _dbContext.PageNotes.AsNoTracking().FirstOrDefaultAsync(n => n.PageId == page.Id, ct);
                if (note is not null)
                {
                    await UpsertPageNotesAsync(page, note, ct);
                }
            }

            DocumentOutlineRecord? outline = await _dbContext.DocumentOutlines.AsNoTracking().FirstOrDefaultAsync(o => o.DocumentId == doc.Id, ct);
            List<DocumentOutlineNodeRecord> nodes = await _dbContext.DocumentOutlineNodes.AsNoTracking().Where(n => n.DocumentId == doc.Id).ToListAsync(ct);
            if (outline is not null || nodes.Count > 0)
            {
                await ReplaceOutlineAsync(doc, outline?.Outline ?? string.Empty, nodes, ct);
            }
        }
    }

    private async Task UpsertEntryAsync(SearchIndexEntry entry, CancellationToken ct)
    {
        if (!await EnsureSearchIndexAsync(ct))
        {
            return;
        }

        try
        {
            string normalizedEntityId = IdNorm.Norm(entry.EntityId);
            SearchIndexEntryRecord? existing = await _dbContext.SearchIndexEntries.FirstOrDefaultAsync(e => e.EntityType == entry.EntityType && e.EntityId == normalizedEntityId, ct);
            if (existing is null)
            {
                existing = new SearchIndexEntryRecord { EntityType = entry.EntityType, EntityId = normalizedEntityId };
                _dbContext.SearchIndexEntries.Add(existing);
            }

            existing.DocumentId = IdNorm.Norm(entry.DocumentId);
            existing.ProjectId = IdNorm.Norm(entry.ProjectId);
            existing.SectionId = entry.SectionId.HasValue ? IdNorm.Norm(entry.SectionId.Value) : null;
            existing.PageId = entry.PageId.HasValue ? IdNorm.Norm(entry.PageId.Value) : null;
            existing.Title = entry.Title ?? string.Empty;
            existing.Content = entry.Content ?? string.Empty;
            existing.UpdatedAt = entry.UpdatedAt.ToString("O");
            await _dbContext.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            DisableSearchIndex(ex);
        }
    }

    private async Task<bool> EnsureSearchIndexAsync(CancellationToken ct)
    {
        try
        {
            await _dbContext.SearchIndexEntries.AsNoTracking().Take(1).ToListAsync(ct);
            _disabled = false;
            _disabledReason = null;
            return true;
        }
        catch (Exception ex)
        {
            DisableSearchIndex(ex);
            return false;
        }
    }

    private void DisableSearchIndex(Exception ex)
    {
        _disabled = true;
        _disabledReason = ex.Message;
        _logger.LogWarning(ex, "Search index disabled. Reason={Reason}", _disabledReason);
    }

    private static string NormalizeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string trimmed = value.Trim();
        if ((trimmed.StartsWith("{", StringComparison.Ordinal) || trimmed.StartsWith("[", StringComparison.Ordinal))
            && TryExtractPlainTextFromJson(trimmed, out string jsonText))
        {
            return Regex.Replace(jsonText, "\\s+", " ").Trim();
        }

        string decoded = System.Net.WebUtility.HtmlDecode(value);
        string withoutTags = Regex.Replace(decoded, "<.*?>", " ");
        return Regex.Replace(withoutTags, "\\s+", " ").Trim();
    }

    private static bool TryExtractPlainTextFromJson(string json, out string text)
    {
        text = string.Empty;
        try
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            StringBuilder builder = new();
            AppendJsonText(doc.RootElement, builder);
            text = builder.ToString();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static void AppendJsonText(JsonElement element, StringBuilder builder)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement child in element.EnumerateArray())
            {
                AppendJsonText(child, builder);
            }
            return;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (element.TryGetProperty("type", out JsonElement typeElement)
            && typeElement.ValueKind == JsonValueKind.String
            && string.Equals(typeElement.GetString(), "hardBreak", StringComparison.OrdinalIgnoreCase))
        {
            builder.Append(' ');
        }

        if (element.TryGetProperty("text", out JsonElement textElement) && textElement.ValueKind == JsonValueKind.String)
        {
            builder.Append(textElement.GetString());
            builder.Append(' ');
        }

        if (element.TryGetProperty("content", out JsonElement contentElement))
        {
            AppendJsonText(contentElement, builder);
        }
    }

    private static string BuildSnippet(string title, string content, string query, bool contentMatch)
    {
        string source = contentMatch ? content : string.Join(" ", new[] { title, content });
        int index = source.IndexOf(query, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            string head = source.Length > 120 ? source[..120] + "..." : source;
            return System.Net.WebUtility.HtmlEncode(head);
        }

        int start = Math.Max(0, index - 40);
        int length = Math.Min(source.Length - start, query.Length + 80);
        string snippet = source.Substring(start, length);
        string encoded = System.Net.WebUtility.HtmlEncode(snippet);
        string encodedQuery = System.Net.WebUtility.HtmlEncode(query);
        string highlighted = Regex.Replace(encoded, Regex.Escape(encodedQuery), "<mark>$0</mark>", RegexOptions.IgnoreCase);
        if (start > 0)
        {
            highlighted = "..." + highlighted;
        }
        if (start + length < source.Length)
        {
            highlighted += "...";
        }
        return highlighted;
    }

    private static Guid ParseGuid(string value) => Guid.TryParse(value, out Guid parsed) ? parsed : Guid.Empty;
    private static Guid? ParseNullableGuid(string? value) => string.IsNullOrWhiteSpace(value) ? null : (Guid.TryParse(value, out Guid parsed) ? parsed : null);

    private async Task<Guid> ResolveProjectIdForDocumentAsync(Guid documentId, CancellationToken ct)
    {
        Guid? projectId = await _dbContext.Documents.AsNoTracking().Where(d => d.Id == documentId).Select(d => (Guid?)d.ProjectId).FirstOrDefaultAsync(ct);
        return projectId ?? Guid.Empty;
    }

    private sealed record SearchIndexEntry(string EntityType, Guid EntityId, Guid DocumentId, Guid ProjectId, Guid? SectionId, Guid? PageId, string Title, string Content, DateTimeOffset UpdatedAt);
    private sealed record DocumentLookup(string DocumentId, string Title);
}

public static class SearchEntityTypes
{
    public const string Document = "document";
    public const string Section = "section";
    public const string Page = "page";
    public const string Note = "note";
    public const string SceneCard = "scenecard";
    public const string Outline = "outline";
}
