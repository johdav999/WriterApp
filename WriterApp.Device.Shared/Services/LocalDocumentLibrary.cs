using WriterApp.Device.Shared.Storage;

namespace WriterApp.Device.Shared.Services;

/// <summary>State for the document library; every mutation refreshes the current view from disk.</summary>
public sealed class LocalDocumentLibrary(LocalDocumentRepository repository)
{
    public LocalDocumentScope Scope { get; private set; } = LocalDocumentScope.Active;
    public IReadOnlyList<LocalDocument> Documents { get; private set; } = [];
    public IReadOnlyList<LocalDocument> ActiveDocuments { get; private set; } = [];
    public IReadOnlyList<LocalDocumentIssue> Issues { get; private set; } = [];

    public async Task RefreshAsync(LocalDocumentScope scope, CancellationToken cancellationToken = default)
    {
        LocalDocumentList result = await repository.ListAsync(scope, cancellationToken);
        Scope = scope;
        Documents = result.Documents;
        ActiveDocuments = scope == LocalDocumentScope.Active ? result.Documents
            : (await repository.ListAsync(LocalDocumentScope.Active, cancellationToken)).Documents;
        Issues = result.Issues;
    }

    public async Task<LocalDocument> CreateAsync(string title, CancellationToken cancellationToken = default)
    {
        LocalDocument document = await repository.CreateAsync(title, cancellationToken);
        await RefreshAsync(LocalDocumentScope.Active, cancellationToken);
        return document;
    }

    public async Task<LocalDocument> CreateProjectAsync(string title, CancellationToken cancellationToken = default)
    {
        LocalDocument document = await repository.CreateProjectAsync(title, cancellationToken);
        await RefreshAsync(LocalDocumentScope.Active, cancellationToken);
        return document;
    }

    public async Task<LocalDocument> CreateProjectDocumentAsync(Guid projectId, string title, CancellationToken ct = default)
    {
        if (!ActiveDocuments.Any(d => d.Project?.ProjectId == projectId))
            throw new InvalidOperationException("Choose an active project.");
        var document = await repository.CreateProjectDocumentAsync(projectId, title, "manuscript", ct);
        await RefreshAsync(LocalDocumentScope.Active, ct);
        return document;
    }

    public async Task<LocalDocument> MoveStandaloneToProjectAsync(Guid documentId, Guid projectId, CancellationToken ct = default)
    {
        var source = ActiveDocuments.SingleOrDefault(d => d.DocumentId == documentId)
            ?? throw new InvalidOperationException("Choose an active standalone document.");
        var target = ActiveDocuments.FirstOrDefault(d => d.Project?.ProjectId == projectId)
            ?? throw new InvalidOperationException("Choose an active project.");
        var document = await repository.MoveStandaloneToProjectAsync(source, target.DocumentId, ct);
        await RefreshAsync(Scope, ct);
        return document;
    }

    public async Task<LocalDocument> RenameProjectAsync(LocalDocument document, string title, CancellationToken cancellationToken = default)
    {
        LocalDocument renamed = await repository.ChangeProjectAsync(document,
            new(LocalProjectAction.RenameProject, Title: title), cancellationToken);
        await RefreshAsync(Scope, cancellationToken);
        return renamed;
    }

    public async Task<LocalDocument> CreateImportedAsync(DevicePreparedImport import, CancellationToken cancellationToken = default)
    {
        LocalDocument document = await repository.CreateImportedAsync(import.Title, import.Html, cancellationToken);
        await RefreshAsync(LocalDocumentScope.Active, cancellationToken);
        return document;
    }

    public async Task<LocalDocument> RenameAsync(LocalDocument document, string title, CancellationToken cancellationToken = default)
    {
        LocalDocument renamed = await repository.RenameAsync(document, title, cancellationToken);
        await RefreshAsync(Scope, cancellationToken);
        return renamed;
    }

    public async Task<LocalDocument> DuplicateAsync(LocalDocument document, CancellationToken cancellationToken = default)
    {
        LocalDocument copy = await repository.DuplicateAsync(document.DocumentId, cancellationToken);
        await RefreshAsync(LocalDocumentScope.Active, cancellationToken);
        return copy;
    }

    public async Task MoveToTrashAsync(LocalDocument document, CancellationToken cancellationToken = default)
    {
        await repository.MoveToTrashAsync(document, cancellationToken);
        await RefreshAsync(Scope, cancellationToken);
    }

    public async Task SetProjectTrashAsync(Guid projectId, bool trash, CancellationToken ct = default)
    {
        var members = (await repository.ListAsync(trash ? LocalDocumentScope.Active : LocalDocumentScope.Trash, ct)).Documents
            .Where(d => d.Project?.ProjectId == projectId).ToArray();
        foreach (var member in members)
        {
            if (trash) await repository.MoveToTrashAsync(member, ct);
            else await repository.RestoreAsync(member, ct);
        }
        await RefreshAsync(Scope, ct);
    }

    public async Task RestoreAsync(LocalDocument document, CancellationToken cancellationToken = default)
    {
        await repository.RestoreAsync(document, cancellationToken);
        await RefreshAsync(Scope, cancellationToken);
    }

    public async Task PermanentlyDeleteAsync(LocalDocument document, CancellationToken cancellationToken = default)
    {
        await repository.PermanentlyDeleteAsync(document, cancellationToken);
        await RefreshAsync(Scope, cancellationToken);
    }
}
