using System.Text.Json;

namespace WriterApp.Device.Shared.Storage;

public sealed class FileLocalDocumentStore : ILocalDocumentStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    private readonly SemaphoreSlim _gate = new(1, 1);

    public FileLocalDocumentStore(string rootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        RootPath = Path.GetFullPath(rootPath);
    }

    public string RootPath { get; }

    public async Task<LocalDocumentDraft?> GetAsync(
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        string path = GetDocumentPath(documentId);
        if (!File.Exists(path))
        {
            return null;
        }

        await using FileStream stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<LocalDocumentDraft>(
            stream,
            SerializerOptions,
            cancellationToken);
    }

    public async Task<IReadOnlyList<LocalDocumentDraft>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(RootPath))
        {
            return Array.Empty<LocalDocumentDraft>();
        }

        var documents = new List<LocalDocumentDraft>();
        foreach (string path in Directory.EnumerateFiles(RootPath, "*.json", SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using FileStream stream = File.OpenRead(path);
            LocalDocumentDraft? document = await JsonSerializer.DeserializeAsync<LocalDocumentDraft>(
                stream,
                SerializerOptions,
                cancellationToken);
            if (document is not null)
            {
                documents.Add(document);
            }
        }

        return documents
            .OrderByDescending(document => document.UpdatedAtUtc)
            .ToArray();
    }

    public async Task SaveAsync(
        LocalDocumentDraft document,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        Directory.CreateDirectory(RootPath);

        string destination = GetDocumentPath(document.DocumentId);
        string temporary = destination + ".tmp";

        await _gate.WaitAsync(cancellationToken);
        try
        {
            await using (FileStream stream = File.Create(temporary))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    document,
                    SerializerOptions,
                    cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            File.Move(temporary, destination, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }

            _gate.Release();
        }
    }

    public async Task DeleteAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            string path = GetDocumentPath(documentId);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private string GetDocumentPath(Guid documentId) =>
        Path.Combine(RootPath, $"{documentId:N}.json");
}
