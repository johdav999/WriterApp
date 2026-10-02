using AngleSharp.Html.Parser;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using WriterApp.Device.Shared.Services;
using WriterApp.Device.Shared.Storage;
using Xunit;

namespace WriterApp.Tests;

public sealed class CombinedLibraryTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "WriterApp.CombinedLibrary", Guid.NewGuid().ToString("N"));
    private LocalDocumentLibrary NewLibrary() => new(new LocalDocumentRepository(new FileLocalDocumentStore(_root)));
    private static LocalDocument Standalone(string title) => LocalDocumentCodec.NewDocument(Guid.NewGuid(), title, DateTimeOffset.UtcNow);
    private static LocalDocument Project(string title, string manuscript) => LocalProjectStructure.Attach(Standalone(manuscript), title);

    [Fact]
    public void AllViewKeepsStandaloneAtRootAndCanonicalManuscriptUnderItsProject()
    {
        var project = Project("The coast", "Novel");
        var standalone = Standalone("Morning pages");
        var rows = LocalLibraryView.Rows([project, standalone], LocalLibraryFilter.All, "", "title", new HashSet<Guid> { project.Project!.ProjectId });
        Assert.Equal(3, rows.Count);
        var folder = Assert.Single(rows, r => r.IsProject);
        var child = Assert.Single(rows, r => r.IsChild);
        Assert.Equal(folder.Document.DocumentId, child.Document.DocumentId);
        Assert.NotEqual(folder.Key, child.Key);
        Assert.Equal("The coast", folder.Title);
        Assert.Equal("In The coast", child.Context);
        Assert.Equal("Novel", child.Title);
        Assert.False(rows.Single(r => r.Document.DocumentId == standalone.DocumentId).IsChild);
        Assert.Equal("Standalone document", rows.Single(r => r.Document.DocumentId == standalone.DocumentId).Context);
        Assert.Equal($"/documents/{project.DocumentId:D}", folder.OpenUrl);
        Assert.Equal($"/projects/{project.Project.ProjectId:D}?documentId={project.DocumentId:D}", folder.ProjectDetailsUrl);
        Assert.Null(rows.Single(r => r.Document.DocumentId == standalone.DocumentId).ProjectDetailsUrl);
    }

    [Fact]
    public void FiltersExposeEveryDocumentOnceWithProjectContextAndKeepProjectsSeparate()
    {
        var project = Project("The coast", "Novel");
        var standalone = Standalone("Morning pages");
        var documents = LocalLibraryView.Rows([project, standalone], LocalLibraryFilter.Documents, "", "title", new HashSet<Guid>());
        Assert.Equal(2, documents.Count);
        Assert.All(documents, r => { Assert.False(r.IsProject); Assert.False(r.IsChild); });
        Assert.Equal(2, documents.Select(r => r.Document.DocumentId).Distinct().Count());
        Assert.Equal("In The coast", documents.Single(r => r.Document.Project is not null).Context);
        var projects = LocalLibraryView.Rows([project, standalone], LocalLibraryFilter.Projects, "", "title", new HashSet<Guid> { project.Project!.ProjectId });
        Assert.True(Assert.Single(projects).IsProject);
    }

    [Theory]
    [InlineData("Novel")]
    [InlineData("coast")]
    public void SearchFindsProjectOrManuscriptAndCanCollapseMatchingProject(string query)
    {
        var project = Project("The coast", "Novel");
        var documents = new[] { project, Standalone("Unrelated") };
        var rows = LocalLibraryView.Rows(documents, LocalLibraryFilter.All, query, "title", new HashSet<Guid>());
        Assert.Equal(2, rows.Count);
        Assert.True(rows[1].IsChild);
        Assert.Equal(project.DocumentId, rows[1].Document.DocumentId);
        Assert.Single(LocalLibraryView.Rows(documents, LocalLibraryFilter.All, query, "title", new HashSet<Guid>(), new HashSet<Guid> { project.Project!.ProjectId }));
    }

    [Fact]
    public void SortUsesDisplayedProjectNameAndKeepsItsChildAdjacent()
    {
        var project = Project("A project", "Z manuscript");
        var standalone = Standalone("B standalone");
        var rows = LocalLibraryView.Rows([standalone, project], LocalLibraryFilter.All, "", "title", new HashSet<Guid> { project.Project!.ProjectId });
        Assert.Equal(new[] { "A project", "Z manuscript", "B standalone" }, rows.Select(r => r.Title));
    }

    [Fact]
    public void TrashExcludesActiveItemsAndDoesNotExposeEditableChildRows()
    {
        var deleted = Project("Deleted", "Novel") with { DeletedAtUtc = DateTimeOffset.UtcNow };
        var active = Standalone("Active");
        var rows = LocalLibraryView.Rows([deleted, active], LocalLibraryFilter.Trash, "", "recent", new HashSet<Guid> { deleted.Project!.ProjectId });
        Assert.True(Assert.Single(rows).IsProject);
        Assert.False(rows[0].IsChild);
        Assert.Equal(active.DocumentId, Assert.Single(LocalLibraryView.Rows([deleted, active], LocalLibraryFilter.All, "", "recent", new HashSet<Guid>())).Document.DocumentId);
    }

    [Fact]
    public async Task ProjectCreationAndRenamePersistWithoutChangingManuscriptAndRejectStaleRename()
    {
        var library = NewLibrary();
        var standalone = await library.CreateAsync("Notes");
        var project = await library.CreateProjectAsync("Book");
        var renamed = await library.RenameProjectAsync(project, "New book title");
        Assert.Equal(project.DocumentId, renamed.DocumentId);
        Assert.Equal(project.Project!.ProjectId, renamed.Project!.ProjectId);
        Assert.Equal(project.Title, renamed.Title);
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(project.Sections), System.Text.Json.JsonSerializer.Serialize(renamed.Sections));
        await Assert.ThrowsAsync<LocalDocumentConflictException>(() => library.RenameProjectAsync(project, "Stale"));
        var restarted = NewLibrary();
        await restarted.RefreshAsync(LocalDocumentScope.Active);
        Assert.Equal("New book title", restarted.Documents.Single(d => d.Project is not null).Project!.Title);
        Assert.Null(restarted.Documents.Single(d => d.DocumentId == standalone.DocumentId).Project);
        await restarted.MoveToTrashAsync(renamed);
        await restarted.RefreshAsync(LocalDocumentScope.Trash);
        Assert.Single(restarted.Documents);
        Assert.Equal(standalone.DocumentId, Assert.Single(restarted.ActiveDocuments).DocumentId);
        await restarted.RestoreAsync(restarted.Documents[0]);
        Assert.Empty(restarted.Documents);
        Assert.Equal(2, restarted.ActiveDocuments.Count);
    }

    [Fact]
    public async Task HomeRendersBothCreationActionsAndDistinctProjectAndStandaloneRows()
    {
        using var services = new ServiceCollection().AddLogging()
            .AddWriterAppDeviceCore(new("Test", new Uri("https://test.invalid/")), _root)
            .AddSingleton<NavigationManager>(new TestNavigation())
            .AddSingleton<IJSRuntime>(new NoJs()).BuildServiceProvider();
        var repository = services.GetRequiredService<LocalDocumentRepository>();
        var project = await repository.CreateProjectAsync("Book");
        await repository.CreateAsync("Notes");
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        string html = await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<WriterApp.Device.Shared.Pages.Home>()).ToHtmlString());
        var page = new HtmlParser().ParseDocument(html);
        Assert.Equal("Projects & documents", page.QuerySelector("h1")!.TextContent);
        Assert.Equal("New project", page.QuerySelector(".new-project-button")!.TextContent);
        Assert.Equal("New document", page.QuerySelector(".new-document-button")!.TextContent);
        Assert.Equal(4, page.QuerySelectorAll(".library-tab").Length);
        Assert.Equal(2, page.QuerySelectorAll(".library-row").Length);
        Assert.Equal("Project", page.QuerySelector(".is-project .library-type")!.TextContent);
        Assert.Equal("false", page.QuerySelector(".is-project .project-expand")!.GetAttribute("aria-expanded"));
        Assert.Equal($"/documents/{project.DocumentId:D}", page.QuerySelector(".is-project .library-open")!.GetAttribute("href"));
        Assert.Contains("Standalone document", page.QuerySelectorAll(".library-row").Single(r => !r.ClassList.Contains("is-project")).TextContent);
    }

    // Test-only renderer access is confined here; production components use public Blazor APIs.
#pragma warning disable BL0006
    [Theory]
    [InlineData("none", false)]
    [InlineData("project", true)]
    [InlineData("standalone", false)]
    [InlineData("child", false)]
    public async Task NewDocumentUsesOnlyTheCurrentlySelectedProjectRow(string selection, bool inProject)
    {
        using var services = new ServiceCollection().AddLogging()
            .AddWriterAppDeviceCore(new("Test", new Uri("https://test.invalid/")), _root)
            .AddSingleton<NavigationManager>(new TestNavigation())
            .AddSingleton<IJSRuntime>(new NoJs()).BuildServiceProvider();
        var repository = services.GetRequiredService<LocalDocumentRepository>();
        var project = await repository.CreateProjectAsync("Book");
        await repository.RenameAsync(project, "Manuscript");
        var notes = await repository.CreateAsync("Notes");
        using var renderer = new LibraryRenderer(services, services.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            await renderer.MountAsync();
            if (selection != "none")
                await renderer.ClickAsync(button => HasClass(button, "library-select") && HasText(button, "Book"));
            if (selection == "standalone")
                await renderer.ClickAsync(button => HasClass(button, "library-select") && HasText(button, "Notes"));
            if (selection == "child")
            {
                await renderer.ClickAsync(button => HasAttribute(button, "aria-label", "Expand Book"));
                await renderer.ClickAsync(button => HasClass(button, "library-select") && HasText(button, "Manuscript"));
            }
            await renderer.ClickAsync(button => HasClass(button, "new-document-button"));
        });
        var restarted = NewLibrary();
        await restarted.RefreshAsync(LocalDocumentScope.Active);
        var created = Assert.Single(restarted.Documents, d => d.DocumentId != project.DocumentId && d.DocumentId != notes.DocumentId);
        Assert.Equal("Untitled", created.Title);
        Assert.Equal(inProject ? project.Project!.ProjectId : (Guid?)null, created.Project?.ProjectId);
        if (inProject) Assert.Equal(project.DocumentId, created.Project!.PrimaryDocumentId);
    }

    [Fact]
    public async Task LibraryDropMovesTheOriginalDocumentAndExpandsItsProject()
    {
        using var services = new ServiceCollection().AddLogging()
            .AddWriterAppDeviceCore(new("Test", new Uri("https://test.invalid/")), _root)
            .AddSingleton<NavigationManager>(new TestNavigation())
            .AddSingleton<IJSRuntime>(new NoJs()).BuildServiceProvider();
        var repository = services.GetRequiredService<LocalDocumentRepository>();
        var project = await repository.CreateProjectAsync("Book");
        var notes = await repository.CreateImportedAsync("Notes", "<p>Keep my writing</p>");
        using var renderer = new LibraryRenderer(services, services.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            await renderer.MountAsync();
            await renderer.Home.MoveDocumentToProject(notes.DocumentId.ToString(), project.Project!.ProjectId.ToString());
            Assert.Equal(2, renderer.Elements("article").Count(row => HasClass(row, "is-child")));
            Assert.DoesNotContain(renderer.Elements("article"), row => HasAttribute(row, "data-standalone-document-id", notes.DocumentId.ToString("D")));
        });
        var restarted = NewLibrary();
        await restarted.RefreshAsync(LocalDocumentScope.Active);
        Assert.Equal(2, restarted.Documents.Count);
        var moved = restarted.Documents.Single(d => d.DocumentId == notes.DocumentId);
        Assert.Equal(project.Project!.ProjectId, moved.Project!.ProjectId);
        Assert.Equal(project.DocumentId, moved.Project.PrimaryDocumentId);
        Assert.Equal(notes.Sections[0].Pages[0], moved.Sections[0].Pages[0]);
    }

    [Fact]
    public async Task RenderedEventsExpandProjectFilterDocumentsAndOpenProjectDetailsMenu()
    {
        using var services = new ServiceCollection().AddLogging()
            .AddWriterAppDeviceCore(new("Test", new Uri("https://test.invalid/")), _root)
            .AddSingleton<NavigationManager>(new TestNavigation())
            .AddSingleton<IJSRuntime>(new NoJs()).BuildServiceProvider();
        var repository = services.GetRequiredService<LocalDocumentRepository>();
        var manuscript = await repository.CreateProjectAsync("Book");
        await repository.CreateAsync("Notes");
        using var renderer = new LibraryRenderer(services, services.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            await renderer.MountAsync();
            await renderer.ClickAsync(button => HasAttribute(button, "aria-label", "Expand Book"));
            Assert.Single(renderer.Elements("article"), row => HasClass(row, "is-child"));
            await renderer.ClickAsync(button => HasAttribute(button, "aria-label", "Actions for project Book"));
            Assert.Single(renderer.Elements("a"), link => HasAttribute(link, "href", $"/projects/{manuscript.Project!.ProjectId:D}?documentId={manuscript.DocumentId:D}")
                && link.Any(f => f.FrameType == RenderTreeFrameType.Text && f.TextContent == "Project details"));
            await renderer.ClickAsync(button => HasClass(button, "library-tab") && button.Any(f => f.FrameType == RenderTreeFrameType.Text && f.TextContent == "Documents"));
            Assert.Equal(2, renderer.Elements("article").Count);
            Assert.DoesNotContain(renderer.Elements("article"), row => HasClass(row, "is-project") || HasClass(row, "is-child"));
            await renderer.ClickAsync(button => HasClass(button, "library-tab") && button.Any(f => f.FrameType == RenderTreeFrameType.Text && f.TextContent == "All"));
            Assert.Single(renderer.Elements("article"), row => HasClass(row, "is-child"));
            await renderer.ClickAsync(button => HasAttribute(button, "aria-label", "Collapse Book"));
            Assert.Equal(2, renderer.Elements("article").Count);
            Assert.DoesNotContain(renderer.Elements("article"), row => HasClass(row, "is-child"));
        });
    }

    private static bool HasAttribute(RenderTreeFrame[] frames, string name, string value) =>
        frames.Any(f => f.FrameType == RenderTreeFrameType.Attribute && f.AttributeName == name && f.AttributeValue?.ToString() == value);
    private static bool HasClass(RenderTreeFrame[] frames, string name) =>
        frames.Any(f => f.FrameType == RenderTreeFrameType.Attribute && f.AttributeName == "class" && f.AttributeValue?.ToString()?.Split(' ').Contains(name) == true);
    private static bool HasText(RenderTreeFrame[] frames, string text) =>
        frames.Any(f => f.FrameType == RenderTreeFrameType.Text && f.TextContent == text);

    // Dispatch the callbacks attached to rendered controls so this checks the real page's state transitions.
    private sealed class LibraryRenderer : Renderer
    {
        public LibraryRenderer(IServiceProvider services, ILoggerFactory logger) : base(services, logger)
        {
            ElementReferenceContext = new WebElementReferenceContext(services.GetRequiredService<IJSRuntime>());
        }
        private int _rootId;
        public WriterApp.Device.Shared.Pages.Home Home { get; private set; } = null!;
        public override Dispatcher Dispatcher { get; } = Dispatcher.CreateDefault();
        protected override void HandleException(Exception exception) => throw exception;
        protected override Task UpdateDisplayAsync(in RenderBatch renderBatch) => Task.CompletedTask;
        public Task MountAsync()
        {
            Home = (WriterApp.Device.Shared.Pages.Home)InstantiateComponent(typeof(WriterApp.Device.Shared.Pages.Home));
            _rootId = AssignRootComponentId(Home);
            return RenderRootComponentAsync(_rootId);
        }
        public List<RenderTreeFrame[]> Elements(string tag)
        {
            var frames = GetCurrentRenderTreeFrames(_rootId);
            List<RenderTreeFrame[]> elements = [];
            for (int i = 0; i < frames.Count; i++)
                if (frames.Array[i].FrameType == RenderTreeFrameType.Element && frames.Array[i].ElementName == tag)
                    elements.Add(frames.Array.Skip(i).Take(frames.Array[i].ElementSubtreeLength).ToArray());
            return elements;
        }
        public Task ClickAsync(Func<RenderTreeFrame[], bool> predicate)
        {
            var button = Assert.Single(Elements("button"), frames => predicate(frames));
            var handler = Assert.Single(button, f => f.FrameType == RenderTreeFrameType.Attribute && f.AttributeName == "onclick");
            return DispatchEventAsync(handler.AttributeEventHandlerId, null, new MouseEventArgs());
        }
    }
#pragma warning restore BL0006

    private sealed class TestNavigation : NavigationManager
    {
        public TestNavigation() => Initialize("http://localhost/", "http://localhost/");
        protected override void NavigateToCore(string uri, bool forceLoad) { }
    }
    private sealed class NoJs : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => ValueTask.FromResult(default(TValue)!);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => InvokeAsync<TValue>(identifier, args);
    }
    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
