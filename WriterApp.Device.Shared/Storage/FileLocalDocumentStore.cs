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

    public Task<LocalDocument> CreateProjectAsync(string title, CancellationToken cancellationToken = default) =>
        LockedAsync(async () =>
        {
            var document = Services.LocalProjectStructure.Attach(LocalDocumentCodec.NewDocument(Guid.NewGuid(), CleanTitle(title), _time.GetUtcNow()), title);
            await WriteAsync(document, cancellationToken);
            return document;
        }, cancellationToken);

    public Task<LocalDocument> CreateProjectDocumentAsync(Guid projectId, string title, string kind, CancellationToken ct = default) =>
        LockedAsync(async () =>
        {
            if (kind is not ("manuscript" or "notes" or "synopsis" or "outline" or "other"))
                throw new InvalidOperationException("Unknown document kind.");
            var metadata = await ReadMetadataAsync(projectId, ct) ?? throw new InvalidOperationException("Project unavailable.");
            var document = LocalDocumentCodec.NewDocument(Guid.NewGuid(), CleanTitle(title), _time.GetUtcNow()) with { Kind = kind };
            document = Services.LocalProjectStructure.Attach(document, metadata.Title);
            document = document with { Project = metadata.Apply(document.Project!) with
                { Version = 3, ManuscriptId = document.DocumentId, Nodes = kind == "manuscript" ? document.Project!.Nodes : [] } };
            await WriteAsync(document, ct);
            return document;
        }, ct);

    public Task<LocalDocument> MoveStandaloneToProjectAsync(LocalDocument source, Guid targetDocumentId, CancellationToken ct = default) =>
        LockedAsync(async () =>
        {
            var current = await RequireAsync(source.DocumentId, source.LocalRevision, ct);
            var target = await RequireAsync(targetDocumentId, null, ct);
            if (current.SyncState == LocalSyncState.Conflict)
                throw new InvalidOperationException("Resolve this document's sync conflict before moving it.");
            if (target.Project is not { } project || target.DeletedAtUtc is not null)
                throw new InvalidOperationException("Choose an active project.");
            return await CommitChangeAsync(Services.LocalProjectStructure.AttachToProject(current, project), current, ct);
        }, ct);

    public Task<LocalDocument> UpdateProjectMetadataAsync(LocalDocument source, string? title = null, bool makePrimary = false, CancellationToken ct = default) =>
        LockedAsync(async () =>
        {
            var current = await RequireAsync(source.DocumentId, source.LocalRevision, ct);
            var p = current.Project ?? throw new InvalidOperationException("Project unavailable.");
            if (source.Project?.MetadataRevision != p.MetadataRevision) throw new LocalDocumentConflictException(source.DocumentId);
            if (makePrimary && (current.Kind != "manuscript" || current.DeletedAtUtc is not null))
                throw new InvalidOperationException("Choose an active manuscript as the primary document.");
            var metadata = LocalProjectMetadata.From(p) with
            {
                Title = title is null ? p.Title : CleanTitle(title),
                PrimaryDocumentId = makePrimary ? current.DocumentId : p.PrimaryDocumentId ?? current.DocumentId,
                ServerPrimaryDocumentId = makePrimary ? current.ServerDocumentId ?? p.ServerPrimaryDocumentId : p.ServerPrimaryDocumentId,
                MetadataRevision = checked(p.MetadataRevision + 1), MetadataDirty = true
            };
            if (metadata.Title.Length > 200) throw new InvalidOperationException("Use a title of 1–200 characters.");
            await WriteMetadataAsync(metadata, ct);
            return await CommitChangeAsync(current with { Project = metadata.Apply(p) }, current, ct);
        }, ct);

    public Task<LocalDocument> CreateImportedAsync(string title, string html, CancellationToken cancellationToken = default) =>
        LockedAsync(async () =>
        {
            ArgumentNullException.ThrowIfNull(html);
            LocalDocument document = LocalDocumentCodec.NewDocument(Guid.NewGuid(), CleanTitle(title),
                _time.GetUtcNow(), html, LocalContentFormat.Html);
            await WriteAsync(document, cancellationToken);
            return document;
        }, cancellationToken);

    public Task<LocalDocument?> GetAsync(Guid documentId, CancellationToken cancellationToken = default) =>
        LockedAsync(() => ReadAsync(documentId, cancellationToken), cancellationToken);

    public Task<LocalSearchDocuments> ReadSearchDocumentsAsync(CancellationToken cancellationToken = default) => LockedAsync(async () =>
    {
        const long byteBudget = 32 * 1024 * 1024;
        var documents = new List<LocalDocument>(); var issues = new List<LocalDocumentIssue>();
        int scanned = 0; long bytes = 0; bool limited = false;
        foreach (string path in Directory.EnumerateFiles(_rootPath, "*.json", SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (++scanned > 256) { limited = true; break; }
            if (!Guid.TryParseExact(Path.GetFileNameWithoutExtension(path), "N", out Guid id) || id == Guid.Empty
                || !string.Equals(Path.GetFileName(path), $"{id:N}.json", StringComparison.Ordinal))
            { issues.Add(new(Path.GetFileName(path), LocalDocumentIssueKind.Corrupt, "Unrecognized document filename.")); continue; }
            try
            {
                long length = new FileInfo(path).Length;
                if (length > byteBudget - bytes) { limited = true; continue; }
                bytes += length;
                var document = await ReadAsync(id, cancellationToken);
                if (document?.DeletedAtUtc is null && document is not null) documents.Add(document);
            }
            catch (LocalDocumentReadException error) { issues.Add(error.Issue); }
            catch (IOException) { issues.Add(new(Path.GetFileName(path), LocalDocumentIssueKind.Unavailable, "Document unavailable; retry search.")); }
        }
        return new LocalSearchDocuments(documents.OrderBy(d => d.Title, StringComparer.OrdinalIgnoreCase).ThenBy(d => d.DocumentId).ToArray(), issues, limited);
    }, cancellationToken);

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
            if (current.Project is null && document.Project is not null && (current.ServerDocumentId is not null || current.SyncState != LocalSyncState.LocalOnly))
                throw new InvalidOperationException("Duplicate the cloud document before attaching a local project.");
            if (current.Project is { } project && (document.Project is null || document.Project.ProjectId != project.ProjectId
                || document.Project.ManuscriptId != project.ManuscriptId || document.Project.ServerProjectId != project.ServerProjectId))
                throw new InvalidOperationException("Saving cannot detach or replace the project's identity.");
            if (current.DeletedAtUtc is not null) throw new InvalidOperationException("Restore the document before editing it.");
            // Ordinary manuscript saves cannot overwrite shared metadata from a stale editor snapshot.
            if (current.Project is { } authoritative && document.Project is { } incoming)
            {
                var shared = LocalProjectMetadata.From(authoritative);
                var proposed = LocalProjectMetadata.From(incoming) with { PrimaryDocumentId = shared.PrimaryDocumentId,
                    ServerPrimaryDocumentId = shared.ServerPrimaryDocumentId, ServerMetadataRevision = shared.ServerMetadataRevision };
                if (incoming.MetadataRevision == authoritative.MetadataRevision && proposed != shared)
                {
                    shared = proposed with { MetadataRevision = checked(shared.MetadataRevision + 1), MetadataDirty = true };
                    await WriteMetadataAsync(shared, cancellationToken);
                }
                document = document with { Project = shared.Apply(incoming) };
            }
            // Local editing cannot change synchronization identity or bypass lifecycle operations.
            if (document.CreatedAtUtc != current.CreatedAtUtc || document.DeletedAtUtc != current.DeletedAtUtc
                || document.ServerDocumentId != current.ServerDocumentId || document.ServerProjectId != current.ServerProjectId
                || document.ServerVersion != current.ServerVersion || document.LastSyncedAtUtc != current.LastSyncedAtUtc
                || document.SyncState != current.SyncState)
                throw new InvalidOperationException("Local editing cannot change lifecycle or synchronization metadata.");
            return await CommitChangeAsync(Services.LocalPlanning.Reconcile(current, document), current, cancellationToken);
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
            LocalDocument detached = Services.LocalDocumentStructure.DetachIdentities(source);
            LocalDocument copy = detached with
            {
                DocumentId = Guid.NewGuid(), Title = source.Title + " (copy)", ServerDocumentId = null,
                ServerProjectId = null, ServerVersion = null, LastSyncedAtUtc = null,
                SyncState = LocalSyncState.LocalOnly, LocalRevision = 1, DeletedAtUtc = null,
                CreatedAtUtc = now, UpdatedAtUtc = now,
                Sections = detached.Sections.Select(section => section with
                {
                    CreatedAtUtc = now, UpdatedAtUtc = now,
                    Pages = section.Pages.Select(page => page with
                    {
                        CreatedAtUtc = now, UpdatedAtUtc = now
                    }).ToArray()
                }).ToArray()
            };
            await WriteAsync(copy, cancellationToken);
            return copy;
        }, cancellationToken);

    public Task<LocalDocument> RecoverSnapshotAsync(LocalDocument source, CancellationToken cancellationToken = default) =>
        LockedAsync(async () =>
        {
            LocalDocumentCodec.Validate(source);
            var now = _time.GetUtcNow();
            var copy = Services.LocalDocumentStructure.DetachIdentities(source, preserveProject: true) with
            {
                DocumentId = Guid.NewGuid(), Title = CleanTitle("Before AI — " + source.Title),
                ServerDocumentId = null, ServerProjectId = null, ServerVersion = null, LastSyncedAtUtc = null,
                SyncState = LocalSyncState.LocalOnly, LocalRevision = 1, DeletedAtUtc = null,
                CreatedAtUtc = now, UpdatedAtUtc = now
            };
            if (copy.Project is { } project) copy = copy with { Project = project with { ManuscriptId = copy.DocumentId, PrimaryDocumentId = copy.Kind == "manuscript" ? copy.DocumentId : null } };
            LocalDocumentCodec.Validate(copy);
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
            if (current.Project is not null)
                throw new InvalidOperationException("Project manuscripts are retained in Trash for recovery. Restore the manuscript to reopen its project.");
            cancellationToken.ThrowIfCancellationRequested();
            string path = GetPath(documentId);
            File.Delete(path + ".legacy.bak");
            File.Delete(path + ".v1.bak");
            File.Delete(path + ".v2.bak");
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
    internal Task<LocalDocument> ApplySyncAsync(LocalDocument document, long? expectedRevision, CancellationToken ct, bool projects = false, bool overwriteProjectMetadata = false) =>
        LockedAsync(async () =>
        {
            var current = await ReadAsync(document.DocumentId, ct);
            if (current?.LocalRevision != expectedRevision) throw new LocalDocumentConflictException(document.DocumentId);
            if (!projects && (current?.Project is not null || document.Project is not null))
                throw new InvalidOperationException("Local project synchronization is not available yet.");
            var updated = document with { LocalRevision = checked((current?.LocalRevision ?? 0) + 1) };
            if (updated.Project is { } incoming)
            {
                var canonical = await FindMetadataAsync(incoming.ProjectId, incoming.ServerProjectId, ct);
                if (canonical is not null) updated = updated with { Project = incoming with { ProjectId = canonical.ProjectId } };
                var metadata = LocalProjectMetadata.From(updated.Project!);
                if (metadata.ServerPrimaryDocumentId is { } cloudPrimary)
                {
                    if (document.ServerDocumentId == cloudPrimary) metadata = metadata with { PrimaryDocumentId = document.DocumentId };
                    else if (await FindLocalPrimaryAsync(cloudPrimary, ct) is { } localPrimary) metadata = metadata with { PrimaryDocumentId = localPrimary };
                }
                Guid? intendedPrimary = canonical is null ? null : canonical.PrimaryDocumentId == document.DocumentId ? document.ServerDocumentId
                    : (await ReadAsync(canonical.PrimaryDocumentId, ct))?.ServerDocumentId ?? canonical.ServerPrimaryDocumentId;
                bool preservesPendingMetadata = overwriteProjectMetadata || canonical?.MetadataDirty != true || (metadata.Title == canonical.Title
                    && metadata.Subtitle == canonical.Subtitle && metadata.AuthorName == canonical.AuthorName && metadata.Language == canonical.Language
                    && metadata.Genre == canonical.Genre && metadata.DefaultExportSettingsJson == canonical.DefaultExportSettingsJson
                    && metadata.CoverImageUrl == canonical.CoverImageUrl && metadata.ServerPrimaryDocumentId == intendedPrimary);
                if (preservesPendingMetadata && (canonical is null || (metadata.ServerMetadataRevision ?? 0) >= (canonical.ServerMetadataRevision ?? 0)))
                {
                    metadata = metadata with { MetadataRevision = canonical?.MetadataRevision ?? metadata.MetadataRevision,
                        MetadataDirty = !overwriteProjectMetadata && document.SyncState != LocalSyncState.Synced && canonical?.MetadataDirty == true };
                    // Preserve local primary identity when the cloud identifier still points at that same document.
                    if (!overwriteProjectMetadata && canonical is not null && canonical.ServerPrimaryDocumentId == metadata.ServerPrimaryDocumentId)
                        metadata = metadata with { PrimaryDocumentId = canonical.PrimaryDocumentId };
                    if (document.ServerDocumentId == metadata.ServerPrimaryDocumentId)
                        metadata = metadata with { PrimaryDocumentId = document.DocumentId };
                    await WriteMetadataAsync(metadata, ct);
                }
                updated = await HydrateProjectAsync(updated, ct);
            }
            LocalDocumentCodec.Validate(updated);
            await BackupPlanningUpgradeAsync(current, updated, ct);
            await WriteAsync(updated, ct);
            return updated;
        }, ct);

    private async Task<LocalDocument> CommitChangeAsync(LocalDocument document, LocalDocument current, CancellationToken cancellationToken)
    {
        DateTimeOffset now = NextTime(current);
        if (document.DeletedAtUtc is { } deleted && deleted > now) now = deleted;
        document = document with { UpdatedAtUtc = now };
        LocalDocumentCodec.Validate(document);
        await BackupPlanningUpgradeAsync(current, document, cancellationToken);
        var oldSections = current.Sections.Concat(current.DeletedSections.Select(x => x.Section))
            .ToDictionary(section => section.SectionId);
        var oldPages = oldSections.Values.SelectMany(s => s.Pages).Concat(current.DeletedPages.Select(x => x.Page))
            .ToDictionary(page => page.PageId);
        // Trash is part of the same aggregate, including its server identities and source payload.
        foreach (var section in document.DeletedSections.Select(x => x.Section))
        {
            if (!oldSections.TryGetValue(section.SectionId, out var old) || !SameSource(section, old))
                throw new InvalidOperationException("Section trash must retain its original source.");
        }
        foreach (var page in document.DeletedPages.Select(x => x.Page))
        {
            if (!oldPages.TryGetValue(page.PageId, out var old) || !SameSource(page, old))
                throw new InvalidOperationException("Page trash must retain its original source.");
        }
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
                LocalPage[] pages = section.Pages.OrderBy(page => page.OrderIndex).Select(page =>
                {
                    LocalPage? old = null;
                    oldPages.TryGetValue(page.PageId, out old);
                    if (page.ServerPageId != old?.ServerPageId)
                        throw new InvalidOperationException("Local editing cannot change server page identity.");
                    return page with { CreatedAtUtc = old?.CreatedAtUtc ?? now,
                        UpdatedAtUtc = old is not null && SameSource(page, old) ? old.UpdatedAtUtc : now };
                }).ToArray();
                bool unchanged = previous is not null && SameSource(section with { Pages = pages }, previous);
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

    private static bool SameSource<T>(T left, T right) =>
        JsonSerializer.SerializeToUtf8Bytes(left).SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(right));

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
                using var sourceJson = JsonDocument.Parse(bytes);
                string backup = path + (sourceJson.RootElement.TryGetProperty("schemaVersion", out var version) ? $".v{version.GetInt32()}.bak" : ".legacy.bak");
                if (!File.Exists(backup)) await _writer.WriteAsync(backup, bytes, cancellationToken);
                else if (!(await File.ReadAllBytesAsync(backup, cancellationToken)).SequenceEqual(bytes))
                    throw new IOException("The migration backup differs from the source.");
                await WriteAsync(document, cancellationToken);
            }
            return await HydrateProjectAsync(document, cancellationToken);
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

    private async Task BackupPlanningUpgradeAsync(LocalDocument? current, LocalDocument next, CancellationToken ct)
    {
        if (current?.Project?.Version != 1 || next.Project?.Version != 2) return;
        string source = GetPath(current.DocumentId), backup = source + ".project-v1.bak";
        if (!File.Exists(backup)) await _writer.WriteAsync(backup, await File.ReadAllBytesAsync(source, ct), ct);
    }
    private async Task WriteAsync(LocalDocument document, CancellationToken cancellationToken)
    {
        document = await HydrateProjectAsync(document, cancellationToken);
        await _writer.WriteAsync(GetPath(document.DocumentId), LocalDocumentCodec.Encode(document), cancellationToken);
    }

    private string MetadataPath(Guid id) => Path.Combine(_rootPath, "projects", $"{id:N}.json");
    // Called only after the user chooses the retained local copy in a metadata conflict.
    internal Task RebaseProjectSyncRevisionAsync(LocalDocument source, long revision, CancellationToken ct) => LockedAsync(async () =>
    {
        var current = await RequireAsync(source.DocumentId, source.LocalRevision, ct);
        if (current.Project is not { } p) return true;
        await WriteMetadataAsync(LocalProjectMetadata.From(p) with { ServerMetadataRevision = revision }, ct);
        return true;
    }, ct);
    private async Task<Guid?> FindLocalPrimaryAsync(Guid serverId, CancellationToken ct)
    {
        foreach (var path in Directory.EnumerateFiles(_rootPath, "*.json"))
        {
            if (!Guid.TryParseExact(Path.GetFileNameWithoutExtension(path), "N", out var id)) continue;
            try
            {
                if ((await ReadAsync(id, ct))?.ServerDocumentId == serverId) return id;
            }
            catch (LocalDocumentReadException) { /* An unreadable sibling remains available through recovery. */ }
        }
        return null;
    }
    private async Task<LocalProjectMetadata?> ReadMetadataAsync(Guid id, CancellationToken ct)
    {
        if (!File.Exists(MetadataPath(id))) return null;
        var value = JsonSerializer.Deserialize<LocalProjectMetadata>(await File.ReadAllBytesAsync(MetadataPath(id), ct));
        if (value is null || value.Version != 1 || value.ProjectId != id || value.PrimaryDocumentId == Guid.Empty
            || string.IsNullOrWhiteSpace(value.Title)) throw new JsonException("Invalid project metadata; original preserved.");
        return value;
    }
    private async Task<LocalProjectMetadata?> FindMetadataAsync(Guid id, Guid? serverId, CancellationToken ct)
    {
        var direct = await ReadMetadataAsync(id, ct);
        if (direct is not null || serverId is null || !Directory.Exists(Path.Combine(_rootPath, "projects"))) return direct;
        foreach (var file in Directory.EnumerateFiles(Path.Combine(_rootPath, "projects"), "*.json"))
            if (Guid.TryParseExact(Path.GetFileNameWithoutExtension(file), "N", out var candidate)
                && await ReadMetadataAsync(candidate, ct) is { } metadata && metadata.ServerProjectId == serverId) return metadata;
        return null;
    }
    private Task WriteMetadataAsync(LocalProjectMetadata metadata, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.Combine(_rootPath, "projects"));
        return _writer.WriteAsync(MetadataPath(metadata.ProjectId), JsonSerializer.SerializeToUtf8Bytes(metadata), ct);
    }
    private async Task<LocalDocument> HydrateProjectAsync(LocalDocument document, CancellationToken ct)
    {
        if (document.Project is not { } project) return document;
        var metadata = await FindMetadataAsync(project.ProjectId, project.ServerProjectId, ct);
        if (metadata is null)
        {
            metadata = LocalProjectMetadata.From(project);
            await WriteMetadataAsync(metadata, ct);
        }
        return document with { Project = metadata.Apply(project) };
    }

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
