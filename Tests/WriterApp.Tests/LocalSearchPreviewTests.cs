using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WriterApp.Device.Shared.Components;
using WriterApp.Device.Shared.Services;
using WriterApp.Device.Shared.Storage;
using WriterApp.UI.Shared;
using Xunit;

namespace WriterApp.Tests;

public sealed class LocalSearchPreviewTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "WriterApp.SearchPreviewTests", Guid.NewGuid().ToString("N"));
    private FileLocalDocumentStore Store => new(_root);
    private LocalDocumentRepository Repo => new(Store);
    private LocalDocumentSearch Search => new(Store);

    [Theory]
    [InlineData("Räksmörgås CAFÉ", "café", 11, 4)]
    [InlineData("café 日本語", "CAFÉ", 0, 5)]
    [InlineData("😀 café", "CAFÉ", 3, 4)]
    [InlineData("日本語です", "日本語", 0, 3)]
    [InlineData("Åland", "aland", -1, 0)]
    [InlineData("Straße", "STRASSE", -1, 0)]
    public void UnicodeRulePreservesOriginalOffsetsAndAccents(string text, string query, int start, int length)
    {
        var match = TextSearch.Find(text, query, 1).FirstOrDefault();
        if (start < 0) Assert.Null(match); else Assert.Equal(new TextSearchMatch(start, length), match);
    }

    [Fact]
    public async Task SearchReflectsWritingRenameMovesDeletionRestoreAndRestartWithoutNetwork()
    {
        var doc = await Repo.CreateAsync("Café notebook");
        Guid section = doc.Sections[0].SectionId, page = doc.Sections[0].Pages[0].PageId;
        var session = new LocalEditorSession(Repo, doc);
        session.Edit(page, "<p>Räksmörgås <strong>ca</strong>fé 日本語</p>"); await session.SaveAsync(); doc = session.Document;
        var results = await Search.SearchAsync("CAFÉ");
        Assert.Equal(2, results.Items.Count);
        var writing = results.Items.Single(r => !r.TitleMatch);
        Assert.Equal(section, writing.SectionId); Assert.Equal(page, writing.PageId);
        doc = await Repo.ChangeStructureAsync(doc, new(LocalStructureAction.CreatePage, TargetSectionId: section, Title: "Keep"));
        doc = await Repo.ChangeStructureAsync(doc, new(LocalStructureAction.CreateSection, Title: "Destination"));
        Guid destination = doc.Sections[1].SectionId;
        doc = await Repo.ChangeStructureAsync(doc, new(LocalStructureAction.MovePage, page, destination));
        doc = await Repo.ChangeStructureAsync(doc, new(LocalStructureAction.RenamePage, page, Title: "Moved page"));
        var resolved = (await Search.ResolveAsync(writing, "café"))!;
        Assert.Equal(destination, resolved.SectionId); Assert.Equal("Moved page", resolved.PageTitle);
        doc = await Repo.ChangeStructureAsync(doc, new(LocalStructureAction.DeletePage, page));
        Assert.Null(await Search.ResolveAsync(writing, "café"));
        Assert.Single((await Search.SearchAsync("café")).Items); // Title, never trash writing.
        doc = (await Store.GetAsync(doc.DocumentId))!;
        doc = await Repo.ChangeStructureAsync(doc, new(LocalStructureAction.RestorePage, page, section));
        Assert.Equal(2, (await new LocalDocumentSearch(Store).SearchAsync("café")).Items.Count);
        doc = await Repo.RenameAsync(doc, "Renamed");
        Assert.Single((await Search.SearchAsync("café")).Items);
        doc = await Repo.MoveToTrashAsync(doc);
        Assert.Empty((await Search.SearchAsync("café")).Items); Assert.Null(await Search.ResolveAsync(writing, "café"));
        doc = await Repo.RestoreAsync(doc);
        Assert.Single((await Search.SearchAsync("café")).Items);
        doc = await Repo.SaveAsync(doc with { Sections = doc.Sections.Select(s => s with { Pages = s.Pages.Select(p => p.PageId == page ? p with { Content = "<p>Changed writing</p>" } : p).ToArray() }).ToArray() });
        Assert.Null(await Search.ResolveAsync(writing, "café"));
    }

    [Fact]
    public async Task LateQueryCannotReplaceNewerResultsEvenIfStorageIgnoresCancellation()
    {
        var store = new DelayedSearchStore();
        using var runner = new LocalSearchQuery(new(store));
        var old = runner.RunAsync("old", TimeSpan.Zero);
        await store.FirstStarted.Task;
        var current = await runner.RunAsync("new", TimeSpan.Zero);
        Assert.Equal("new document", Assert.Single(current!.Items).DocumentTitle);
        store.FirstRead.SetResult(new([LocalDocumentCodec.NewDocument(Guid.NewGuid(), "old document", DateTimeOffset.UtcNow)], [], false));
        Assert.Null(await old);
    }

    [Fact]
    public async Task DebounceCancellationAvoidsFlushAndFailedFlushPreventsSearch()
    {
        var store = new DelayedSearchStore(); using var runner = new LocalSearchQuery(new(store));
        int flushes = 0;
        var delayed = runner.RunAsync("old", TimeSpan.FromHours(1), () => { flushes++; return Task.FromResult(true); });
        runner.Cancel(); Assert.Null(await delayed); Assert.Equal(0, flushes); Assert.Equal(0, store.Reads);
        Assert.Null(await runner.RunAsync("new", TimeSpan.Zero, () => Task.FromResult(false)));
        Assert.Equal(0, store.Reads);
    }

    [Fact]
    public async Task SaveFailureKeepsWritingAndPreventsResultNavigationPreparation()
    {
        var doc = await Repo.CreateAsync("Before result");
        var failing = new FileLocalDocumentStore(_root, TimeProvider.System, new AtomicDocumentWriter(_ => throw new IOException("Disk full")));
        var session = new LocalEditorSession(new(failing), doc);
        var page = doc.Sections[0].Pages[0]; session.Edit(page.PageId, "<p>Unsaved 日本語</p>");
        await Assert.ThrowsAsync<IOException>(() => session.SaveAsync());
        Assert.True(session.IsDirty); Assert.Equal("<p>Unsaved 日本語</p>", session.ContentFor(page));
        Assert.Equal(doc.LocalRevision, (await Store.GetAsync(doc.DocumentId))!.LocalRevision);
    }

    [Fact]
    public async Task LimitsAndUnavailableFilesAreExplicitAndCanceledQueryStops()
    {
        var doc = await Repo.CreateAsync("limit");
        var sections = Enumerable.Range(0, 60).Select(i => doc.Sections[0] with { SectionId = Guid.NewGuid(), OrderIndex = i,
            Pages = [doc.Sections[0].Pages[0] with { PageId = Guid.NewGuid(), Content = "<p>needle</p>" }] }).ToArray();
        doc = await Repo.SaveAsync(doc with { Sections = sections });
        await File.WriteAllTextAsync(Path.Combine(_root, $"{Guid.NewGuid():N}.json"), "broken");
        var result = await Search.SearchAsync("needle");
        Assert.Equal(50, result.Items.Count); Assert.True(result.Limited); Assert.Equal(1, result.UnavailableDocuments);
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Search.SearchAsync("needle", canceled.Token));
        Assert.ThrowsAny<OperationCanceledException>(() => TextSearch.Find("writing", "writing", 1, canceled.Token));
        var huge = sections[0].Pages[0] with { Content = new string('x', 2_000_001) };
        var limited = LocalDocumentSearch.Query(new([doc with { Sections = [sections[0] with { Pages = [huge] }] }], [], false), "needle", default);
        Assert.True(limited.Limited); Assert.Empty(limited.Items);
    }

    [Theory]
    [InlineData(LocalContentFormat.Html, "<h6>Heading</h6><p>ca<strong>fé</strong> 日本語 <a href=\"https://example.com\">safe</a></p><ol start=\"7\"><li><p>List</p></li></ol>", "<h6>Heading</h6>")]
    [InlineData(LocalContentFormat.LegacyText, "<script>literal</script> 日本語", "&lt;script&gt;")]
    [InlineData(LocalContentFormat.LegacyJson, "{\"type\":\"doc\",\"content\":[{\"type\":\"heading\",\"attrs\":{\"level\":6},\"content\":[{\"type\":\"text\",\"text\":\"日本語\"}]}]}", "<h6>日本語</h6>")]
    public void SupportedPreviewRetainsFormattingWithSafeLinks(LocalContentFormat format, string source, string token)
    {
        var page = LocalDocumentCodec.NewDocument(Guid.NewGuid(), "Preview", DateTimeOffset.UtcNow, source, format).Sections[0].Pages[0];
        var rendered = LocalDocumentPreview.Render(page);
        Assert.Null(rendered.Warning); Assert.Contains(token, rendered.Html);
        Assert.DoesNotContain("<script>", rendered.Html);
        if (format == LocalContentFormat.Html) { Assert.Contains("noopener noreferrer", rendered.Html); Assert.Contains("start=\"7\"", rendered.Html); }
        Assert.Equal(source, page.Content);
    }

    [Theory]
    [InlineData("<table><tr><td>Original 日本語</td></tr></table><img src='javascript:bad()'>")]
    [InlineData("<p onclick='run()'>Original</p><script>private script</script><a href='javascript:run()'>link</a>")]
    [InlineData("<p style='color:red'>Original</p><iframe src='https://never-load.invalid'></iframe>")]
    public void UnsupportedPreviewIsInertAndExplainsTextOnlyRendering(string source)
    {
        var page = LocalDocumentCodec.NewDocument(Guid.NewGuid(), "Protected", DateTimeOffset.UtcNow, source).Sections[0].Pages[0];
        var rendered = LocalDocumentPreview.Render(page);
        Assert.NotNull(rendered.Warning); Assert.Contains("Original", rendered.Html);
        Assert.DoesNotContain("<img", rendered.Html); Assert.DoesNotContain("<script", rendered.Html);
        Assert.DoesNotContain("<iframe", rendered.Html); Assert.DoesNotContain("javascript:", rendered.Html);
        Assert.Equal(source, page.Content);
    }

    [Fact]
    public async Task OrderedFullPreviewFindAndRepeatedRenderingDoNotChangeRevisionOrFiles()
    {
        var doc = await Repo.CreateAsync("Preview 日本語");
        doc = await Repo.ChangeStructureAsync(doc, new(LocalStructureAction.CreatePage, TargetSectionId: doc.Sections[0].SectionId, Title: "Second"));
        doc = await Repo.ChangeStructureAsync(doc, new(LocalStructureAction.CreateSection, Title: "Last"));
        doc = await Repo.SaveAsync(doc with { Sections = doc.Sections.Select(s => s with { Pages = s.Pages.Select(p => p with { Content = "<p>ca<strong>fé</strong> 日本語</p>" }).ToArray() }).ToArray() });
        string path = Path.Combine(_root, $"{doc.DocumentId:N}.json"); byte[] before = await File.ReadAllBytesAsync(path);
        var preview = LocalDocumentPreview.Create((await Store.GetAsync(doc.DocumentId))!);
        Assert.Equal(doc.LocalRevision, preview.Revision); Assert.Equal(doc.Sections[0].SectionId, preview.Sections[0].SectionId);
        Assert.Equal(2, preview.Sections[0].Pages.Count); Assert.Equal(doc.Sections[0].Pages[1].PageId, preview.Sections[0].Pages[1].PageId);
        var found = LocalDocumentPreview.Highlight(preview.Sections[0].Pages[0].Html, "CAFÉ");
        Assert.Equal(1, found.Matches); Assert.Contains("<mark>ca</mark>", found.Html); Assert.Contains("<mark>fé</mark>", found.Html);
        Assert.Equal(before, await File.ReadAllBytesAsync(path)); Assert.Equal(doc.LocalRevision, (await Store.GetAsync(doc.DocumentId))!.LocalRevision);
        var trash = await Repo.MoveToTrashAsync(doc); Assert.Throws<InvalidOperationException>(() => LocalDocumentPreview.Create(trash));
    }

    [Fact]
    public async Task SharedPresentationEscapesSearchTextAndRendersPreviewOutlineForAllPages()
    {
        using var services = new ServiceCollection().AddLogging().AddSingleton<NavigationManager>(new PreviewNavigation()).BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        string highlight = await renderer.Dispatcher.InvokeAsync(async () => (await renderer.RenderComponentAsync<SearchHighlightText>(ParameterView.FromDictionary(
            new Dictionary<string, object?> { ["Text"] = "<script>CAFÉ</script>", ["Query"] = "café" }))).ToHtmlString());
        Assert.Contains("&lt;script&gt;", highlight); Assert.Contains("<mark>CAF", highlight); Assert.DoesNotContain("<script>", highlight);
        var doc = await Repo.CreateAsync("Preview"); var snapshot = LocalDocumentPreview.Create(doc);
        string html = await renderer.Dispatcher.InvokeAsync(async () => (await renderer.RenderComponentAsync<LocalDocumentPreviewPanel>(ParameterView.FromDictionary(
            new Dictionary<string, object?> { ["Snapshot"] = snapshot }))).ToHtmlString());
        Assert.Contains("Preview outline", html); Assert.Contains("Find in preview", html); Assert.Contains("Back to editing", html);
        Assert.Contains($"preview-page-{doc.Sections[0].Pages[0].PageId:N}", html); Assert.Contains("This page is empty", html);
        Assert.Contains("href=\"http://localhost/documents/test?page=page-id#preview-page-", html);
    }

    private sealed class PreviewNavigation : NavigationManager
    {
        public PreviewNavigation() => Initialize("http://localhost/", "http://localhost/documents/test?page=page-id");
        protected override void NavigateToCore(string uri, bool forceLoad) => throw new NotSupportedException();
    }

    private sealed class DelayedSearchStore : ILocalDocumentStore
    {
        public int Reads;
        public TaskCompletionSource FirstStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<LocalSearchDocuments> FirstRead { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<LocalSearchDocuments> ReadSearchDocumentsAsync(CancellationToken cancellationToken = default)
        {
            if (++Reads == 1) { FirstStarted.TrySetResult(); return FirstRead.Task; }
            return Task.FromResult(new LocalSearchDocuments([LocalDocumentCodec.NewDocument(Guid.NewGuid(), "new document", DateTimeOffset.UtcNow)], [], false));
        }
        public Task<LocalDocument> CreateAsync(string title, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<LocalDocument> CreateImportedAsync(string title, string html, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<LocalDocument?> GetAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<LocalDocumentList> ListAsync(LocalDocumentScope scope = LocalDocumentScope.Active, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<LocalDocument> SaveAsync(LocalDocument doc, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<LocalDocument> RenameAsync(Guid id, long revision, string title, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<LocalDocument> DuplicateAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<LocalDocument> MoveToTrashAsync(Guid id, long revision, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<LocalDocument> RestoreAsync(Guid id, long revision, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task PermanentlyDeleteAsync(Guid id, long revision, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
