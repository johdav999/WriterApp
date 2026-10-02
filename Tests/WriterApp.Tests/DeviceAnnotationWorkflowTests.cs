using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using WriterApp.Device.Shared.Components;
using WriterApp.Device.Shared.Pages;
using WriterApp.Device.Shared.Services;
using WriterApp.Device.Shared.Storage;
using Xunit;

namespace WriterApp.Tests;

public sealed class DeviceAnnotationWorkflowTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "WriterApp.AnnotationWorkflow", Guid.NewGuid().ToString("N"));

    private ServiceProvider Services()
    {
        var services = new ServiceCollection().AddLogging()
            .AddSingleton<IJSRuntime, NoJs>().AddSingleton<NavigationManager, Navigation>();
        services.AddWriterAppDeviceCore(new("Test", new("https://test.invalid/")), _root);
        return services.BuildServiceProvider();
    }

    private static LocalDocument Book(LocalDocument document) => LocalProjectStructure.Attach(document, "Book");

    [Fact]
    public async Task SavingThroughPanelDelegateRefreshesTheWorkspaceImmediately()
    {
        using var services = Services();
        var repository = services.GetRequiredService<LocalDocumentRepository>();
        var document = await repository.SaveAsync(Book(await repository.CreateImportedAsync("Draft", "<p>A unique passage.</p>")));
        var scene = document.Project!.Nodes.Single(n => n.NodeType == "scene");
        WorkspaceHarness? workspace = null;
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<WorkspaceHarness>(ParameterView.FromDictionary(new Dictionary<string, object?>
            { ["Saved"] = document, ["Ready"] = (Action<WorkspaceHarness>)(value => workspace = value) }));
            Assert.DoesNotContain("Review wording", output.ToHtmlString());
            Assert.Empty(workspace!.TextEditor.Annotations);

            var saved = await workspace!.CommitAsync(d => LocalPlanning.AddAnnotation(d, scene.NodeId, "comment", "Review wording", "unique passage"));
            Assert.NotNull(saved);
            // The panel calls a Func delegate, so Blazor does not rerender the parent for it.
            // The actual workspace must propagate the committed annotation without another click.
            Assert.Contains("Review wording", output.ToHtmlString());
            Assert.Contains("annotation-text-link", output.ToHtmlString());
            var markup = Assert.Single(workspace.TextEditor.Annotations);
            await workspace.NavigateAsync(markup.Id);
            Assert.Equal(markup.Id, workspace.TextEditor.RequestedAnnotationId);
            Assert.NotNull(workspace.TextEditor.AnnotationRequest);

            await workspace.CommitAsync(d => LocalPlanning.Resolve(d, scene.NodeId, markup.Id, true));
            Assert.Equal("resolved", Assert.Single(workspace.TextEditor.Annotations).Status);
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CommentAutomaticallyUsesSelectionAndPreservesItDuringTyping(bool captureOnFocus)
    {
        using var services = Services();
        var repository = services.GetRequiredService<LocalDocumentRepository>();
        var document = Book(await repository.CreateImportedAsync("Draft", "<p>A unique passage and another passage.</p>"));
        var pageId = document.Sections[0].Pages[0].PageId;
        PanelHarness? panel = null;
        string selection = "unique passage";
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            await renderer.RenderComponentAsync<PanelHarness>(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                ["Document"] = document, ["SectionId"] = document.Sections[0].SectionId, ["Subview"] = "annotations",
                ["Ready"] = (Action<PanelHarness>)(value => panel = value),
                ["GetSelectedText"] = (Func<Task<string>>)(() => Task.FromResult(selection)),
                ["Commit"] = (Func<Func<LocalDocument, LocalDocument>, Task<LocalDocument?>>)(mutation =>
                { document = mutation(document); return Task.FromResult<LocalDocument?>(document); })
            }));
            if (captureOnFocus) { await panel!.CaptureAsync(); selection = "another passage"; }
            panel!.Write("Review this <script> passage");
            await panel.AddAsync();
        });
        var annotation = Assert.Single(document.Project!.Nodes.Single(n => n.NodeType == "scene").Annotations);
        Assert.Equal("unique passage", annotation.Value.AnchorText);
        Assert.Equal(pageId, LocalAnnotationMarkup.PageFor(document, annotation.LocalId));
        Assert.Single(LocalAnnotationMarkup.ForPage(document, pageId));
        Assert.Equal("<p>A unique passage and another passage.</p>", document.Sections[0].Pages[0].Content);
    }

    public sealed class PanelHarness : LocalPlanningPanel
    {
        [Parameter] public Action<PanelHarness> Ready { get; set; } = default!;
        protected override void OnInitialized() => Ready(this);
        public Task CaptureAsync() => Invoke("CaptureSelectedTextAsync");
        public Task AddAsync() => Invoke("AddAnnotationAsync");
        public void Write(string content) => typeof(LocalPlanningPanel).GetField("_annotationText", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(this, content);
        private Task Invoke(string method) => (Task)typeof(LocalPlanningPanel).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(this, null)!;
    }

    public sealed class WorkspaceHarness : DocumentWorkspace
    {
        [Parameter] public LocalDocument Saved { get; set; } = default!;
        [Parameter] public Action<WorkspaceHarness> Ready { get; set; } = default!;
        [Inject] public LocalDocumentRepository Repository { get; set; } = default!;
        [Inject] public LocalRecoveryStore Recovery { get; set; } = default!;
        protected override void OnInitialized() => Ready(this);
        protected override async Task OnParametersSetAsync()
        {
            DocumentId = Saved.DocumentId;
            var session = new LocalEditorSession(Repository, Saved);
            Set("_session", session);
            Set("_selectedPage", Saved.Sections[0].Pages[0].PageId);
            Set("_autosave", new LocalAutosaveCoordinator(session, Recovery, () => Task.CompletedTask));
            var subviews = (Dictionary<DeviceEditorPanel, string>)typeof(DocumentWorkspace).GetField("_subviews", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(this)!;
            subviews[DeviceEditorPanel.Notes] = "annotations";
            await View.SelectPanelAsync(DeviceEditorPanel.Notes);
        }
        public Task<LocalDocument?> CommitAsync(Func<LocalDocument, LocalDocument> mutation) =>
            (Task<LocalDocument?>)typeof(DocumentWorkspace).GetMethod("CommitPanelMutationAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(this, [mutation])!;
        public DeviceTextEditor TextEditor => (DeviceTextEditor)typeof(DocumentWorkspace).GetField("_editor", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(this)!;
        public Task NavigateAsync(Guid id) => EventCallback.Factory.Create<Guid>(this, value =>
            (Task)typeof(DocumentWorkspace).GetMethod("NavigateToAnnotationAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(this, [value])!).InvokeAsync(id);
        private void Set(string name, object value) => typeof(DocumentWorkspace).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(this, value);
    }

    private sealed class Navigation : NavigationManager
    {
        public Navigation() => Initialize("http://localhost/", "http://localhost/");
        protected override void NavigateToCore(string uri, bool forceLoad) { }
    }
    private sealed class NoJs : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => throw new InvalidOperationException("Static rendering must not invoke JS.");
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken ct, object?[]? args) => InvokeAsync<TValue>(identifier, args);
    }
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
