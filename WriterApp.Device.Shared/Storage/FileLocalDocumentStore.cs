using System.Text.Json;

namespace WriterApp.Device.Shared.Storage;

public sealed class FileLocalDocumentStore : ILocalDocumentStore
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _rootPath;
    private readonly TimeProvider _time;
    private readonly AtomicDocumentWriter _writer;

    public FileLocalDocumentStore(string rootPath, TimeProvider? timeProvider = null)
        : this(rootPath, timeProvider ?? TimeProvider.System, new AtomicDocumentWriter()) { }

    internal FileLocalDocumentStore(string rootPath, TimeProvider timeProvider, AtomicDocumentWriter writer)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        _rootPath = Path.GetFullPath(rootPath);
        _time = timeProvider;
        _writer = writer;
    }

    public Task<LocalDocument> CreateAsync(string title, CancellationToken cancellationToken = default) =>
        LockedAsync(async () =>
        {
            LocalDocument document = LocalDocumentCodec.NewDocument(Guid.NewGuid(), CleanTitle(title), _time.GetUtcNow());
            await WriteAsync(document, cancellationToken);
            return document;
        }, cancellationToken);

    public Task<LocalDocument?> GetAsync(Guid documentId, CancellationToken cancellationToken = default) =>
        LockedAsync(() => ReadAsync(documentId, cancellationToken), cancellationToken);

    public Task<LocalDocumentList> ListAsync(LocalDocumentScope scope = LocalDocumentScope.Active,
        CancellationToken cancellationToken = default) => LockedAsync(async () =>
    {
        if (!Enum.IsDefined(scope)) throw new ArgumentOutOfRangeException(nameof(scope));
        var documents = new List<LocalDocument>();
        var issues = new List<LocalDocumentIssue>();
        foreach (string path in Directory.EnumerateFiles(_rootPath, "*.json", SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Guid.TryParseExact(Path.GetFileNameWithoutExtension(path), "N", out Guid id) || id == Guid.Empty
                || !string.Equals(Path.GetFileName(path), $"{id:N}.json", StringComparison.Ordinal))
            {
                issues.Add(new(Path.GetFileName(path), LocalDocumentIssueKind.Corrupt, "Unrecognized document filename. The file was preserved."));
                continue;
            }
            try
            {
                LocalDocument? document = await ReadAsync(id, cancellationToken);
                if (document is not null && (scope == LocalDocumentScope.All
                    || (document.DeletedAtUtc is not null) == (scope == LocalDocumentScope.Trash)))
                    documents.Add(document);
            }
            catch (LocalDocumentReadException error) { issues.Add(error.Issue); }
        }
        return new LocalDocumentList(documents.OrderByDescending(document => document.UpdatedAtUtc)
            .ThenBy(document => document.DocumentId).ToArray(), issues);
    }, cancellationToken);

    public Task<LocalDocument> SaveAsync(LocalDocument document, CancellationToken cancellationToken = default) =>
        LockedAsync(async () =>
        {
            ArgumentNullException.ThrowIfNull(document);
            LocalDocument current = await RequireAsync(document.DocumentId, document.LocalRevision, cancellationToken);
            if (current.DeletedAtUtc is not null) throw new InvalidOperationException("Restore the document before editing it.");
            // Local editing cannot change synchronization identity or bypass lifecycle operations.
            if (document.CreatedAtUtc != current.CreatedAtUtc || document.DeletedAtUtc != current.DeletedAtUtc
                || document.ServerDocumentId != current.ServerDocumentId || document.ServerProjectId != current.ServerProjectId
                || document.ServerVersion != current.ServerVersion || document.LastSyncedAtUtc != current.LastSyncedAtUtc
                || document.SyncState != current.SyncState)
                throw new InvalidOperationException("Local editing cannot change lifecycle or synchronization metadata.");
            return await CommitChangeAsync(document, current, cancellationToken);
        }, cancellationToken);

    public Task<LocalDocument> RenameAsync(Guid documentId, long revision, string title,
        CancellationToken cancellationToken = default) => ChangeAsync(documentId, revision, current =>
        {
            if (current.DeletedAtUtc is not null) throw new InvalidOperationException("Restore the document before renaming it.");
            return current with { Title = CleanTitle(title) };
        }, cancellationToken);

    public Task<LocalDocument> MoveToTrashAsync(Guid documentId, long revision,
        CancellationToken cancellationToken = default) => ChangeAsync(documentId, revision,
            current => current.DeletedAtUtc is not null ? current : current with { DeletedAtUtc = NextTime(current) }, cancellationToken);

    public Task<LocalDocument> RestoreAsync(Guid documentId, long revision,
        CancellationToken cancellationToken = default) => ChangeAsync(documentId, revision,
            current => current with { DeletedAtUtc = null }, cancellationToken);

    public Task<LocalDocument> DuplicateAsync(Guid documentId, CancellationToken cancellationToken = default) =>
        LockedAsync(async () =>
        {
            LocalDocument source = await RequireAsync(documentId, null, cancellationToken);
            DateTimeOffset now = _time.GetUtcNow();
            LocalDocument copy = source with
            {
                DocumentId = Guid.NewGuid(), Title = source.Title + " (copy)", ServerDocumentId = null,
                ServerProjectId = null, ServerVersion = null, LastSyncedAtUtc = null,
                SyncState = LocalSyncState.LocalOnly, LocalRevision = 1, DeletedAtUtc = null,
                CreatedAtUtc = now, UpdatedAtUtc = now,
                Sections = source.Sections.Select(section => section with
                {
                    SectionId = Guid.NewGuid(), ServerSectionId = null, CreatedAtUtc = now, UpdatedAtUtc = now,
                    Pages = section.Pages.Select(page => page with
                    {
                        PageId = Guid.NewGuid(), ServerPageId = null, CreatedAtUtc = now, UpdatedAtUtc = now
                    }).ToArray()
                }).ToArray()
            };
            await WriteAsync(copy, cancellationToken);
            return copy;
        }, cancellationToken);

    public Task PermanentlyDeleteAsync(Guid documentId, long revision, CancellationToken cancellationToken = default) =>
        LockedAsync(async () =>
        {
            LocalDocument current = await RequireAsync(documentId, revision, cancellationToken);
            if (current.DeletedAtUtc is null) throw new InvalidOperationException("Move the document to trash before deleting it permanently.");
            // Cloud deletion is handled by the sync queue; retain linked local writing for recovery.
            if (current.ServerDocumentId is not null)
                throw new InvalidOperationException("Use the cloud sync deletion controls. Linked local copies are retained for recovery.");
            cancellationToken.ThrowIfCancellationRequested();
            string path = GetPath(documentId);
            File.Delete(path + ".legacy.bak");
            foreach (string temporary in Directory.EnumerateFiles(_rootPath, Path.GetFileName(path) + "*.tmp"))
                File.Delete(temporary);
            File.Delete(path);
            return true;
        }, cancellationToken);

    private Task<LocalDocument> ChangeAsync(Guid id, long revision, Func<LocalDocument, LocalDocument> change,
        CancellationToken cancellationToken) => LockedAsync(async () =>
        {
            LocalDocument current = await RequireAsync(id, revision, cancellationToken);
            LocalDocument changed = change(current);
            return changed == current ? current : await CommitChangeAsync(changed, current, cancellationToken);
        }, cancellationToken);

    // Synchronization uses compare-and-swap; network work never holds the document-store lock.
    internal Task<LocalDocument> ApplySyncAsync(LocalDocument document, long? expectedRevision, CancellationToken ct) =>
        LockedAsync(async () =>
        {
            var current = await ReadAsync(document.DocumentId, ct);
            if (current?.LocalRevision != expectedRevision) throw new LocalDocumentConflictException(document.DocumentId);
            var updated = document with { LocalRevision = checked((current?.LocalRevision ?? 0) + 1) };
            LocalDocumentCodec.Validate(updated);
            await WriteAsync(updated, ct);
            return updated;
        }, ct);

    private async Task<LocalDocument> CommitChangeAsync(LocalDocument document, LocalDocument current, CancellationToken cancellationToken)
    {
        DateTimeOffset now = NextTime(current);
        if (document.DeletedAtUtc is { } deleted && deleted > now) now = deleted;
        document = document with { UpdatedAtUtc = now };
        LocalDocumentCodec.Validate(document);
        var oldSections = current.Sections.ToDictionary(section => section.SectionId);
        LocalDocument changed = document with
        {
            UpdatedAtUtc = now, LocalRevision = checked(current.LocalRevision + 1),
            SyncState = current.SyncState == LocalSyncState.Conflict ? LocalSyncState.Conflict
                : current.ServerDocumentId is null ? LocalSyncState.LocalOnly : LocalSyncState.PendingUpload,
            Sections = document.Sections.OrderBy(section => section.OrderIndex).Select(section =>
            {
                oldSections.TryGetValue(section.SectionId, out LocalSection? previous);
                if (section.ServerSectionId != previous?.ServerSectionId)
                    throw new InvalidOperationException("Local editing cannot change server section identity.");
                var oldPages = previous?.Pages.ToDictionary(page => page.PageId);
                LocalPage[] pages = section.Pages.OrderBy(page => page.OrderIndex).Select(page =>
                {
                    LocalPage? old = null;
                    oldPages?.TryGetValue(page.PageId, out old);
                    if (page.ServerPageId != old?.ServerPageId)
                        throw new InvalidOperationException("Local editing cannot change server page identity.");
                    return page with { CreatedAtUtc = old?.CreatedAtUtc ?? now,
                        UpdatedAtUtc = old is not null && page == old ? old.UpdatedAtUtc : now };
                }).ToArray();
                bool unchanged = previous is not null && section.Title == previous.Title
                    && section.OrderIndex == previous.OrderIndex && section.NarrativePurpose == previous.NarrativePurpose
                    && section.LanguageCode == previous.LanguageCode && pages.SequenceEqual(previous.Pages);
                return section with { Pages = pages, CreatedAtUtc = previous?.CreatedAtUtc ?? now,
                    UpdatedAtUtc = unchanged ? previous!.UpdatedAtUtc : now };
            }).ToArray()
        };
        await WriteAsync(changed, cancellationToken);
        return changed;
    }

    private DateTimeOffset NextTime(LocalDocument document)
    {
        DateTimeOffset now = _time.GetUtcNow();
        return now > document.UpdatedAtUtc ? now : document.UpdatedAtUtc;
    }

    private async Task<LocalDocument> RequireAsync(Guid id, long? revision, CancellationToken cancellationToken)
    {
        LocalDocument document = await ReadAsync(id, cancellationToken)
            ?? throw new FileNotFoundException($"Local document {id} was not found.");
        if (revision.HasValue && revision != document.LocalRevision) throw new LocalDocumentConflictException(id);
        return document;
    }

    private async Task<LocalDocument?> ReadAsync(Guid id, CancellationToken cancellationToken)
    {
        string path = GetPath(id);
        try
        {
            byte[] bytes;
            try { bytes = await File.ReadAllBytesAsync(path, cancellationToken); }
            catch (FileNotFoundException) { return null; }
            (LocalDocument document, bool migrated) = LocalDocumentCodec.Decode(bytes, id, Path.GetFileName(path));
            if (migrated)
            {
                // Preserve the exact legacy bytes before replacing the source, including after a failed retry.
                string backup = path + ".legacy.bak";
                if (!File.Exists(backup)) await _writer.WriteAsync(backup, bytes, cancellationToken);
                else if (!(await File.ReadAllBytesAsync(backup, cancellationToken)).SequenceEqual(bytes))
                    throw new IOException("The migration backup differs from the source.");
                await WriteAsync(document, cancellationToken);
            }
            return document;
        }
        catch (LocalDocumentReadException) { throw; }
        catch (JsonException error)
        {
            throw new LocalDocumentReadException(new(Path.GetFileName(path), LocalDocumentIssueKind.Corrupt,
                "This document cannot be read. Its file was preserved for recovery."), error);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            throw new LocalDocumentReadException(new(Path.GetFileName(path), LocalDocumentIssueKind.Unavailable,
                "This document is unavailable. Check storage access and retry; the source file was preserved."), error);
        }
    }

    private Task WriteAsync(LocalDocument document, CancellationToken cancellationToken) =>
        _writer.WriteAsync(GetPath(document.DocumentId), LocalDocumentCodec.Encode(document), cancellationToken);

    private string GetPath(Guid id)
    {
        if (id == Guid.Empty) throw new ArgumentException("A document ID is required.", nameof(id));
        return Path.Combine(_rootPath, $"{id:N}.json");
    }

    private static string CleanTitle(string title)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        return title.Trim();
    }

    private async Task<T> LockedAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(_rootPath);
            // Cross-instance/process lock: a competing app gets a retryable IO failure, never a lost update.
            using var lease = new FileStream(Path.Combine(_rootPath, ".store.lock"), FileMode.OpenOrCreate,
                FileAccess.ReadWrite, FileShare.None);
            cancellationToken.ThrowIfCancellationRequested();
            return await action();
        }
        finally { _gate.Release(); }
    }
}
