using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using WriterApp.Domain.Documents;

namespace WriterApp.Application.State
{
    public sealed class DocumentStorageService
    {
        private const string StorageKey = "writerapp.document";
        private const string StorageIndexKey = "writerapp.document.index";
        private const string StoragePrefix = "writerapp.document.";
        private const string AutosavePrefix = "writerapp.document.autosave.";
        private const string MigrationPrefix = "writerapp.document.migrated.";
        private readonly IJSRuntime _jsRuntime;
        private readonly ILogger<DocumentStorageService> _logger;
        private readonly string _storageKey;
        private readonly string _storageIndexKey;
        private readonly string _storagePrefix;
        private readonly string _autosavePrefix;
        private readonly string _migrationPrefix;
        private bool _prefixesLogged;
        private bool _migrationKeyLogged;

        public DocumentStorageService(IJSRuntime jsRuntime, ILogger<DocumentStorageService> logger)
        {
            _jsRuntime = jsRuntime;
            _logger = logger;
            _storageKey = StorageKey;
            _storageIndexKey = StorageIndexKey;
            _storagePrefix = NormalizePrefix(StoragePrefix, requireTrailingDot: true);
            _autosavePrefix = NormalizePrefix(AutosavePrefix, requireTrailingDot: true);
            _migrationPrefix = NormalizePrefix(MigrationPrefix, requireTrailingDot: true);

            Debug.Assert(!_storagePrefix.EndsWith("..", StringComparison.Ordinal), "StoragePrefix should not end with '..'");
            Debug.Assert(!_autosavePrefix.EndsWith("..", StringComparison.Ordinal), "AutosavePrefix should not end with '..'");
            Debug.Assert(!_migrationPrefix.EndsWith("..", StringComparison.Ordinal), "MigrationPrefix should not end with '..'");
        }

        public async Task<List<DocumentIndexEntry>> LoadIndexAsync()
        {
            string? json = null;
            try
            {
                json = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", _storageIndexKey);
            }
            catch (JSException ex)
            {
                _logger.LogWarning(ex, "Local storage index read failed.");
                return new List<DocumentIndexEntry>();
            }

            if (string.IsNullOrWhiteSpace(json))
            {
                return new List<DocumentIndexEntry>();
            }

            List<DocumentIndexEntry>? entries = JsonSerializer.Deserialize<List<DocumentIndexEntry>>(json);
            if (entries is null)
            {
                return new List<DocumentIndexEntry>();
            }

            entries.Sort((a, b) => b.LastModifiedUtc.CompareTo(a.LastModifiedUtc));
            return entries;
        }

        public async Task<Document?> LoadDocumentByIdAsync(string documentId)
        {
            string key = _storagePrefix + documentId;
            string? json = null;
            try
            {
                json = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", key);
            }
            catch (JSException ex)
            {
                _logger.LogWarning(ex, "Local storage read failed.");
                return null;
            }

            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            DocumentStorage? payload = JsonSerializer.Deserialize<DocumentStorage>(json);
            if (payload?.Document is null)
            {
                return null;
            }

            return DocumentFactory.EnsureSynopsis(payload.Document);
        }

        public async Task<IReadOnlyList<LegacyDocumentSnapshot>> LoadAllLegacyDocumentsAsync(CancellationToken ct = default)
        {
            LegacySnapshotResult result = await LoadLegacySnapshotAsync(ct);
            return result.Snapshots;
        }

        public async Task<LegacySnapshotResult> LoadLegacySnapshotAsync(CancellationToken ct = default)
        {
            StorageDiagnosticsResult? diagnostics = null;
            try
            {
                _logger.LogInformation("Local storage bulk read starting.");
                _logger.LogInformation(
                    "Local storage bulk read: StoragePrefix={StoragePrefix} IndexKey={IndexKey} AutosavePrefix={AutosavePrefix} MigrationPrefix={MigrationPrefix}",
                    _storagePrefix,
                    _storageIndexKey,
                    _autosavePrefix,
                    _migrationPrefix);
                _logger.LogInformation(
                    "Local storage bulk read identifiers={ListIdentifier},{JsonIdentifier} StoragePrefixEndsWithDot={StorageDot} IndexKeyEndsWithDot={IndexDot} AutosavePrefixEndsWithDot={AutosaveDot} MigrationPrefixEndsWithDot={MigrationDot}",
                    "writerAppStorage.listLegacyDocumentIds",
                    "writerAppStorage.loadLegacyDocumentJson",
                    _storagePrefix.EndsWith(".", StringComparison.Ordinal),
                    _storageIndexKey.EndsWith(".", StringComparison.Ordinal),
                    _autosavePrefix.EndsWith(".", StringComparison.Ordinal),
                    _migrationPrefix.EndsWith(".", StringComparison.Ordinal));
                LogPrefixDiagnostics();
                try
                {
                    diagnostics = await _jsRuntime.InvokeAsync<StorageDiagnosticsResult>(
                        "writerAppStorage.diagnostics",
                        ct,
                        _storagePrefix,
                        _storageIndexKey,
                        _autosavePrefix,
                        _migrationPrefix);
                    _logger.LogInformation(
                        "Local storage diagnostics: ExistsStorage={ExistsStorage} ExistsLoader={ExistsLoader} Origin={Origin} KeyCount={KeyCount} IndexKeyExists={IndexKeyExists} IndexValueLength={IndexValueLength} DocKeys={DocKeys} AutosaveKeys={AutosaveKeys} MigratedKeys={MigratedKeys} Error={Error}",
                        diagnostics.ExistsWriterAppStorage,
                        diagnostics.ExistsLoadLegacyDocuments,
                        diagnostics.Origin ?? "unknown",
                        diagnostics.KeyCount,
                        diagnostics.IndexKeyExists,
                        diagnostics.IndexValueLength,
                        diagnostics.MatchedDocKeysCount,
                        diagnostics.MatchedAutosaveKeysCount,
                        diagnostics.MatchedMigratedKeysCount,
                        diagnostics.Error ?? "none");
                    if (_logger.IsEnabled(LogLevel.Debug) && diagnostics.SampleKeys is not null)
                    {
                        _logger.LogDebug("Local storage diagnostics sample keys: {Keys}", string.Join(", ", diagnostics.SampleKeys));
                    }
                }
                catch (JSException ex)
                {
                    _logger.LogWarning(ex, "Local storage diagnostics failed.");
                }
            }
            catch (JSException ex)
            {
                _logger.LogWarning(ex, "Local storage diagnostics failed.");
                return LegacySnapshotResult.Empty;
            }

            LegacyIdListResult listResult;
            using (var listTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
            using (var listLinked = CancellationTokenSource.CreateLinkedTokenSource(ct, listTimeout.Token))
            {
                try
                {
                    listResult = await LoadLegacyDocumentIdListAsync(listLinked.Token);
                }
                catch (OperationCanceledException) when (listTimeout.IsCancellationRequested)
                {
                    _logger.LogWarning(
                        "Legacy id list timed out; check local-storage.js load and circuit stability. Identifier={Identifier}",
                        "writerAppStorage.listLegacyDocumentIds");
                    return LegacySnapshotResult.Empty;
                }
            }
            if (listResult.Error is not null)
            {
                _logger.LogWarning("Local storage id list error: {Error}", listResult.Error);
                return LegacySnapshotResult.Empty;
            }

            int totalIds = listResult.Items.Count;
            int migratedIds = listResult.Items.Count(item => item.Migrated);
            int toLoadIds = totalIds - migratedIds;

            _logger.LogInformation(
                "Local storage legacy id list returned {Total} (Migrated={Migrated} ToLoad={ToLoad}).",
                totalIds,
                migratedIds,
                toLoadIds);

            if (totalIds == 0)
            {
                _logger.LogInformation(
                    "Local storage bulk read returned 0 entries. Check browser origin (http vs https) for legacy data.");
                return LegacySnapshotResult.Empty;
            }

            int attempted = 0;
            int succeeded = 0;
            int failed = 0;
            List<LegacyDocumentSnapshot> snapshots = new();

            using var globalTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            using var globalLinked = CancellationTokenSource.CreateLinkedTokenSource(ct, globalTimeout.Token);

            foreach (LegacyIdEntry entry in listResult.Items)
            {
                if (globalLinked.IsCancellationRequested)
                {
                    break;
                }

                if (entry.Migrated)
                {
                    continue;
                }

                attempted++;
                using var perDocTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                using var perDocLinked = CancellationTokenSource.CreateLinkedTokenSource(globalLinked.Token, perDocTimeout.Token);
                try
                {
                    LegacyJsonResult jsonResult = await LoadLegacyDocumentJsonAsync(entry.Id, perDocLinked.Token);
                    if (jsonResult.Error is not null)
                    {
                        failed++;
                        _logger.LogWarning("Legacy json load failed for {DocumentId}: {Error}", entry.Id, jsonResult.Error);
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(jsonResult.Json))
                    {
                        failed++;
                        _logger.LogWarning("Legacy json empty for {DocumentId}.", entry.Id);
                        continue;
                    }

                    if (!Guid.TryParse(entry.Id, out Guid documentId))
                    {
                        failed++;
                        _logger.LogWarning("Legacy json id invalid {DocumentId}.", entry.Id);
                        continue;
                    }

                    if (jsonResult.Json.Length > 500_000)
                    {
                        failed++;
                        _logger.LogWarning(
                            "Legacy json too large for {DocumentId} ({Length} chars). Skipping; chunking required.",
                            entry.Id,
                            jsonResult.Json.Length);
                        continue;
                    }

                    DocumentStorage? payload;
                    try
                    {
                        payload = JsonSerializer.Deserialize<DocumentStorage>(jsonResult.Json);
                    }
                    catch (JsonException ex)
                    {
                        failed++;
                        _logger.LogWarning(ex, "Legacy document payload parse failed for {DocumentId}.", entry.Id);
                        continue;
                    }

                    if (payload?.Document is null)
                    {
                        failed++;
                        _logger.LogWarning("Legacy document payload empty for {DocumentId}.", entry.Id);
                        continue;
                    }

                    Document normalized = DocumentFactory.EnsureSynopsis(payload.Document);
                    snapshots.Add(new LegacyDocumentSnapshot(documentId, normalized, entry.Migrated));
                    succeeded++;
                }
                catch (OperationCanceledException) when (perDocTimeout.IsCancellationRequested)
                {
                    failed++;
                    _logger.LogWarning(
                        "Legacy json load timed out for {DocumentId}. Suspected oversize or circuit instability.",
                        entry.Id);
                }
                catch (OperationCanceledException)
                {
                    failed++;
                    _logger.LogWarning(
                        "Legacy json load canceled for {DocumentId}.",
                        entry.Id);
                }
            }

            _logger.LogInformation(
                "Local storage legacy snapshot summary: Attempted={Attempted} Succeeded={Succeeded} Failed={Failed}.",
                attempted,
                succeeded,
                failed);

            return new LegacySnapshotResult(snapshots, attempted, succeeded, failed, migratedIds);
        }

        public async Task<LegacyIdListResult> LoadLegacyDocumentIdListAsync(CancellationToken ct = default)
        {
            try
            {
                LegacyIdListResult result = await _jsRuntime.InvokeAsync<LegacyIdListResult>(
                    "writerAppStorage.listLegacyDocumentIds",
                    ct,
                    _storagePrefix,
                    _storageIndexKey,
                    _autosavePrefix,
                    _migrationPrefix);
                if (result.Items is null)
                {
                    return LegacyIdListResult.Empty;
                }

                _logger.LogInformation("Legacy id list loaded {Count} entries.", result.Items.Count);
                return result;
            }
            catch (JSException ex)
            {
                _logger.LogWarning(ex, "Local storage id list failed.");
                return LegacyIdListResult.Empty;
            }
        }

        public async Task<LegacyJsonResult> LoadLegacyDocumentJsonAsync(string documentId, CancellationToken ct = default)
        {
            try
            {
                LegacyJsonResult result = await _jsRuntime.InvokeAsync<LegacyJsonResult>(
                    "writerAppStorage.loadLegacyDocumentJson",
                    ct,
                    _storagePrefix,
                    documentId);
                _logger.LogInformation(
                    "Legacy json load: DocumentId={DocumentId} JsonLength={Length}",
                    documentId,
                    result.Json?.Length ?? 0);
                return result;
            }
            catch (JSException ex)
            {
                _logger.LogWarning(ex, "Local storage json load failed for {DocumentId}.", documentId);
                return new LegacyJsonResult(documentId, null, ex.Message);
            }
        }

        public async Task SaveDocumentAsync(Document document)
        {
            Document normalized = DocumentFactory.EnsureSynopsis(document);
            DocumentStorage payload = new(normalized);
            string json = JsonSerializer.Serialize(payload);

            try
            {
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", _storageKey, json);
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", _storagePrefix + normalized.DocumentId, json);
            }
            catch (JSException ex)
            {
                _logger.LogWarning(ex, "Local storage save failed.");
            }

            await UpdateDocumentIndexAsync(normalized);
        }

        public async Task<bool> SaveAutosaveAsync(Guid documentId, Document document, DateTime autosavedUtc)
        {
            Document normalized = DocumentFactory.EnsureSynopsis(document);
            DocumentAutosave payload = new(normalized, autosavedUtc);
            string json = JsonSerializer.Serialize(payload);

            try
            {
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", _autosavePrefix + documentId, json);
                return true;
            }
            catch (JSException ex)
            {
                _logger.LogWarning(ex, "Local storage autosave failed.");
                return false;
            }
        }

        public async Task<DocumentAutosave?> LoadAutosaveAsync(string documentId)
        {
            string? json = null;
            try
            {
                json = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", _autosavePrefix + documentId);
            }
            catch (JSException ex)
            {
                _logger.LogWarning(ex, "Local storage autosave read failed.");
                return null;
            }

            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            DocumentAutosave? autosave = JsonSerializer.Deserialize<DocumentAutosave>(json);
            if (autosave?.Document is null)
            {
                return autosave;
            }

            Document normalized = DocumentFactory.EnsureSynopsis(autosave.Document);
            return new DocumentAutosave(normalized, autosave.AutosavedUtc);
        }

        public async Task<bool> ClearAutosaveAsync(Guid documentId)
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", _autosavePrefix + documentId);
                return true;
            }
            catch (JSException ex)
            {
                _logger.LogWarning(ex, "Local storage autosave clear failed.");
                return false;
            }
        }

        public async Task<bool> IsMigratedAsync(Guid documentId)
        {
            string? value = null;
            try
            {
                LogMigrationKeyOnce(documentId);
                value = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", _migrationPrefix + documentId);
            }
            catch (JSException ex)
            {
                _logger.LogWarning(ex, "Local storage migration flag read failed.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            return string.Equals(value, "1", StringComparison.Ordinal)
                || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
        }

        public async Task MarkMigratedAsync(Guid documentId)
        {
            try
            {
                LogMigrationKeyOnce(documentId);
                _logger.LogInformation("Local storage migration flag write: {Key}", _migrationPrefix + documentId);
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", _migrationPrefix + documentId, "1");
            }
            catch (JSException ex)
            {
                _logger.LogWarning(ex, "Local storage migration flag write failed.");
            }
        }

        private async Task UpdateDocumentIndexAsync(Document document)
        {
            List<DocumentIndexEntry> entries = await LoadIndexAsync();

            string title = string.IsNullOrWhiteSpace(document.Metadata.Title)
                ? "Untitled"
                : document.Metadata.Title;

            int wordCount = CountWordsInDocument(document);
            DocumentIndexEntry entry = new(document.DocumentId, title, document.Metadata.ModifiedUtc, wordCount);

            int index = entries.FindIndex(item => item.DocumentId == entry.DocumentId);
            if (index >= 0)
            {
                entries[index] = entry;
            }
            else
            {
                entries.Add(entry);
            }

            entries.Sort((a, b) => b.LastModifiedUtc.CompareTo(a.LastModifiedUtc));
            string json = JsonSerializer.Serialize(entries);

            try
            {
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", _storageIndexKey, json);
            }
            catch (JSException ex)
            {
                _logger.LogWarning(ex, "Local storage index update failed.");
            }
        }

        private static int CountWordsInDocument(Document document)
        {
            int wordCount = 0;
            for (int chapterIndex = 0; chapterIndex < document.Chapters.Count; chapterIndex++)
            {
                Chapter chapter = document.Chapters[chapterIndex];
                for (int sectionIndex = 0; sectionIndex < chapter.Sections.Count; sectionIndex++)
                {
                    Section section = chapter.Sections[sectionIndex];
                    string decoded = PlainTextMapper.ToPlainText(section.Content.Value ?? string.Empty);
                    MatchCollection matches = Regex.Matches(decoded, @"\b[\p{L}\p{N}']+\b");
                    wordCount += matches.Count;
                }
            }

            return wordCount;
        }

        public sealed record DocumentAutosave(Document Document, DateTime AutosavedUtc);

        private sealed record DocumentStorage(Document Document);

        private sealed record LegacyStorageEntry(string Id, string? Json, bool Migrated);

        public sealed record LegacyDocumentSnapshot(Guid DocumentId, Document Document, bool IsMigrated);

        public sealed record LegacySnapshotResult(
            IReadOnlyList<LegacyDocumentSnapshot> Snapshots,
            int Attempted,
            int Succeeded,
            int Failed,
            int AlreadyMigrated)
        {
            public static LegacySnapshotResult Empty { get; } =
                new(Array.Empty<LegacyDocumentSnapshot>(), 0, 0, 0, 0);
        }

        public sealed record LegacyIdEntry(string Id, bool Migrated);

        public sealed record LegacyIdListResult(List<LegacyIdEntry> Items, string? Error)
        {
            public static LegacyIdListResult Empty { get; } =
                new(new List<LegacyIdEntry>(), null);
        }

        public sealed record LegacyJsonResult(string Id, string? Json, string? Error);

        private static string NormalizePrefix(string value, bool requireTrailingDot)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return requireTrailingDot ? "." : string.Empty;
            }

            string normalized = value;
            while (normalized.EndsWith("..", StringComparison.Ordinal))
            {
                normalized = normalized[..^1];
            }

            if (requireTrailingDot && !normalized.EndsWith(".", StringComparison.Ordinal))
            {
                normalized += ".";
            }

            return normalized;
        }

        private void LogPrefixDiagnostics()
        {
            if (_prefixesLogged)
            {
                return;
            }

            _prefixesLogged = true;
            if (_storagePrefix.EndsWith("..", StringComparison.Ordinal)
                || _autosavePrefix.EndsWith("..", StringComparison.Ordinal)
                || _migrationPrefix.EndsWith("..", StringComparison.Ordinal))
            {
                _logger.LogWarning(
                    "Local storage prefix normalization detected trailing dots: StoragePrefix={StoragePrefix} AutosavePrefix={AutosavePrefix} MigrationPrefix={MigrationPrefix}",
                    _storagePrefix,
                    _autosavePrefix,
                    _migrationPrefix);
            }
        }

        private void LogMigrationKeyOnce(Guid documentId)
        {
            if (_migrationKeyLogged)
            {
                return;
            }

            _migrationKeyLogged = true;
            _logger.LogInformation(
                "Local storage migration key sample: {Key}",
                _migrationPrefix + documentId);
        }

        private sealed record StorageDiagnosticsResult(
            bool ExistsWriterAppStorage,
            bool ExistsLoadLegacyDocuments,
            string? Origin,
            int KeyCount,
            bool IndexKeyExists,
            int IndexValueLength,
            int MatchedDocKeysCount,
            int MatchedAutosaveKeysCount,
            int MatchedMigratedKeysCount,
            string? Error,
            string[]? SampleKeys);
    }
}
