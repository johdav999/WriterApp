using WriterApp.Device.Shared.Storage;

namespace WriterApp.Device.Shared.Services;

/// <summary>Tracks in-memory page edits and commits snapshots without losing edits made during a save.</summary>
public sealed class LocalEditorSession(LocalDocumentRepository repository, LocalDocument document)
{
    private readonly Dictionary<Guid, string> _pending = [];
    private readonly SemaphoreSlim _saveGate = new(1, 1);
    private bool _saving;
    private Guid? _contextPage;
    public void RememberPage(Guid? pageId)
    {
        if (Document.Project is not null && pageId is { } id && Document.Sections.SelectMany(s => s.Pages).Any(p => p.PageId == id)) _contextPage = id;
    }
    public LocalDocument Document { get; private set; } = document;
    public bool IsDirty => _pending.Count != 0 || (_contextPage is not null && _contextPage != Document.Project?.LastPageId);
    public LocalSaveState SaveState { get; private set; } = LocalSaveState.Saved;
    public string ContentFor(LocalPage page) => _pending.GetValueOrDefault(page.PageId, page.Content);
    public LocalContentFormat FormatFor(LocalPage page) => _pending.ContainsKey(page.PageId) ? LocalContentFormat.Html : page.ContentFormat;

    /// <summary>Refresh cloud acknowledgments without replacing writing or pending editor changes.</summary>
    public async Task<bool> RefreshSyncMetadataAsync(CancellationToken cancellationToken = default)
    {
        await _saveGate.WaitAsync(cancellationToken);
        try
        {
            var latest = await repository.LoadAsync(Document.DocumentId, cancellationToken);
            if (latest is null || latest.LocalRevision <= Document.LocalRevision
                || DeviceSyncMapping.Fingerprint(latest) != DeviceSyncMapping.Fingerprint(Document)) return false;
            Document = latest;
            return true;
        }
        finally { _saveGate.Release(); }
    }

    public void AdoptSavedDocument(LocalDocument saved)
    {
        if (saved.DocumentId != Document.DocumentId || IsDirty || saved.LocalRevision <= Document.LocalRevision)
            throw new InvalidOperationException("The editor cannot adopt a stale or unsaved document.");
        Document = saved;
        SaveState = LocalSaveState.Saved;
    }

    public LocalDocument Snapshot() => Document with
    {
        Project = Document.Project is { } project ? project with { LastPageId = _contextPage ?? project.LastPageId } : null,
        Sections = Document.Sections.Select(section => section with
        {
            Pages = section.Pages.Select(page => _pending.TryGetValue(page.PageId, out string? html)
                ? page with { Content = html, ContentFormat = LocalContentFormat.Html } : page).ToArray()
        }).ToArray()
    };

    public void Edit(Guid pageId, string html)
    {
        if (Document.DeletedAtUtc is not null) throw new InvalidOperationException("Restore the document before editing.");
        LocalPage page = Document.Sections.SelectMany(section => section.Pages).Single(item => item.PageId == pageId);
        DeviceContentCompatibility.RequireEditable(page.Content, page.ContentFormat);
        DeviceContentCompatibility.RequireEditable(html, LocalContentFormat.Html);
        if (!_saving && page.ContentFormat == LocalContentFormat.Html && page.Content == html) _pending.Remove(pageId);
        else _pending[pageId] = html;
        SaveState = IsDirty ? LocalSaveState.Unsaved : LocalSaveState.Saved;
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        await _saveGate.WaitAsync(cancellationToken);
        try
        {
            if (!IsDirty) return;
            var latest = await repository.LoadAsync(Document.DocumentId, cancellationToken);
            if (latest is not null && latest.LocalRevision != Document.LocalRevision)
            {
                // Sync acknowledgments can advance only metadata while typing continues.
                // Never rebase over changed writing or remote deletion/trash.
                if (DeviceSyncMapping.Fingerprint(latest) != DeviceSyncMapping.Fingerprint(Document))
                    throw new LocalDocumentConflictException(Document.DocumentId);
                Document = latest;
            }
            var snapshot = new Dictionary<Guid, string>(_pending);
            LocalDocument updated = Document with
            {
                Project = Document.Project is { } project ? project with { LastPageId = _contextPage ?? project.LastPageId } : null,
                Sections = Document.Sections.Select(section => section with
                {
                    Pages = section.Pages.Select(page => snapshot.TryGetValue(page.PageId, out string? html)
                        ? page with { Content = html, ContentFormat = LocalContentFormat.Html } : page).ToArray()
                }).ToArray()
            };
            SaveState = LocalSaveState.Saving;
            _saving = true;
            Document = await repository.SaveAsync(updated, cancellationToken);
            foreach ((Guid id, string html) in snapshot)
                if (_pending.TryGetValue(id, out string? pending) && pending == html) _pending.Remove(id);
            SaveState = IsDirty ? LocalSaveState.Unsaved : LocalSaveState.Saved;
        }
        catch { SaveState = LocalSaveState.Error; throw; }
        finally { _saving = false; _saveGate.Release(); }
    }
}
