using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
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
        Task<IReadOnlyList<SearchResultDto>> SearchAsync(string userId, string query, bool includeMeta, int limit, CancellationToken ct);
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
        private static bool _initialized;
        private static bool _disabled;
        private static string? _disabledReason;

        public SearchIndexService(
            AppDbContext dbContext,
            ILogger<SearchIndexService> logger,
            ISearchIndexBackfillQueue backfillQueue)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _backfillQueue = backfillQueue ?? throw new ArgumentNullException(nameof(backfillQueue));
        }

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
                SectionId: null,
                PageId: null,
                Title: document.Title ?? string.Empty,
                Content: document.Title ?? string.Empty), ct);
        }

        public Task UpsertSectionAsync(SectionRecord section, CancellationToken ct)
        {
            if (section is null)
            {
                throw new ArgumentNullException(nameof(section));
            }

            return UpsertEntryAsync(new SearchIndexEntry(
                EntityType: SearchEntityTypes.Section,
                EntityId: section.Id,
                DocumentId: section.DocumentId,
                SectionId: section.Id,
                PageId: null,
                Title: section.Title ?? string.Empty,
                Content: section.Title ?? string.Empty), ct);
        }

        public Task UpsertPageAsync(PageRecord page, CancellationToken ct)
        {
            if (page is null)
            {
                throw new ArgumentNullException(nameof(page));
            }

            string content = NormalizeText(page.Content);
            return UpsertEntryAsync(new SearchIndexEntry(
                EntityType: SearchEntityTypes.Page,
                EntityId: page.Id,
                DocumentId: page.DocumentId,
                SectionId: page.SectionId,
                PageId: page.Id,
                Title: page.Title ?? string.Empty,
                Content: content), ct);
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

            string content = NormalizeText(notes.Notes);
            return UpsertEntryAsync(new SearchIndexEntry(
                EntityType: SearchEntityTypes.Note,
                EntityId: notes.PageId,
                DocumentId: page.DocumentId,
                SectionId: page.SectionId,
                PageId: page.Id,
                Title: $"Notes: {page.Title ?? "Page"}",
                Content: content), ct);
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

            string content = string.Join("\n", new[]
            {
                card.NarrativePurpose,
                card.EmotionalBeat,
                card.KeyEvents,
                card.OpenQuestions
            }.Where(value => !string.IsNullOrWhiteSpace(value)));

            return UpsertEntryAsync(new SearchIndexEntry(
                EntityType: SearchEntityTypes.SceneCard,
                EntityId: card.SectionId,
                DocumentId: section.DocumentId,
                SectionId: section.Id,
                PageId: null,
                Title: $"Scene card: {section.Title ?? "Section"}",
                Content: NormalizeText(content)), ct);
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
                    SectionId: null,
                    PageId: null,
                    Title: $"Outline: {document.Title ?? "Document"}",
                    Content: NormalizeText(outlineText)), ct);
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
                    SectionId: node.LinkedSectionId,
                    PageId: null,
                    Title: $"Outline: {node.Title}",
                    Content: NormalizeText(content)), ct);
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
            string query,
            bool includeMeta,
            int limit,
            CancellationToken ct)
        {
            using IDisposable? scope = _logger.BeginScope(new Dictionary<string, object?>
            {
                ["SearchUserId"] = userId,
                ["IncludeMeta"] = includeMeta,
                ["Limit"] = limit
            });

            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new ArgumentException("userId is required.", nameof(userId));
            }

            string normalizedQuery = NormalizeQuery(query);
            _logger.LogDebug("Search normalized query. RawLength={RawLength} Normalized='{NormalizedQuery}'.",
                query?.Length ?? 0,
                normalizedQuery);
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
            bool hasUserEntries = await HasEntriesForUserAsync(userId, ct);
            if (!hasUserEntries)
            {
                bool enqueued = _backfillQueue.Enqueue(userId);
                _logger.LogInformation(enqueued
                    ? "BACKFILL_START queued for user."
                    : "BACKFILL_ALREADY_RUNNING backfill already queued or in progress for user.");
                return Array.Empty<SearchResultDto>();
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
    snippet(SearchIndexFts, 1, '<mark>', '</mark>', '...', 10) AS Snippet,
    bm25(SearchIndexFts, 1.2, 0.8) AS Score
FROM SearchIndexFts
JOIN SearchIndexEntries e ON e.Id = SearchIndexFts.rowid
JOIN Documents d ON (
    d.Id = e.DocumentId
    OR lower(d.Id) = lower(e.DocumentId)
    OR lower(hex(d.Id)) = replace(lower(e.DocumentId), '-', '')
)
WHERE SearchIndexFts MATCH $query
  AND d.OwnerUserId = $userId
  AND ($includeMeta = 1 OR e.EntityType = 'page')
ORDER BY
    CASE
        WHEN e.EntityType = 'page' THEN 0
        WHEN e.EntityType IN ('document', 'section') THEN 1
        WHEN e.EntityType = 'note' THEN 2
        WHEN e.EntityType = 'scenecard' THEN 3
        WHEN e.EntityType = 'outline' THEN 4
        ELSE 5
    END,
    Score
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
                    string snippet = reader.IsDBNull(7) ? string.Empty : reader.GetString(7);
                    double score = reader.IsDBNull(8) ? 0 : reader.GetDouble(8);

                    results.Add(new SearchResultDto(
                        DocumentId: documentId,
                        SectionId: sectionId,
                        PageId: pageId,
                        EntityType: entityType,
                        EntityId: entityId,
                        Title: title,
                        Snippet: snippet,
                        Score: score,
                        DocumentTitle: documentTitle));
                }

                _logger.LogDebug("Search complete. ResultCount={ResultCount}.", results.Count);
                if (results.Count == 0)
                {
                    long entryCount = await CountEntriesForUserAsync(userId, ct);
                    _logger.LogDebug("Search returned 0 results. UserEntryCount={UserEntryCount}.", entryCount);
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
                long? existingId = await TryGetEntryIdAsync(connection, transaction, entry.EntityType, entry.EntityId.ToString("D"), ct);
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
                long? existingId = await TryGetEntryIdAsync(connection, transaction, entityType, entityId.ToString("D"), ct);
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
WHERE EntityType = 'outline' AND DocumentId = $documentId;
";
                AddParameter(command, "$documentId", documentId.ToString("D"));

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
    $sectionId,
    $pageId,
    $title,
    $content,
    $updatedAt
);
SELECT last_insert_rowid();
";
            AddParameter(command, "$entityType", entry.EntityType);
            AddParameter(command, "$entityId", entry.EntityId.ToString("D"));
            AddParameter(command, "$documentId", entry.DocumentId.ToString("D"));
            AddParameter(command, "$sectionId", entry.SectionId?.ToString("D"));
            AddParameter(command, "$pageId", entry.PageId?.ToString("D"));
            AddParameter(command, "$title", entry.Title ?? string.Empty);
            AddParameter(command, "$content", entry.Content ?? string.Empty);
            AddParameter(command, "$updatedAt", DateTimeOffset.UtcNow.ToString("O"));

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
    SectionId = $sectionId,
    PageId = $pageId,
    Title = $title,
    Content = $content,
    UpdatedAt = $updatedAt
WHERE Id = $id;
";
            AddParameter(command, "$documentId", entry.DocumentId.ToString("D"));
            AddParameter(command, "$sectionId", entry.SectionId?.ToString("D"));
            AddParameter(command, "$pageId", entry.PageId?.ToString("D"));
            AddParameter(command, "$title", entry.Title ?? string.Empty);
            AddParameter(command, "$content", entry.Content ?? string.Empty);
            AddParameter(command, "$updatedAt", DateTimeOffset.UtcNow.ToString("O"));
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
            AddParameter(command, "$entityId", entry.EntityId.ToString("D"));
            AddParameter(command, "$documentId", entry.DocumentId.ToString("D"));
            AddParameter(command, "$sectionId", entry.SectionId?.ToString("D"));
            AddParameter(command, "$pageId", entry.PageId?.ToString("D"));
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

            string decoded = System.Net.WebUtility.HtmlDecode(value);
            string withoutTags = Regex.Replace(decoded, "<.*?>", " ");
            string normalized = Regex.Replace(withoutTags, "\\s+", " ").Trim();
            return normalized;
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

                await using var command = connection.CreateCommand();
                command.CommandText = @"
CREATE TABLE IF NOT EXISTS SearchIndexEntries (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    EntityType TEXT NOT NULL,
    EntityId TEXT NOT NULL,
    DocumentId TEXT NOT NULL,
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
            _logger.LogError(ex, "Search index disabled: {Reason}", _disabledReason);
        }

        private async Task<bool> HasEntriesForUserAsync(string userId, CancellationToken ct)
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
    d.Id = e.DocumentId
    OR lower(d.Id) = lower(e.DocumentId)
    OR lower(hex(d.Id)) = replace(lower(e.DocumentId), '-', '')
)
WHERE d.OwnerUserId = $userId
LIMIT 1;
";
                AddParameter(command, "$userId", userId);
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
    d.Id = e.DocumentId
    OR lower(d.Id) = lower(e.DocumentId)
    OR lower(hex(d.Id)) = replace(lower(e.DocumentId), '-', '')
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
            Guid? SectionId,
            Guid? PageId,
            string Title,
            string Content);
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
