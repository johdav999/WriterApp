using AngleSharp.Html.Parser;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using WriterApp.Device.Shared.Pages;
using WriterApp.Device.Shared.Services;
using Xunit;

namespace WriterApp.Tests;

public sealed class DeviceStoryTabRegressionTests
{
    [Theory]
    [InlineData(false, null, null)]
    [InlineData(true, null, null)]
    [InlineData(false, "Story", "scene")]
    [InlineData(true, "Story", "scene")]
    [InlineData(false, "Story", "storyboard")]
    [InlineData(true, "Story", "storyboard")]
    [InlineData(false, "Synopsis", null)]
    [InlineData(true, "Synopsis", null)]
    [InlineData(false, "Notes", "notes")]
    [InlineData(true, "Notes", "notes")]
    public async Task PlanningTabsStayVisibleAndDeepLinksWorkWithOrWithoutProject(bool hasProject, string? panel, string? subview)
    {
        string root = Path.Combine(Path.GetTempPath(), "WriterApp.StoryTabs", Guid.NewGuid().ToString("N"));
        try
        {
            var registrations = new ServiceCollection().AddLogging()
                .AddSingleton<IJSRuntime, NoJs>().AddSingleton<NavigationManager, Navigation>();
            registrations.AddWriterAppDeviceCore(new("Test", new("https://test.invalid/")), root);
            using var services = registrations.BuildServiceProvider();
            var repository = services.GetRequiredService<LocalDocumentRepository>();
            var document = await repository.CreateImportedAsync("Draft", "<p>Keep this writing.</p>");
            if (hasProject) document = await repository.AttachProjectAsync(document, "Book");

            await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
            string html = await renderer.Dispatcher.InvokeAsync(async () =>
            {
                var output = await renderer.RenderComponentAsync<WorkspaceRoute>(ParameterView.FromDictionary(new Dictionary<string, object?>
                {
                    [nameof(DocumentWorkspace.DocumentId)] = document.DocumentId,
                    [nameof(WorkspaceRoute.RoutePanel)] = panel,
                    [nameof(WorkspaceRoute.RouteSubview)] = subview
                }));
                return output.ToHtmlString();
            });
            var dom = new HtmlParser().ParseDocument(html);
            Assert.NotNull(dom.QuerySelector("#context-category-story"));
            Assert.NotNull(dom.QuerySelector("#context-category-notes"));
            string selectedCategory = panel == "Notes" ? "notes" : panel is "Story" or "Synopsis" ? "story" : "writing";
            Assert.Equal("true", dom.QuerySelector("#context-category-" + selectedCategory)!.GetAttribute("aria-selected"));
            if (panel is not null)
            {
                Assert.False(dom.QuerySelector(".planning-fields")!.HasAttribute("hidden"));
                string selectedSubview = panel == "Synopsis" ? "synopsis" : subview!;
                Assert.Equal("true", dom.QuerySelector("#context-subview-" + selectedSubview)!.GetAttribute("aria-selected"));
                if (!hasProject)
                {
                    Assert.Contains("This document is not part of a project yet.", dom.QuerySelector(".local-planning-panel")!.TextContent);
                    Assert.Equal("/projects", dom.QuerySelector(".local-planning-panel a")!.GetAttribute("href"));
                    Assert.True(dom.QuerySelector(".desktop-context-ai")!.HasAttribute("hidden"));
                }
                else Assert.Contains("Save planning", dom.QuerySelector(".local-planning-panel")!.TextContent);
            }
            var saved = await repository.LoadAsync(document.DocumentId);
            Assert.Equal(document.LocalRevision, saved!.LocalRevision);
            Assert.Equal("<p>Keep this writing.</p>", saved.Sections[0].Pages[0].Content);
            Assert.Equal(hasProject, saved.Project is not null);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    // HtmlRenderer has no router to supply query values. Supply them before the real lifecycle runs.
    public sealed class WorkspaceRoute : DocumentWorkspace
    {
        [Parameter] public string? RoutePanel { get; set; }
        [Parameter] public string? RouteSubview { get; set; }
        protected override Task OnParametersSetAsync()
        {
            RequestedPanel = RoutePanel;
            RequestedSubview = RouteSubview;
            return base.OnParametersSetAsync();
        }
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
