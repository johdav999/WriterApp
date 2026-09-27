using WriterApp.Device.Shared.Storage;

namespace WriterApp.Device.Shared.Services;

/// <summary>State for the document library; every mutation refreshes the current view from disk.</summary>
public sealed class LocalDocumentLibrary(LocalDocumentRepository repository)
{
    public LocalDocumentScope Scope { get; private set; } = LocalDocumentScope.Active;
    public IReadOnlyList<LocalDocument> Documents { get; private set; } = [];
    public IReadOnlyList<LocalDocumentIssue> Issues { get; private set; } = [];

    public async Task RefreshAsync(LocalDocumentScope scope, CancellationToken cancellationToken = default)
    {
        LocalDocumentList result = await repository.ListAsync(scope, cancellationToken);
        Scope = scope;
        Documents = result.Documents;
        Issues = result.Issues;
    }

    public async Task<LocalDocument> CreateAsync(string title, CancellationToken cancellationToken = default)
    {
        LocalDocument document = await repository.CreateAsync(title, cancellationToken);
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
