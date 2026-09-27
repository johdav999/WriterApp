namespace WriterApp.Device.Shared.Storage;

public interface ILocalDocumentStore
{
    string RootPath { get; }

    Task<LocalDocumentDraft?> GetAsync(Guid documentId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LocalDocumentDraft>> ListAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(LocalDocumentDraft document, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid documentId, CancellationToken cancellationToken = default);
}
