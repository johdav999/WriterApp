using WriterApp.Device.Shared.Storage;
using Xunit;

namespace WriterApp.Tests;

public sealed class FileLocalDocumentStoreTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(
        Path.GetTempPath(),
        "WriterApp.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task SaveListGetAndDeleteRoundTrip()
    {
        var store = new FileLocalDocumentStore(_rootPath);
        var older = new LocalDocumentDraft(
            Guid.NewGuid(),
            "First draft",
            "{\"type\":\"doc\"}",
            DateTimeOffset.Parse("2026-09-26T10:00:00Z"),
            "server-v1");
        var newer = new LocalDocumentDraft(
            Guid.NewGuid(),
            "Second draft",
            "{\"type\":\"doc\",\"content\":[]}",
            DateTimeOffset.Parse("2026-09-27T10:00:00Z"));

        await store.SaveAsync(older);
        await store.SaveAsync(newer);

        IReadOnlyList<LocalDocumentDraft> documents = await store.ListAsync();
        LocalDocumentDraft? loaded = await store.GetAsync(older.DocumentId);

        Assert.Equal([newer.DocumentId, older.DocumentId], documents.Select(document => document.DocumentId));
        Assert.Equal(older, loaded);
        Assert.DoesNotContain(Directory.EnumerateFiles(_rootPath), path => path.EndsWith(".tmp", StringComparison.Ordinal));

        await store.DeleteAsync(older.DocumentId);

        Assert.Null(await store.GetAsync(older.DocumentId));
        Assert.Single(await store.ListAsync());
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }
}
