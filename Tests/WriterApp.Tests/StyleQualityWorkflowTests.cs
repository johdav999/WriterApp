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

public sealed class StyleQualityWorkflowTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task QualityPanelShowsRevisionActionAndExplicitApplyAfterPreview(bool hasProposal)
    {
        string root = Path.Combine(Path.GetTempPath(), "WriterApp.StyleQuality", Guid.NewGuid().ToString("N"));
        try
        {
            var registrations = new ServiceCollection().AddLogging()
                .AddSingleton<IJSRuntime, NoJs>().AddSingleton<NavigationManager, Navigation>();
            registrations.AddWriterAppDeviceCore(new("Test", new("https://test.invalid/")), root);
            using var services = registrations.BuildServiceProvider();
            var repository = services.GetRequiredService<LocalDocumentRepository>();
            var doc = await repository.CreateImportedAsync("Draft", "<p>Original page</p>");
            doc = doc with { ServerDocumentId = Guid.NewGuid(), ServerVersion = "v1", SyncState = LocalSyncState.Synced };
            var page = doc.Sections[0].Pages[0];
            var capture = new AiEditorSnapshot(page.Content, "Original page", "Original page", 0, 13, 0, 15, 0);
            var prepared = DeviceAiRequests.Build(doc, doc.Sections[0], page, capture, DeviceAiAction.StyleQuality);
            var preview = hasProposal ? new DeviceAiProposal(prepared, "Clearer writing <script>", null, "Original page", new()) : null;
            await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
            string html = await renderer.Dispatcher.InvokeAsync(async () =>
            {
                var output = await renderer.RenderComponentAsync<QualityWorkspace>(ParameterView.FromDictionary(new Dictionary<string, object?>
                { [nameof(QualityWorkspace.Saved)] = doc, [nameof(QualityWorkspace.Preview)] = preview }));
                return output.ToHtmlString();
            });
            Assert.Contains("Suggest style & quality revision", html);
            if (hasProposal)
            {
                Assert.Contains("Apply style revision", html);
                Assert.Contains("Original page", html);
                Assert.Contains("Clearer writing &lt;script&gt;", html);
                Assert.Contains("Apply replaces this page", html);
                Assert.Contains("Dismiss", html);
            }
            else Assert.DoesNotContain("Apply style revision", html);
            Assert.DoesNotContain("Analysis only. No manuscript changes", html);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    // Seed review states while rendering the actual workspace and all its child components.
    public sealed class QualityWorkspace : DocumentWorkspace
    {
        [Parameter] public LocalDocument Saved { get; set; } = default!;
        [Parameter] public DeviceAiProposal? Preview { get; set; }
        [Inject] public LocalDocumentRepository Repository { get; set; } = default!;
        protected override void OnInitialized() { }
        protected override Task OnParametersSetAsync()
        {
            DocumentId = Saved.DocumentId;
            Set("_session", new LocalEditorSession(Repository, Saved));
            Set("_selectedPage", Saved.Sections[0].Pages[0].PageId);
            Set("_aiProposal", Preview);
            var subviews = (Dictionary<DeviceEditorPanel, string>)typeof(DocumentWorkspace)
                .GetField("_subviews", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(this)!;
            subviews[DeviceEditorPanel.Writing] = "quality";
            return Task.CompletedTask;
        }
        private void Set(string field, object? value) => typeof(DocumentWorkspace)
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(this, value);
    }
    private sealed class Navigation : NavigationManager
    {
        public Navigation() => Initialize("http://localhost/", "http://localhost/");
        protected override void NavigateToCore(string uri, bool forceLoad) { }
    }
    private sealed class NoJs : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => throw new InvalidOperationException("Static render must not invoke JS.");
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken ct, object?[]? args) => InvokeAsync<TValue>(identifier, args);
    }
}
