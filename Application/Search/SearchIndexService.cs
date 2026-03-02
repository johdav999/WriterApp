using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WriterApp.Data;
using WriterApp.Data.Documents;
using WriterApp.Application.Documents;
using Microsoft.Extensions.Logging;

namespace WriterApp.Application.Search
{
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
        private static readonly SemaphoreSlim InitLock = new(1, 1);
        private static readonly SemaphoreSlim _probeLock = new(1, 1);
        private static bool _initialized;
        private static bool _runtimePathLogged;
        private static volatile bool _disabled;
        private static string? _disabledReason;
        private static DateTimeOffset? _disabledUntilUtc;
        private static DateTimeOffset _lastProbeUtc;

        public SearchIndexService(
            AppDbContext dbContext,
            ILogger<SearchIndexService> logger,
            ISearchIndexBackfillQueue backfillQueue)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _backfillQueue = backfillQueue ?? throw new ArgumentNullException(nameof(backfillQueue));
        }

        public string? DisabledReason => _disabledReason;

        public Task UpsertDocumentAsync(DocumentRecord document, CancellationToken ct)
        {
            if (document is null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            return UpsertEntryAsync(new SearchIndexEntry(
                EntityType: SearchEntityTypes.Document,
                EntityId: document.Id,
                DocumentId: document.Id,
                ProjectId: document.ProjectId,
                SectionId: null,
                PageId: null,
                Title: document.Title ?? string.Empty,
                Content: document.Title ?? string.Empty,
                UpdatedAt: document.UpdatedAt), ct);
        }

        public Task UpsertSectionAsync(SectionRecord section, CancellationToken ct)
        {
            if (section is null)
            {
                throw new ArgumentNullException(nameof(section));
            }
            
            return UpsertSectionCoreAsync(section, ct);
        }

        private async Task UpsertSectionCoreAsync(SectionRecord section, CancellationToken ct)
        {
            Guid projectId = section.Document?.ProjectId ?? await ResolveProjectIdForDocumentAsync(section.DocumentId, ct);
            await UpsertEntryAsync(new SearchIndexEntry(
                EntityType: SearchEntityTypes.Section,
                EntityId: section.Id,
                DocumentId: section.DocumentId,
                ProjectId: projectId,
                SectionId: section.Id,
                PageId: null,
                Title: section.Title ?? string.Empty,
                Content: section.Title ?? string.Empty,
                UpdatedAt: section.UpdatedAt), ct);
        }

        public Task UpsertPageAsync(PageRecord page, CancellationToken ct)
        {
            if (page is null)
            {
                throw new ArgumentNullException(nameof(page));
            }
            
            return UpsertPageCoreAsync(page, ct);
        }

        private async Task UpsertPageCoreAsync(PageRecord page, CancellationToken ct)
        {
            string content = NormalizeText(page.Content);
            Guid projectId = page.Document?.ProjectId ?? await ResolveProjectIdForDocumentAsync(page.DocumentId, ct);
            await UpsertEntryAsync(new SearchIndexEntry(
                EntityType: SearchEntityTypes.Page,
                EntityId: page.Id,
                DocumentId: page.DocumentId,
                ProjectId: projectId,
                SectionId: page.SectionId,
                PageId: page.Id,
                Title: page.Title ?? string.Empty,
                Content: content,
                UpdatedAt: page.UpdatedAt), ct);
        }

        public Task UpsertPageNotesAsync(PageRecord page, PageNoteRecord notes, CancellationToken ct)
        {
            if (page is null)
            {
                throw new ArgumentNullException(nameof(page));
            }

            if (notes is null)
            {
                throw new ArgumentNullException(nameof(notes));
            }
            
            return UpsertPageNotesCoreAsync(page, notes, ct);
        }

        private async Task UpsertPageNotesCoreAsync(PageRecord page, PageNoteRecord notes, CancellationToken ct)
        {
            string content = NormalizeText(notes.Notes);
            Guid projectId = page.Document?.ProjectId ?? await ResolveProjectIdForDocumentAsync(page.DocumentId, ct);
            await UpsertEntryAsync(new SearchIndexEntry(
                EntityType: SearchEntityTypes.Note,
                EntityId: notes.PageId,
                DocumentId: page.DocumentId,
                ProjectId: projectId,
                SectionId: page.SectionId,
                PageId: page.Id,
                Title: $"Notes: {page.Title ?? "Page"}",
                Content: content,
                UpdatedAt: notes.UpdatedAt), ct);
        }

        public Task UpsertSceneCardAsync(SectionRecord section, SectionSceneCardRecord card, CancellationToken ct)
        {
            if (section is null)
            {
                throw new ArgumentNullException(nameof(section));
            }

            if (card is null)
            {
                throw new ArgumentNullException(nameof(card));
            }

            return UpsertSceneCardCoreAsync(section, card, ct);
        }

        private async Task UpsertSceneCardCoreAsync(SectionRecord section, SectionSceneCardRecord card, CancellationToken ct)
        {
            string content = string.Join("\n", new[]
            {
                card.NarrativePurpose,
                card.EmotionalBeat,
                card.KeyEvents,
                card.OpenQuestions
            }.Where(value => !string.IsNullOrWhiteSpace(value)));

            Guid projectId = section.Document?.ProjectId ?? await ResolveProjectIdForDocumentAsync(section.DocumentId, ct);
            await UpsertEntryAsync(new SearchIndexEntry(
                EntityType: SearchEntityTypes.SceneCard,
                EntityId: card.SectionId,
                DocumentId: section.DocumentId,
                ProjectId: projectId,
                SectionId: section.Id,
                PageId: null,
                Title: $"Scene card: {section.Title ?? "Section"}",
                Content: NormalizeText(content),
                UpdatedAt: card.UpdatedUtc), ct);
        }

        public async Task ReplaceOutlineAsync(
            DocumentRecord document,
            string outlineText,
            IReadOnlyList<DocumentOutlineNodeRecord> nodes,
            CancellationToken ct)
        {
            if (document is null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            await DeleteOutlineEntriesAsync(document.Id, ct);

            if (!string.IsNullOrWhiteSpace(outlineText))
            {
                await UpsertEntryAsync(new SearchIndexEntry(
                    EntityType: SearchEntityTypes.Outline,
                    EntityId: document.Id,
                    DocumentId: document.Id,
                    ProjectId: document.ProjectId,
                    SectionId: null,
                    PageId: null,
                    Title: $"Outline: {document.Title ?? "Document"}",
                    Content: NormalizeText(outlineText),
                    UpdatedAt: document.UpdatedAt), ct);
            }

            if (nodes is null || nodes.Count == 0)
            {
                return;
            }

            foreach (DocumentOutlineNodeRecord node in nodes)
            {
                string content = string.Join("\n", new[]
                {
                    node.Title,
                    node.Notes
                }.Where(value => !string.IsNullOrWhiteSpace(value)));

                await UpsertEntryAsync(new SearchIndexEntry(
                    EntityType: SearchEntityTypes.Outline,
                    EntityId: node.Id,
                    DocumentId: document.Id,
                    ProjectId: document.ProjectId,
                    SectionId: node.LinkedSectionId,
                    PageId: null,
                    Title: $"Outline: {node.Title}",
                    Content: NormalizeText(content),
                    UpdatedAt: document.UpdatedAt), ct);
            }
        }

        public Task DeleteByEntityAsync(string entityType, Guid entityId, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(entityType))
            {
                throw new ArgumentException("entityType is required.", nameof(entityType));
            }

            return DeleteEntryAsync(entityType, entityId, ct);
        }

        public async Task<IReadOnlyList<SearchResultDto>> SearchAsync(
            string userId,
            Guid projectId,
            string query,
            bool includeMeta,
            int limit,
            string? correlationId,
            CancellationToken ct)
        {
            using IDisposable? scope = _logger.BeginScope(new Dictionary<string, object?>
            {
                ["SearchUserId"] = userId,
                ["IncludeMeta"] = includeMeta,
                ["Limit"] = limit,
                ["CorrelationId"] = correlationId
            });

            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new ArgumentException("userId is required.", nameof(userId));
            }

            userId = userId.Trim();
            if (IdNorm.TryNormGuidString(userId, out string normalizedUserId))
            {
                userId = normalizedUserId;
            }
            if (projectId == Guid.Empty)
            {
                throw new ArgumentException("projectId is required.", nameof(projectId));
            }

            string normalizedQuery = NormalizeText(query);
            string projectIdLower = IdNorm.Norm(projectId);
            _logger.LogDebug("Search normalized query. RawLength={RawLength} Normalized='{NormalizedQuery}'. ProjectId={ProjectId}",
                query?.Length ?? 0,
                normalizedQuery,
                projectIdLower);
            if (string.IsNullOrWhiteSpace(normalizedQuery))
            {
                _logger.LogDebug("Search skipped: normalized query was empty.");
                return Array.Empty<SearchResultDto>();
            }

            int clampedLimit = Math.Clamp(limit, 1, 200);
            if (clampedLimit != limit)
            {
                _logger.LogDebug("Search limit clamped. Requested={Requested} Clamped={Clamped}.", limit, clampedLimit);
            }

            _logger.LogDebug("Search ensure index.");
            if (!await EnsureSearchIndexAsync(ct))
            {
                _logger.LogWarning("Search aborted: index not available. Disabled={Disabled} Reason={Reason}.",
                    _disabled,
                    _disabledReason);
                return Array.Empty<SearchResultDto>();
            }

            _logger.LogDebug("Search check existing entries for user.");
            bool hasUserEntries = await HasEntriesForUserProjectAsync(userId, projectId, ct);
            if (!hasUserEntries)
            {
                bool enqueued = _backfillQueue.Enqueue(userId);
                _logger.LogInformation(enqueued
                    ? "BACKFILL_START queued for user."
                    : "BACKFILL_ALREADY_RUNNING backfill already queued or in progress for user.");

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

                hasUserEntries = await HasEntriesForUserProjectAsync(userId, projectId, ct);
                if (!hasUserEntries)
                {
                    return Array.Empty<SearchResultDto>();
                }
            }
            else
            {
                _logger.LogDebug("Search index entries found for user.");
            }

            string sql = @"
SELECT
    e.EntityType,
    e.EntityId,
    e.DocumentId,
    e.SectionId,
    e.PageId,
    e.Title,
    d.Title as DocumentTitle,
    e.Content
FROM SearchIndexEntries e
JOIN Documents d ON (
    lower(d.Id) = lower(e.DocumentId)
)
WHERE d.OwnerUserId = $userId
  AND lower(e.ProjectId) = $projectId
  AND (
        (lower(e.EntityType) = 'page' AND lower(e.Content) LIKE '%' || lower($query) || '%')
        OR (
            $includeMeta = 1
            AND lower(e.EntityType) <> 'page'
            AND (
                lower(e.Title) LIKE '%' || lower($query) || '%'
                OR lower(e.Content) LIKE '%' || lower($query) || '%'
            )
        )
    )
ORDER BY
    CASE
        WHEN lower(e.EntityType) = 'page' THEN 0
        WHEN lower(e.EntityType) IN ('document', 'section') THEN 1
        WHEN lower(e.EntityType) = 'note' THEN 2
        WHEN lower(e.EntityType) = 'scenecard' THEN 3
        WHEN lower(e.EntityType) = 'outline' THEN 4
        ELSE 5
    END,
    e.UpdatedAt DESC
LIMIT $limit;
";

            try
            {
                _logger.LogDebug("Search execute SQL.");
                List<SearchResultDto> results = new();
                await using var connection = _dbContext.Database.GetDbConnection();
                if (connection.State != ConnectionState.Open)
                {
                    await connection.OpenAsync(ct);
                }

                await using var command = connection.CreateCommand();
                command.CommandText = sql;
                AddParameter(command, "$query", normalizedQuery);
                AddParameter(command, "$userId", userId);
                AddParameter(command, "$projectId", IdNorm.Norm(projectId));
                AddParameter(command, "$includeMeta", includeMeta ? 1 : 0);
                AddParameter(command, "$limit", clampedLimit);

                await using var reader = await command.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    string entityType = reader.GetString(0);
                    string entityId = reader.GetString(1);
                    Guid documentId = ParseGuid(reader.GetString(2));
                    Guid? sectionId = ParseNullableGuid(reader.IsDBNull(3) ? null : reader.GetString(3));
                    Guid? pageId = ParseNullableGuid(reader.IsDBNull(4) ? null : reader.GetString(4));
                    string title = reader.IsDBNull(5) ? string.Empty : reader.GetString(5);
                    string documentTitle = reader.IsDBNull(6) ? string.Empty : reader.GetString(6);
                    string content = reader.IsDBNull(7) ? string.Empty : reader.GetString(7);
                    bool isContentMatch = string.Equals(entityType, SearchEntityTypes.Page, StringComparison.OrdinalIgnoreCase);
                    string snippet = BuildSnippet(title, content, normalizedQuery, isContentMatch);
                    string matchKind = isContentMatch ? "content" : "meta";

                    results.Add(new SearchResultDto(
                        DocumentId: documentId,
                        SectionId: sectionId,
                        PageId: pageId,
                        EntityType: entityType,
                        EntityId: entityId,
                        Title: title,
                        Snippet: snippet,
                        Score: isContentMatch ? 0 : 1,
                        DocumentTitle: documentTitle,
                        MatchKind: matchKind));
                }

                _logger.LogDebug("Search complete. ResultCount={ResultCount}.", results.Count);
                if (results.Count == 0)
                {
                    long entryCount = await CountEntriesForUserProjectAsync(userId, projectId, ct);
                    _logger.LogDebug("Search returned 0 results. UserEntryCount={UserEntryCount}.", entryCount);
                }
                else
                {
                    int contentCount = results.Count(result => string.Equals(result.MatchKind, "content", StringComparison.OrdinalIgnoreCase));
                    int metaCount = results.Count - contentCount;
                    _logger.LogDebug("Search category counts. Content={ContentCount} Meta={MetaCount}", contentCount, metaCount);
                }
                return results;
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Search canceled.");
                return Array.Empty<SearchResultDto>();
            }
            catch (SqliteException ex) when (IsCorrupt(ex))
            {
                DisableSearchIndex(ex);
                return Array.Empty<SearchResultDto>();
            }
        }

        public async Task<int> GetProjectEntryCountAsync(string ownerUserId, Guid projectId, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(ownerUserId) || projectId == Guid.Empty)
            {
                return 0;
            }

            if (!await EnsureSearchIndexAsync(ct))
            {
                return 0;
            }

            await using var connection = _dbContext.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync(ct);
            }

            await using var command = connection.CreateCommand();
            command.CommandText = @"
SELECT COUNT(*)
FROM SearchIndexEntries e
JOIN Documents d ON lower(d.Id) = lower(e.DocumentId)
WHERE lower(e.ProjectId) = $projectId
  AND d.OwnerUserId = $ownerUserId;
";
            AddParameter(command, "$projectId", IdNorm.Norm(projectId));
            AddParameter(command, "$ownerUserId", ownerUserId);

            object? result = await command.ExecuteScalarAsync(ct);
            return result is null || result == DBNull.Value ? 0 : Convert.ToInt32(result);
        }

        public async Task RebuildProjectIndexAsync(string ownerUserId, Guid projectId, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(ownerUserId))
            {
                throw new ArgumentException("ownerUserId is required.", nameof(ownerUserId));
            }

            if (projectId == Guid.Empty)
            {
                throw new ArgumentException("projectId is required.", nameof(projectId));
            }

            if (!await EnsureSearchIndexAsync(ct))
            {
                throw new InvalidOperationException("Search index is not available.");
            }

            string normalizedProjectId = IdNorm.Norm(projectId);

            await using var connection = _dbContext.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync(ct);
            }

            await using (var tx = await connection.BeginTransactionAsync(ct))
            {
                List<long> existingIds = new();
                await using (var idCommand = connection.CreateCommand())
                {
                    idCommand.Transaction = tx;
                    idCommand.CommandText = @"
SELECT e.Id
FROM SearchIndexEntries e
JOIN Documents d ON lower(d.Id) = lower(e.DocumentId)
WHERE lower(e.ProjectId) = $projectId
  AND d.OwnerUserId = $ownerUserId;";
                    AddParameter(idCommand, "$projectId", normalizedProjectId);
                    AddParameter(idCommand, "$ownerUserId", ownerUserId);
                    await using var reader = await idCommand.ExecuteReaderAsync(ct);
                    while (await reader.ReadAsync(ct))
                    {
                        existingIds.Add(reader.GetInt64(0));
                    }
                }

                foreach (long id in existingIds)
                {
                    await DeleteFtsAsync(connection, tx, id, ct);
                }

                await using (var deleteEntries = connection.CreateCommand())
                {
                    deleteEntries.Transaction = tx;
                    deleteEntries.CommandText = @"
DELETE FROM SearchIndexEntries
WHERE ProjectId = $projectId
  AND EXISTS (
      SELECT 1
      FROM Documents d
      WHERE lower(d.Id) = lower(SearchIndexEntries.DocumentId)
        AND d.OwnerUserId = $ownerUserId
  );";
                    AddParameter(deleteEntries, "$projectId", normalizedProjectId);
                    AddParameter(deleteEntries, "$ownerUserId", ownerUserId);
                    await deleteEntries.ExecuteNonQueryAsync(ct);
                }

                await tx.CommitAsync(ct);
            }

            List<DocumentRecord> documents = await _dbContext.Documents
                .AsNoTracking()
                .Where(d => d.OwnerUserId == ownerUserId && d.ProjectId == projectId)
                .ToListAsync(ct);

            if (documents.Count == 0)
            {
                _logger.LogInformation(
                    "Search project rebuild completed with no documents. OwnerUserId={OwnerUserId} ProjectId={ProjectId}",
                    ownerUserId,
                    normalizedProjectId);
                return;
            }

            HashSet<Guid> documentIds = documents.Select(d => d.Id).ToHashSet();
            List<PageRecord> pages = await _dbContext.Pages
                .AsNoTracking()
                .Where(p => documentIds.Contains(p.DocumentId))
                .ToListAsync(ct);

            Dictionary<Guid, DocumentRecord> docsById = documents.ToDictionary(d => d.Id);
            const int batchSize = 200;
            int inserted = 0;
            System.Data.Common.DbTransaction transaction = await connection.BeginTransactionAsync(ct);
            try
            {
                foreach (PageRecord page in pages)
                {
                    if (!docsById.TryGetValue(page.DocumentId, out DocumentRecord? doc))
                    {
                        continue;
                    }

                    SearchIndexEntry entry = new(
                        EntityType: "Page",
                        EntityId: page.Id,
                        DocumentId: page.DocumentId,
                        ProjectId: doc.ProjectId,
                        SectionId: page.SectionId,
                        PageId: page.Id,
                        Title: string.IsNullOrWhiteSpace(page.Title) ? "Page" : page.Title,
                        Content: NormalizeText(string.Concat(page.Title ?? "Page", "\n", page.Content ?? string.Empty)),
                        UpdatedAt: page.UpdatedAt);

                    long id = await InsertEntryAsync(connection, transaction, entry, ct);
                    await InsertFtsAsync(connection, transaction, id, entry, ct);
                    inserted++;

                    if (inserted % batchSize == 0)
                    {
                        await transaction.CommitAsync(ct);
                        await transaction.DisposeAsync();
                        transaction = await connection.BeginTransactionAsync(ct);
                    }
                }

                await transaction.CommitAsync(ct);
            }
            finally
            {
                await transaction.DisposeAsync();
            }

            _logger.LogInformation(
                "Search project rebuild completed. OwnerUserId={OwnerUserId} ProjectId={ProjectId} Entries={Entries}",
                ownerUserId,
                normalizedProjectId,
                inserted);
        }

        private async Task UpsertEntryAsync(SearchIndexEntry entry, CancellationToken ct)
        {
            if (!await EnsureSearchIndexAsync(ct))
            {
                return;
            }

            try
            {
                await using var connection = _dbContext.Database.GetDbConnection();
                if (connection.State != ConnectionState.Open)
                {
                    await connection.OpenAsync(ct);
                }

                await using var transaction = await connection.BeginTransactionAsync(ct);
                long? existingId = await TryGetEntryIdAsync(connection, transaction, entry.EntityType, IdNorm.Norm(entry.EntityId), ct);
                if (existingId.HasValue)
                {
                    await UpdateEntryAsync(connection, transaction, existingId.Value, entry, ct);
                }
                else
                {
                    long newId = await InsertEntryAsync(connection, transaction, entry, ct);
                    await InsertFtsAsync(connection, transaction, newId, entry, ct);
                }

                await transaction.CommitAsync(ct);
            }
            catch (SqliteException ex) when (IsCorrupt(ex))
            {
                DisableSearchIndex(ex);
            }
        }

        private async Task DeleteEntryAsync(string entityType, Guid entityId, CancellationToken ct)
        {
            if (!await EnsureSearchIndexAsync(ct))
            {
                return;
            }

            try
            {
                await using var connection = _dbContext.Database.GetDbConnection();
                if (connection.State != ConnectionState.Open)
                {
                    await connection.OpenAsync(ct);
                }

                await using var transaction = await connection.BeginTransactionAsync(ct);
                long? existingId = await TryGetEntryIdAsync(connection, transaction, entityType, IdNorm.Norm(entityId), ct);
                if (!existingId.HasValue)
                {
                    await transaction.CommitAsync(ct);
                    return;
                }

                await DeleteFtsAsync(connection, transaction, existingId.Value, ct);
                await DeleteEntryRowAsync(connection, transaction, existingId.Value, ct);
                await transaction.CommitAsync(ct);
            }
            catch (SqliteException ex) when (IsCorrupt(ex))
            {
                DisableSearchIndex(ex);
            }
        }

        private async Task DeleteOutlineEntriesAsync(Guid documentId, CancellationToken ct)
        {
            if (!await EnsureSearchIndexAsync(ct))
            {
                return;
            }

            try
            {
                await using var connection = _dbContext.Database.GetDbConnection();
                if (connection.State != ConnectionState.Open)
                {
                    await connection.OpenAsync(ct);
                }

                await using var transaction = await connection.BeginTransactionAsync(ct);
                await using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = @"
SELECT Id
FROM SearchIndexEntries
WHERE EntityType = 'outline' AND lower(DocumentId) = $documentId;
";
                AddParameter(command, "$documentId", IdNorm.Norm(documentId));

                List<long> ids = new();
                await using (var reader = await command.ExecuteReaderAsync(ct))
                {
                    while (await reader.ReadAsync(ct))
                    {
                        ids.Add(reader.GetInt64(0));
                    }
                }

                foreach (long id in ids)
                {
                    await DeleteFtsAsync(connection, transaction, id, ct);
                    await DeleteEntryRowAsync(connection, transaction, id, ct);
                }

                await transaction.CommitAsync(ct);
            }
            catch (SqliteException ex) when (IsCorrupt(ex))
            {
                DisableSearchIndex(ex);
            }
        }

        private static async Task<long?> TryGetEntryIdAsync(
            System.Data.Common.DbConnection connection,
            System.Data.Common.DbTransaction transaction,
            string entityType,
            string entityId,
            CancellationToken ct)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = @"
SELECT Id
FROM SearchIndexEntries
WHERE EntityType = $entityType AND EntityId = $entityId
LIMIT 1;
";
            AddParameter(command, "$entityType", entityType);
            AddParameter(command, "$entityId", entityId);
            object? result = await command.ExecuteScalarAsync(ct);
            if (result is null || result == DBNull.Value)
            {
                return null;
            }

            return Convert.ToInt64(result);
        }

        private static async Task<long> InsertEntryAsync(
            System.Data.Common.DbConnection connection,
            System.Data.Common.DbTransaction transaction,
            SearchIndexEntry entry,
            CancellationToken ct)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = @"
INSERT INTO SearchIndexEntries (
    EntityType,
    EntityId,
    DocumentId,
    ProjectId,
    SectionId,
    PageId,
    Title,
    Content,
    UpdatedAt
)
VALUES (
    $entityType,
    $entityId,
    $documentId,
    $projectId,
    $sectionId,
    $pageId,
    $title,
    $content,
    $updatedAt
);
SELECT last_insert_rowid();
";
            AddParameter(command, "$entityType", entry.EntityType);
            AddParameter(command, "$entityId", IdNorm.Norm(entry.EntityId));
            AddParameter(command, "$documentId", IdNorm.Norm(entry.DocumentId));
            AddParameter(command, "$projectId", IdNorm.Norm(entry.ProjectId));
            AddParameter(command, "$sectionId", entry.SectionId.HasValue ? IdNorm.Norm(entry.SectionId.Value) : null);
            AddParameter(command, "$pageId", entry.PageId.HasValue ? IdNorm.Norm(entry.PageId.Value) : null);
            AddParameter(command, "$title", entry.Title ?? string.Empty);
            AddParameter(command, "$content", entry.Content ?? string.Empty);
            AddParameter(command, "$updatedAt", entry.UpdatedAt.ToString("O"));

            object? result = await command.ExecuteScalarAsync(ct);
            return Convert.ToInt64(result);
        }

        private static async Task UpdateEntryAsync(
            System.Data.Common.DbConnection connection,
            System.Data.Common.DbTransaction transaction,
            long id,
            SearchIndexEntry entry,
            CancellationToken ct)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = @"
UPDATE SearchIndexEntries
SET
    DocumentId = $documentId,
    ProjectId = $projectId,
    SectionId = $sectionId,
    PageId = $pageId,
    Title = $title,
    Content = $content,
    UpdatedAt = $updatedAt
WHERE Id = $id;
";
            AddParameter(command, "$documentId", IdNorm.Norm(entry.DocumentId));
            AddParameter(command, "$projectId", IdNorm.Norm(entry.ProjectId));
            AddParameter(command, "$sectionId", entry.SectionId.HasValue ? IdNorm.Norm(entry.SectionId.Value) : null);
            AddParameter(command, "$pageId", entry.PageId.HasValue ? IdNorm.Norm(entry.PageId.Value) : null);
            AddParameter(command, "$title", entry.Title ?? string.Empty);
            AddParameter(command, "$content", entry.Content ?? string.Empty);
            AddParameter(command, "$updatedAt", entry.UpdatedAt.ToString("O"));
            AddParameter(command, "$id", id);
            await command.ExecuteNonQueryAsync(ct);

            await DeleteFtsAsync(connection, transaction, id, ct);
            await InsertFtsAsync(connection, transaction, id, entry, ct);
        }

        private static async Task InsertFtsAsync(
            System.Data.Common.DbConnection connection,
            System.Data.Common.DbTransaction transaction,
            long id,
            SearchIndexEntry entry,
            CancellationToken ct)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = @"
INSERT INTO SearchIndexFts (
    rowid,
    Title,
    Content,
    EntityType,
    EntityId,
    DocumentId,
    SectionId,
    PageId
)
VALUES (
    $id,
    $title,
    $content,
    $entityType,
    $entityId,
    $documentId,
    $sectionId,
    $pageId
);
";
            AddParameter(command, "$id", id);
            AddParameter(command, "$title", entry.Title ?? string.Empty);
            AddParameter(command, "$content", entry.Content ?? string.Empty);
            AddParameter(command, "$entityType", entry.EntityType);
            AddParameter(command, "$entityId", IdNorm.Norm(entry.EntityId));
            AddParameter(command, "$documentId", IdNorm.Norm(entry.DocumentId));
            AddParameter(command, "$sectionId", entry.SectionId.HasValue ? IdNorm.Norm(entry.SectionId.Value) : null);
            AddParameter(command, "$pageId", entry.PageId.HasValue ? IdNorm.Norm(entry.PageId.Value) : null);
            await command.ExecuteNonQueryAsync(ct);
        }

        private static async Task DeleteEntryRowAsync(
            System.Data.Common.DbConnection connection,
            System.Data.Common.DbTransaction transaction,
            long id,
            CancellationToken ct)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "DELETE FROM SearchIndexEntries WHERE Id = $id;";
            AddParameter(command, "$id", id);
            await command.ExecuteNonQueryAsync(ct);
        }

        private static async Task DeleteFtsAsync(
            System.Data.Common.DbConnection connection,
            System.Data.Common.DbTransaction transaction,
            long id,
            CancellationToken ct)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "DELETE FROM SearchIndexFts WHERE rowid = $id;";
            AddParameter(command, "$id", id);
            await command.ExecuteNonQueryAsync(ct);
        }

        private static void AddParameter(System.Data.Common.DbCommand command, string name, object? value)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }

        private static string NormalizeText(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string trimmed = value.Trim();
            if (LooksLikeJson(trimmed) && TryExtractPlainTextFromJson(trimmed, out string jsonText))
            {
                return NormalizeWhitespace(jsonText);
            }

            string decoded = System.Net.WebUtility.HtmlDecode(value);
            string withoutTags = Regex.Replace(decoded, "<.*?>", " ");
            return NormalizeWhitespace(withoutTags);
        }

        private static string BuildSnippet(string title, string content, string query, bool contentMatch)
        {
            string source = contentMatch ? content : string.Join(" ", new[] { title, content });
            if (string.IsNullOrWhiteSpace(source))
            {
                return string.Empty;
            }

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
            string highlighted = Regex.Replace(
                encoded,
                Regex.Escape(encodedQuery),
                "<mark>$0</mark>",
                RegexOptions.IgnoreCase);

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

        private static bool LooksLikeJson(string value)
        {
            return value.StartsWith("{", StringComparison.Ordinal) || value.StartsWith("[", StringComparison.Ordinal);
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
            switch (element.ValueKind)
            {
                case JsonValueKind.Array:
                    foreach (JsonElement child in element.EnumerateArray())
                    {
                        AppendJsonText(child, builder);
                    }
                    return;
                case JsonValueKind.Object:
                    string? nodeType = null;
                    if (element.TryGetProperty("type", out JsonElement typeElement) &&
                        typeElement.ValueKind == JsonValueKind.String)
                    {
                        nodeType = typeElement.GetString();
                    }

                    if (string.Equals(nodeType, "hardBreak", StringComparison.OrdinalIgnoreCase))
                    {
                        builder.Append(' ');
                    }

                    if (element.TryGetProperty("text", out JsonElement textElement) &&
                        textElement.ValueKind == JsonValueKind.String)
                    {
                        builder.Append(textElement.GetString());
                        builder.Append(' ');
                    }

                    if (element.TryGetProperty("content", out JsonElement contentElement))
                    {
                        AppendJsonText(contentElement, builder);
                    }
                    return;
            }
        }

        private static string NormalizeWhitespace(string value)
        {
            return Regex.Replace(value, "\\s+", " ").Trim();
        }

        private static string NormalizeQuery(string? query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return string.Empty;
            }

            List<string> tokens = new();
            foreach (Match match in Regex.Matches(query, "\"[^\"]+\"|\\S+"))
            {
                string token = match.Value.Trim();
                if (token.Length == 0)
                {
                    continue;
                }

                if (token.StartsWith("\"", StringComparison.Ordinal) && token.EndsWith("\"", StringComparison.Ordinal))
                {
                    token = token[1..^1];
                }

                token = token.Replace("\"", "\"\"");
                if (token.Length == 0)
                {
                    continue;
                }

                tokens.Add($"\"{token}\"");
            }

            return string.Join(" ", tokens);
        }

        private static Guid ParseGuid(string value)
        {
            return Guid.TryParse(value, out Guid parsed) ? parsed : Guid.Empty;
        }

        private static Guid? ParseNullableGuid(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return Guid.TryParse(value, out Guid parsed) ? parsed : null;
        }

        private async Task<bool> EnsureSearchIndexAsync(CancellationToken ct)
        {
            if (_disabled)
            {
                return false;
            }

            if (_initialized)
            {
                return true;
            }

            try
            {
                await InitLock.WaitAsync(ct);
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            try
            {
                if (_disabled)
                {
                    return false;
                }

                if (_initialized)
                {
                    return true;
                }

                await using var connection = _dbContext.Database.GetDbConnection();
                if (connection.State != ConnectionState.Open)
                {
                    await connection.OpenAsync(ct);
                }

                await LogDatabasePathOnceAsync(connection, ct);

                await using var command = connection.CreateCommand();
                command.CommandText = @"
CREATE TABLE IF NOT EXISTS SearchIndexEntries (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    EntityType TEXT NOT NULL,
    EntityId TEXT NOT NULL,
    DocumentId TEXT NOT NULL,
    ProjectId TEXT NOT NULL DEFAULT '',
    SectionId TEXT NULL,
    PageId TEXT NULL,
    Title TEXT NOT NULL DEFAULT '',
    Content TEXT NOT NULL DEFAULT '',
    UpdatedAt TEXT NOT NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS IX_SearchIndexEntries_Entity
ON SearchIndexEntries (EntityType, EntityId);
CREATE INDEX IF NOT EXISTS IX_SearchIndexEntries_Document
ON SearchIndexEntries (DocumentId);
CREATE INDEX IF NOT EXISTS IX_SearchIndexEntries_ProjectId
ON SearchIndexEntries (ProjectId);
CREATE VIRTUAL TABLE IF NOT EXISTS SearchIndexFts
USING fts5(
    Title,
    Content,
    EntityType UNINDEXED,
    EntityId UNINDEXED,
    DocumentId UNINDEXED,
    SectionId UNINDEXED,
    PageId UNINDEXED,
    content='SearchIndexEntries',
    content_rowid='Id'
);
";
                try
                {
                    await command.ExecuteNonQueryAsync(ct);
                    await EnsureProjectIdColumnAsync(connection, ct);
                    _initialized = true;
                    return true;
                }
                catch (OperationCanceledException)
                {
                    return false;
                }
                catch (SqliteException ex) when (IsCorrupt(ex))
                {
                    DisableSearchIndex(ex);
                    return false;
                }
            }
            finally
            {
                InitLock.Release();
            }
        }

        private static bool IsCorrupt(SqliteException ex)
        {
            return ex.SqliteErrorCode == 11;
        }

        private void DisableSearchIndex(SqliteException ex)
        {
            _disabled = true;
            _disabledReason = ex.Message;
            _disabledUntilUtc = DateTimeOffset.UtcNow.AddSeconds(30);
            _logger.LogWarning(
                ex,
                "Search index disabled temporarily: {Reason}. RetryAfterUtc={RetryAfterUtc}",
                _disabledReason,
                _disabledUntilUtc);
        }

        public async Task<bool> TryProbeAndRecoverAsync(CancellationToken ct = default)
        {
            if (!_disabled)
            {
                return true;
            }

            DateTimeOffset now = DateTimeOffset.UtcNow;
            if (_disabledUntilUtc.HasValue && now < _disabledUntilUtc.Value)
            {
                return false;
            }

            await _probeLock.WaitAsync(ct);
            try
            {
                if (!_disabled)
                {
                    return true;
                }

                now = DateTimeOffset.UtcNow;
                if (_disabledUntilUtc.HasValue && now < _disabledUntilUtc.Value)
                {
                    return false;
                }

                _lastProbeUtc = now;
                try
                {
                    await using var connection = _dbContext.Database.GetDbConnection();
                    if (connection.State != ConnectionState.Open)
                    {
                        await connection.OpenAsync(ct);
                    }

                    await using (var quickCheck = connection.CreateCommand())
                    {
                        quickCheck.CommandText = "PRAGMA quick_check;";
                        await quickCheck.ExecuteScalarAsync(ct);
                    }

                    _initialized = false;
                    if (!await EnsureSearchIndexAsync(ct))
                    {
                        _disabled = true;
                        _disabledUntilUtc = DateTimeOffset.UtcNow.AddSeconds(60);
                        _logger.LogWarning(
                            "Search index probe failed to initialize. DisabledReason={Reason} RetryAfterUtc={RetryAfterUtc}",
                            _disabledReason,
                            _disabledUntilUtc);
                        return false;
                    }

                    await using (var ftsProbe = connection.CreateCommand())
                    {
                        ftsProbe.CommandText = "SELECT rowid FROM SearchIndexFts LIMIT 1;";
                        await ftsProbe.ExecuteScalarAsync(ct);
                    }

                    _disabled = false;
                    _disabledReason = null;
                    _disabledUntilUtc = null;
                    _logger.LogInformation("Search index recovered successfully. LastProbeUtc={LastProbeUtc}", _lastProbeUtc);
                    return true;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _disabled = true;
                    _disabledReason = ex.Message;
                    _disabledUntilUtc = DateTimeOffset.UtcNow.AddSeconds(60);
                    _logger.LogWarning(
                        ex,
                        "Search index probe failed. DisabledReason={Reason} RetryAfterUtc={RetryAfterUtc}",
                        _disabledReason,
                        _disabledUntilUtc);
                    return false;
                }
            }
            finally
            {
                _probeLock.Release();
            }
        }

        public async Task RebuildSearchIndexAsync(CancellationToken ct = default)
        {
            await using var connection = _dbContext.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync(ct);
            }

            await using (var drop = connection.CreateCommand())
            {
                drop.CommandText = @"
DROP TABLE IF EXISTS SearchIndexFts;
DROP TABLE IF EXISTS SearchIndexEntries;";
                await drop.ExecuteNonQueryAsync(ct);
            }

            _initialized = false;
            _disabled = false;
            _disabledReason = null;
            _disabledUntilUtc = null;

            if (!await EnsureSearchIndexAsync(ct))
            {
                throw new InvalidOperationException($"Search index rebuild failed during initialization: {_disabledReason}");
            }

            List<string> userIds = await _dbContext.Documents
                .AsNoTracking()
                .Where(d => !string.IsNullOrWhiteSpace(d.OwnerUserId))
                .Select(d => d.OwnerUserId)
                .Distinct()
                .ToListAsync(ct);

            foreach (string userId in userIds)
            {
                await BackfillUserAsync(userId, ct);
            }

            _logger.LogInformation("Search index rebuild completed. UserCount={UserCount}", userIds.Count);
        }

        private async Task LogDatabasePathOnceAsync(System.Data.Common.DbConnection connection, CancellationToken ct)
        {
            if (_runtimePathLogged)
            {
                return;
            }

            string dataSource = connection.DataSource ?? string.Empty;
            string mainFile = string.Empty;
            try
            {
                await using var pragmaCommand = connection.CreateCommand();
                pragmaCommand.CommandText = "PRAGMA database_list;";
                await using var reader = await pragmaCommand.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    string name = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                    if (!string.Equals(name, "main", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    mainFile = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
                    break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Search index could not read PRAGMA database_list.");
            }

            _runtimePathLogged = true;
            _logger.LogInformation(
                "Search index runtime DB path. DataSource={DataSource} MainFile={MainFile}",
                dataSource,
                string.IsNullOrWhiteSpace(mainFile) ? "(unknown)" : mainFile);
        }

        private async Task<bool> HasEntriesForUserProjectAsync(string userId, Guid projectId, CancellationToken ct)
        {
            try
            {
                await using var connection = _dbContext.Database.GetDbConnection();
                if (connection.State != ConnectionState.Open)
                {
                    await connection.OpenAsync(ct);
                }

                _logger.LogDebug("Search entry check using DbConnection. Type={Type} DataSource={DataSource}.",
                    connection.GetType().Name,
                    connection.DataSource);

                await using var command = connection.CreateCommand();
                command.CommandText = @"
SELECT 1
FROM SearchIndexEntries e
JOIN Documents d ON (
    lower(d.Id) = lower(e.DocumentId)
)
WHERE d.OwnerUserId = $userId
  AND lower(e.ProjectId) = $projectId
LIMIT 1;
";
                AddParameter(command, "$userId", userId);
                AddParameter(command, "$projectId", IdNorm.Norm(projectId));
                object? result = await command.ExecuteScalarAsync(ct);
                if (result is null || result == DBNull.Value)
                {
                    _logger.LogDebug("Search entry check found no rows for user.");
                }
                return result is not null && result != DBNull.Value;
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Search entry check canceled.");
                return false;
            }
        }

        public async Task BackfillUserAsync(string userId, CancellationToken ct)
        {
            _logger.LogInformation("BACKFILL_START Search index backfill started for user {UserId}.", userId);

            long entryCountBefore = await CountEntriesForUserAsync(userId, ct);

            int documentCount = await _dbContext.Documents
                .AsNoTracking()
                .CountAsync(doc => doc.OwnerUserId == userId, ct);
            _logger.LogDebug("BACKFILL_PROGRESS source counts. DocumentCount={DocumentCount}.", documentCount);

            List<DocumentRecord> documents = await _dbContext.Documents
                .AsNoTracking()
                .Where(doc => doc.OwnerUserId == userId)
                .ToListAsync(ct);

            int processedDocuments = 0;
            int processedSections = 0;
            int processedPages = 0;
            int processedNotes = 0;
            int processedSceneCards = 0;
            int processedOutlineNodes = 0;
            bool processedOutlineText = false;

            foreach (DocumentRecord document in documents)
            {
                await UpsertDocumentAsync(document, ct);
                processedDocuments++;

                List<SectionRecord> sections = await _dbContext.Sections
                    .AsNoTracking()
                    .Where(section => section.DocumentId == document.Id)
                    .ToListAsync(ct);

                foreach (SectionRecord section in sections)
                {
                    await UpsertSectionAsync(section, ct);
                    processedSections++;

                    SectionSceneCardRecord? card = await _dbContext.SectionSceneCards
                        .AsNoTracking()
                        .FirstOrDefaultAsync(entry => entry.SectionId == section.Id, ct);
                    if (card is not null)
                    {
                        await UpsertSceneCardAsync(section, card, ct);
                        processedSceneCards++;
                    }
                }

                List<PageRecord> pages = await _dbContext.Pages
                    .AsNoTracking()
                    .Where(page => page.DocumentId == document.Id)
                    .ToListAsync(ct);

                foreach (PageRecord page in pages)
                {
                    await UpsertPageAsync(page, ct);
                    processedPages++;

                    PageNoteRecord? notes = await _dbContext.PageNotes
                        .AsNoTracking()
                        .FirstOrDefaultAsync(entry => entry.PageId == page.Id, ct);
                    if (notes is not null)
                    {
                        await UpsertPageNotesAsync(page, notes, ct);
                        processedNotes++;
                    }
                }

                DocumentOutlineRecord? outline = await _dbContext.DocumentOutlines
                    .AsNoTracking()
                    .FirstOrDefaultAsync(entry => entry.DocumentId == document.Id, ct);
                List<DocumentOutlineNodeRecord> nodes = await _dbContext.DocumentOutlineNodes
                    .AsNoTracking()
                    .Where(node => node.DocumentId == document.Id)
                    .ToListAsync(ct);

                if (outline is not null || nodes.Count > 0)
                {
                    await ReplaceOutlineAsync(document, outline?.Outline ?? string.Empty, nodes, ct);
                    processedOutlineText |= outline is not null;
                    processedOutlineNodes += nodes.Count;
                }

                if (processedDocuments % 5 == 0)
                {
                    _logger.LogDebug("BACKFILL_PROGRESS processed {ProcessedDocuments}/{TotalDocuments} documents.",
                        processedDocuments,
                        documentCount);
                }
            }

            long entryCountAfter = await CountEntriesForUserAsync(userId, ct);
            long entryDelta = entryCountAfter - entryCountBefore;

            _logger.LogInformation(
                "BACKFILL_DONE Search index backfill completed for user {UserId}. EntriesBefore={EntriesBefore} EntriesAfter={EntriesAfter} Delta={Delta} Docs={Docs} Sections={Sections} Pages={Pages} Notes={Notes} SceneCards={SceneCards} OutlineText={OutlineText} OutlineNodes={OutlineNodes}.",
                userId,
                entryCountBefore,
                entryCountAfter,
                entryDelta,
                processedDocuments,
                processedSections,
                processedPages,
                processedNotes,
                processedSceneCards,
                processedOutlineText,
                processedOutlineNodes);
        }

        private async Task<long> CountEntriesForUserProjectAsync(string userId, Guid projectId, CancellationToken ct)
        {
            await using var countConnection = _dbContext.Database.GetDbConnection();
            if (countConnection.State != ConnectionState.Open)
            {
                await countConnection.OpenAsync(ct);
            }

            await using var countCommand = countConnection.CreateCommand();
            countCommand.CommandText = @"
SELECT COUNT(*)
FROM SearchIndexEntries e
JOIN Documents d ON (
    lower(d.Id) = lower(e.DocumentId)
)
WHERE d.OwnerUserId = $userId
  AND lower(e.ProjectId) = $projectId;
";
            AddParameter(countCommand, "$userId", userId);
            AddParameter(countCommand, "$projectId", IdNorm.Norm(projectId));
            object? countResult = await countCommand.ExecuteScalarAsync(ct);
            return countResult is null || countResult == DBNull.Value ? 0 : Convert.ToInt64(countResult);
        }

        private async Task<long> CountEntriesForUserAsync(string userId, CancellationToken ct)
        {
            await using var countConnection = _dbContext.Database.GetDbConnection();
            if (countConnection.State != ConnectionState.Open)
            {
                await countConnection.OpenAsync(ct);
            }

            await using var countCommand = countConnection.CreateCommand();
            countCommand.CommandText = @"
SELECT COUNT(*)
FROM SearchIndexEntries e
JOIN Documents d ON (
    lower(d.Id) = e.DocumentId
)
WHERE d.OwnerUserId = $userId;
";
            AddParameter(countCommand, "$userId", userId);
            object? countResult = await countCommand.ExecuteScalarAsync(ct);
            return countResult is null || countResult == DBNull.Value ? 0 : Convert.ToInt64(countResult);
        }

        private sealed record SearchIndexEntry(
            string EntityType,
            Guid EntityId,
            Guid DocumentId,
            Guid ProjectId,
            Guid? SectionId,
            Guid? PageId,
            string Title,
            string Content,
            DateTimeOffset UpdatedAt);

        private static async Task EnsureProjectIdColumnAsync(System.Data.Common.DbConnection connection, CancellationToken ct)
        {
            bool hasProjectId = false;
            await using (var pragma = connection.CreateCommand())
            {
                pragma.CommandText = "PRAGMA table_info('SearchIndexEntries');";
                await using var reader = await pragma.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    string name = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                    if (string.Equals(name, "ProjectId", StringComparison.OrdinalIgnoreCase))
                    {
                        hasProjectId = true;
                        break;
                    }
                }
            }

            if (!hasProjectId)
            {
                await using var alter = connection.CreateCommand();
                alter.CommandText = "ALTER TABLE SearchIndexEntries ADD COLUMN ProjectId TEXT NOT NULL DEFAULT '';";
                await alter.ExecuteNonQueryAsync(ct);
            }

            await using (var backfill = connection.CreateCommand())
            {
                backfill.CommandText = @"
UPDATE SearchIndexEntries
SET ProjectId = COALESCE((
    SELECT lower(d.ProjectId)
    FROM Documents d
    WHERE lower(d.Id) = lower(SearchIndexEntries.DocumentId)
    LIMIT 1
), '')
WHERE ProjectId IS NULL OR ProjectId = '';";
                await backfill.ExecuteNonQueryAsync(ct);
            }

            await using (var idx = connection.CreateCommand())
            {
                idx.CommandText = "CREATE INDEX IF NOT EXISTS IX_SearchIndexEntries_ProjectId ON SearchIndexEntries (ProjectId);";
                await idx.ExecuteNonQueryAsync(ct);
            }
        }

        private async Task<Guid> ResolveProjectIdForDocumentAsync(Guid documentId, CancellationToken ct)
        {
            Guid? projectId = await _dbContext.Documents
                .AsNoTracking()
                .Where(document => document.Id == documentId)
                .Select(document => (Guid?)document.ProjectId)
                .FirstOrDefaultAsync(ct);

            return projectId ?? Guid.Empty;
        }
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
}
