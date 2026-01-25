using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WriterApp.Application.Documents;
using WriterApp.Domain.Documents;

namespace WriterApp.Application.State
{
    public sealed class LegacyDocumentMigrationService
    {
        private const int JsInteropRetryCount = 2;
        private const int JsInteropRetryDelayMs = 300;
        private readonly SemaphoreSlim _migrationGate = new(1, 1);
        private readonly object _snapshotLock = new();
        private Task<IReadOnlyList<Document>>? _snapshotTask;
        private readonly object _statusLock = new();
        private MigrationStatusSnapshot _status = new(
            false,
            0,
            0,
            0,
            0,
            "Idle",
            null,
            null,
            null,
            DateTimeOffset.UtcNow);
        private readonly DocumentStorageService _storage;
        private readonly HttpClient _http;
        private readonly ILogger<LegacyDocumentMigrationService> _logger;

        public LegacyDocumentMigrationService(
            DocumentStorageService storage,
            HttpClient http,
            ILogger<LegacyDocumentMigrationService> logger)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _http = http ?? throw new ArgumentNullException(nameof(http));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<int> MigrateAsync(CancellationToken ct = default)
        {
            await _migrationGate.WaitAsync(ct);
            try
            {
                if (ct.IsCancellationRequested)
                {
                    UpdateStatus(isRunning: false, status: "Canceled before start");
                    return 0;
                }

                    UpdateStatus(isRunning: true, status: "Starting legacy migration", lastDetail: "Snapshot pending");
                IReadOnlyList<Document> snapshot = await SnapshotLegacyDocumentsAsync(ct);
                if (snapshot.Count == 0)
                {
                    UpdateStatus(isRunning: false, status: "No legacy documents to migrate");
                    return 0;
                }

                IReadOnlyList<Guid> migratedIds = await PersistSnapshotAsync(snapshot, ct);
                if (ct.IsCancellationRequested || migratedIds.Count == 0)
                {
                    UpdateStatus(isRunning: false, status: "Migration canceled or no documents persisted");
                    return migratedIds.Count;
                }

                await MarkMigratedAsync(migratedIds, ct);
                UpdateStatus(isRunning: false, status: "Migration completed");
                _logger.LogInformation("Legacy migration: completed, migrated {MigratedCount} documents.", migratedIds.Count);
                return migratedIds.Count;
            }
            finally
            {
                _migrationGate.Release();
            }
        }

        public MigrationStatusSnapshot GetStatusSnapshot()
        {
            lock (_statusLock)
            {
                return _status;
            }
        }

        public async Task<int> GetLegacyDocumentCountAsync(CancellationToken ct = default)
        {
            try
            {
                List<DocumentIndexEntry> index = await ExecuteWithJsRetryAsync(
                    () => _storage.LoadIndexAsync(),
                    "load legacy index",
                    ct);
                return index.Count;
            }
            catch (OperationCanceledException ex)
            {
                _logger.LogWarning(ex, "Legacy migration: load index canceled after retries.");
                return 0;
            }
        }

        private async Task<IReadOnlyList<Document>> SnapshotLegacyDocumentsAsync(CancellationToken ct)
        {
            Task<IReadOnlyList<Document>> snapshotTask;
            lock (_snapshotLock)
            {
                if (_snapshotTask is null || _snapshotTask.IsCompleted)
                {
                    _snapshotTask = LoadSnapshotCoreAsync(ct);
                }

                snapshotTask = _snapshotTask;
            }

            return await snapshotTask.WaitAsync(ct);
        }

        private async Task<IReadOnlyList<Document>> LoadSnapshotCoreAsync(CancellationToken ct)
        {
            DocumentStorageService.LegacySnapshotResult snapshotResult;
            try
            {
                snapshotResult = await ExecuteWithJsRetryAsync(
                    () => _storage.LoadLegacySnapshotAsync(ct),
                    "load legacy documents snapshot",
                    ct);
            }
            catch (OperationCanceledException ex)
            {
                if (ct.IsCancellationRequested)
                {
                    _logger.LogDebug(ex, "Legacy migration: snapshot canceled.");
                    UpdateStatus(isRunning: false, status: "Snapshot canceled");
                    return Array.Empty<Document>();
                }

                _logger.LogWarning(ex, "Legacy migration: snapshot canceled after retries.");
                UpdateStatus(isRunning: false, status: "Snapshot canceled after retries");
                return Array.Empty<Document>();
            }

            IReadOnlyList<DocumentStorageService.LegacyDocumentSnapshot> snapshots = snapshotResult.Snapshots;
            int alreadyMigrated = snapshotResult.AlreadyMigrated;
            int toMigrate = snapshots.Count;
            _logger.LogInformation(
                "Legacy migration: snapshot loaded. Total={Total} Migrated={Migrated} ToMigrate={ToMigrate} Attempted={Attempted} Succeeded={Succeeded} Failed={Failed}.",
                snapshotResult.AlreadyMigrated + snapshots.Count,
                alreadyMigrated,
                toMigrate,
                snapshotResult.Attempted,
                snapshotResult.Succeeded,
                snapshotResult.Failed);
            UpdateStatus(
                totalLegacyDocuments: snapshotResult.AlreadyMigrated + snapshots.Count,
                status: "Snapshot loaded",
                lastDetail: $"Snapshot attempted {snapshotResult.Attempted}, succeeded {snapshotResult.Succeeded}, failed {snapshotResult.Failed}");
            if (snapshots.Count == 0)
            {
                UpdateStatus(isRunning: false, status: "No legacy documents found");
                return Array.Empty<Document>();
            }

            List<Document> documents = new();
            foreach (DocumentStorageService.LegacyDocumentSnapshot snapshot in snapshots)
            {
                if (ct.IsCancellationRequested)
                {
                    break;
                }

                if (snapshot.IsMigrated)
                {
                    _logger.LogDebug("Legacy migration: skipping already-migrated document {DocumentId}.", snapshot.DocumentId);
                    continue;
                }

                documents.Add(snapshot.Document);
            }

            return documents;
        }

        private async Task<IReadOnlyList<Guid>> PersistSnapshotAsync(IReadOnlyList<Document> snapshot, CancellationToken ct)
        {
            List<Guid> migratedIds = new();
            UpdateStatus(status: "Persisting legacy documents", lastDetail: "Persist loop start");
            foreach (Document document in snapshot)
            {
                if (ct.IsCancellationRequested)
                {
                    UpdateStatus(isRunning: false, status: "Migration canceled during persistence");
                    break;
                }

                bool migrated = await TryPersistDocumentAsync(document, ct);
                if (migrated)
                {
                    migratedIds.Add(document.DocumentId);
                    UpdateStatus(persistedDocuments: migratedIds.Count, status: "Persisted legacy document");
                }
            }

            return migratedIds;
        }

        private async Task MarkMigratedAsync(IEnumerable<Guid> migratedIds, CancellationToken ct)
        {
                UpdateStatus(status: "Marking migration flags", lastDetail: "Mark loop start");
            foreach (Guid documentId in migratedIds)
            {
                if (ct.IsCancellationRequested)
                {
                    UpdateStatus(isRunning: false, status: "Migration canceled while marking flags");
                    break;
                }

                try
                {
                    _logger.LogInformation("Legacy migration: marking migrated flag for {DocumentId}.", documentId);
                    await ExecuteWithJsRetryAsync(
                        () => _storage.MarkMigratedAsync(documentId),
                        "mark migrated",
                        ct);
                }
                catch (OperationCanceledException ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Legacy migration: migration flag write canceled for {DocumentId}.",
                        documentId);
                }
            }
        }

        private async Task<bool> TryPersistDocumentAsync(Document document, CancellationToken ct)
        {
            try
            {
                UpdateStatus(
                    status: $"Persisting document {document.DocumentId}",
                    lastDocumentId: document.DocumentId,
                    lastDocumentTitle: document.Metadata.Title,
                    lastDetail: "Creating document");
                _logger.LogInformation("Legacy migration: migrating document {DocumentId}.", document.DocumentId);
                DocumentCreateRequest createRequest = new(
                    document.DocumentId,
                    document.Metadata.Title,
                    document.Metadata.CreatedUtc,
                    document.Metadata.ModifiedUtc,
                    CreateDefaultStructure: false);

                using HttpResponseMessage createResponse = await _http.PostAsJsonAsync("api/documents", createRequest, ct);
                if (!createResponse.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Legacy migration: create document failed with {Status}.", createResponse.StatusCode);
                    return false;
                }

                IReadOnlyList<SectionDto> existingSections = await GetSectionsAsync(document.DocumentId, ct);
                HashSet<Guid> existingSectionIds = existingSections
                    .Select(section => section.Id)
                    .ToHashSet();

                List<Section> orderedSections = document.Chapters
                    .OrderBy(chapter => chapter.Order)
                    .SelectMany(chapter => chapter.Sections.OrderBy(section => section.Order))
                    .ToList();

                int pagesCreated = 0;
                int pagesUpdated = 0;
                int totalContentChars = 0;

                _logger.LogInformation(
                    "Legacy migration: document {DocumentId} \"{Title}\" has {SectionCount} sections.",
                    document.DocumentId,
                    document.Metadata.Title,
                    orderedSections.Count);
                UpdateStatus(sectionsProcessed: orderedSections.Count, lastDetail: $"Sections to migrate: {orderedSections.Count}");

                for (int index = 0; index < orderedSections.Count; index++)
                {
                    Section section = orderedSections[index];
                    if (!existingSectionIds.Contains(section.SectionId))
                    {
                        SectionCreateRequest sectionRequest = new(
                            section.SectionId,
                            section.Title,
                            null,
                            index,
                            section.CreatedUtc,
                            section.ModifiedUtc);

                        using HttpResponseMessage sectionResponse = await _http.PostAsJsonAsync(
                            $"api/documents/{document.DocumentId}/sections",
                            sectionRequest,
                            ct);

                        if (!sectionResponse.IsSuccessStatusCode)
                        {
                            _logger.LogWarning(
                                "Legacy migration: create section {SectionId} failed with {Status}.",
                                section.SectionId,
                                sectionResponse.StatusCode);
                            continue;
                        }
                    }

                    IReadOnlyList<PageDto> existingPages = await GetPagesAsync(section.SectionId, ct);
                    if (existingPages.Count > 0)
                    {
                        string content1 = section.Content.Value ?? string.Empty;
                        bool hasContent = existingPages.Any(page => !string.IsNullOrWhiteSpace(page.Content));
                        if (!hasContent && !string.IsNullOrWhiteSpace(content1))
                        {
                            PageDto target = existingPages
                                .OrderBy(page => page.OrderIndex)
                                .First();
                            PageUpdateRequest update = new(null, content1);
                            using HttpResponseMessage updateResponse = await _http.PutAsJsonAsync(
                                $"api/pages/{target.Id}",
                                update,
                                ct);

                            if (!updateResponse.IsSuccessStatusCode)
                            {
                                _logger.LogWarning(
                                    "Legacy migration: update page {PageId} failed with {Status}.",
                                    target.Id,
                                    updateResponse.StatusCode);
                                UpdateStatus(
                                    status: "Page content update failed",
                                    lastDetail: $"Update failed {updateResponse.StatusCode} for page {target.Id}");
                            }
                            else
                            {
                                _logger.LogInformation(
                                    "Legacy migration: updated page {PageId} content from legacy.",
                                    target.Id);
                                pagesUpdated++;
                                totalContentChars += content1.Length;
                                UpdateStatus(
                                    pagesMigratedDelta: 1,
                                    lastDetail: $"Updated page {target.Id} content");
                            }
                        }

                        continue;
                    }

                    string title = string.IsNullOrWhiteSpace(section.Title) ? "Page 1" : section.Title;
                    string content = section.Content.Value ?? string.Empty;
                    PageCreateRequest pageRequest = new(
                        null,
                        title,
                        content,
                        0,
                        section.CreatedUtc,
                        section.ModifiedUtc);

                    using HttpResponseMessage pageResponse = await _http.PostAsJsonAsync(
                        $"api/sections/{section.SectionId}/pages",
                        pageRequest,
                        ct);

                    if (!pageResponse.IsSuccessStatusCode)
                    {
                        _logger.LogWarning(
                            "Legacy migration: create page for section {SectionId} failed with {Status}.",
                            section.SectionId,
                            pageResponse.StatusCode);
                        UpdateStatus(
                            status: "Create page failed",
                            lastDetail: $"Create page failed {pageResponse.StatusCode} for section {section.SectionId}");
                    }
                    else
                    {
                        pagesCreated++;
                        totalContentChars += content.Length;
                        UpdateStatus(
                            pagesMigratedDelta: 1,
                            lastDetail: $"Created page for section {section.SectionId}");
                    }
                }

                _logger.LogInformation(
                    "Legacy migration: migrated document {DocumentId}. PagesCreated={PagesCreated} PagesUpdated={PagesUpdated} ContentChars={ContentChars}.",
                    document.DocumentId,
                    pagesCreated,
                    pagesUpdated,
                    totalContentChars);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Legacy migration failed.");
                UpdateStatus(status: "Migration failed", lastDetail: ex.Message);
                return false;
            }
        }

        private async Task<IReadOnlyList<SectionDto>> GetSectionsAsync(Guid documentId, CancellationToken ct)
        {
            using HttpResponseMessage response = await _http.GetAsync($"api/documents/{documentId}/sections", ct);
            if (!response.IsSuccessStatusCode)
            {
                return Array.Empty<SectionDto>();
            }
            List<SectionDto>? sections =
                await response.Content.ReadFromJsonAsync<List<SectionDto>>(cancellationToken: ct);

            return sections is null ? Array.Empty<SectionDto>() : sections;
        }

        private async Task<IReadOnlyList<PageDto>> GetPagesAsync(Guid sectionId, CancellationToken ct)
        {
            using HttpResponseMessage response = await _http.GetAsync($"api/sections/{sectionId}/pages", ct);
            if (!response.IsSuccessStatusCode)
            {
                return Array.Empty<PageDto>();
            }

            List<PageDto>? pages = await response.Content.ReadFromJsonAsync<List<PageDto>>(cancellationToken: ct);
            return pages ?? (IReadOnlyList<PageDto>)Array.Empty<PageDto>();
        }

        private async Task<T> ExecuteWithJsRetryAsync<T>(Func<Task<T>> action, string operation, CancellationToken ct)
        {
            int attempt = 0;
            while (true)
            {
                try
                {
                    ct.ThrowIfCancellationRequested();
                    return await action();
                }
                catch (TaskCanceledException ex)
                {
                    if (ct.IsCancellationRequested)
                    {
                        _logger.LogDebug(ex, "Legacy migration: {Operation} canceled.", operation);
                        throw new OperationCanceledException(ct);
                    }

                    attempt++;
                    if (attempt > JsInteropRetryCount)
                    {
                        throw;
                    }

                    _logger.LogWarning(
                        ex,
                        "Legacy migration: {Operation} canceled; retry {Attempt}/{Max}.",
                        operation,
                        attempt,
                        JsInteropRetryCount);
                    await Task.Delay(JsInteropRetryDelayMs, CancellationToken.None);
                }
            }
        }

        private async Task ExecuteWithJsRetryAsync(Func<Task> action, string operation, CancellationToken ct)
        {
            int attempt = 0;
            while (true)
            {
                try
                {
                    ct.ThrowIfCancellationRequested();
                    await action();
                    return;
                }
                catch (TaskCanceledException ex)
                {
                    if (ct.IsCancellationRequested)
                    {
                        _logger.LogDebug(ex, "Legacy migration: {Operation} canceled.", operation);
                        throw new OperationCanceledException(ct);
                    }

                    attempt++;
                    if (attempt > JsInteropRetryCount)
                    {
                        throw;
                    }

                    _logger.LogWarning(
                        ex,
                        "Legacy migration: {Operation} canceled; retry {Attempt}/{Max}.",
                        operation,
                        attempt,
                        JsInteropRetryCount);
                    await Task.Delay(JsInteropRetryDelayMs, CancellationToken.None);
                }
            }
        }

        private void UpdateStatus(
            bool? isRunning = null,
            int? totalLegacyDocuments = null,
            int? persistedDocuments = null,
            int? sectionsProcessed = null,
            int? pagesMigratedDelta = null,
            Guid? lastDocumentId = null,
            string? lastDocumentTitle = null,
            string? status = null,
            string? lastDetail = null)
        {
            lock (_statusLock)
            {
                int nextPages = _status.PagesMigrated;
                if (pagesMigratedDelta.HasValue)
                {
                    nextPages = Math.Max(0, nextPages + pagesMigratedDelta.Value);
                }

                _status = _status with
                {
                    IsRunning = isRunning ?? _status.IsRunning,
                    TotalLegacyDocuments = totalLegacyDocuments ?? _status.TotalLegacyDocuments,
                    PersistedDocuments = persistedDocuments ?? _status.PersistedDocuments,
                    SectionsProcessed = sectionsProcessed ?? _status.SectionsProcessed,
                    PagesMigrated = nextPages,
                    LastDocumentId = lastDocumentId ?? _status.LastDocumentId,
                    LastDocumentTitle = lastDocumentTitle ?? _status.LastDocumentTitle,
                    Status = status ?? _status.Status,
                    LastDetail = lastDetail ?? _status.LastDetail,
                    UpdatedUtc = DateTimeOffset.UtcNow
                };
            }
        }

        public sealed record MigrationStatusSnapshot(
            bool IsRunning,
            int TotalLegacyDocuments,
            int PersistedDocuments,
            int SectionsProcessed,
            int PagesMigrated,
            string Status,
            string? LastDetail,
            Guid? LastDocumentId,
            string? LastDocumentTitle,
            DateTimeOffset UpdatedUtc);
    }
}
