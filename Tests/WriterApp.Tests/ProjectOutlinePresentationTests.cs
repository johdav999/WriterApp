using AngleSharp.Html.Parser;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using WriterApp.Device.Shared.Services;
using WriterApp.Device.Shared.Storage;
using Xunit;

namespace WriterApp.Tests;

public sealed class ProjectOutlinePresentationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "WriterApp.OutlinePresentation", Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProjectAndEmbeddedNavigatorRenderExplicitNativeDragValues(bool embedded)
    {
        using var services = new ServiceCollection().AddLogging()
            .AddWriterAppDeviceCore(new("Test", new Uri("https://test.invalid/")), _root)
            .AddSingleton<NavigationManager>(new TestNavigation())
            .AddSingleton<IJSRuntime>(new NoJs())
            .BuildServiceProvider();
        var document = await services.GetRequiredService<LocalDocumentRepository>().CreateProjectAsync("Outline");
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<WriterApp.Device.Shared.Pages.Projects>(
                ParameterView.FromDictionary(new Dictionary<string, object?>
                {
                    ["ProjectId"] = document.Project!.ProjectId,
                    ["EmbedNavigator"] = embedded
                }))).ToHtmlString());
        var rows = new HtmlParser().ParseDocument(html).QuerySelectorAll(".outline-drag-row");
        Assert.Equal(document.Project!.Nodes.Count, rows.Length);
        Assert.All(rows, row => Assert.Equal("true", row.GetAttribute("draggable")));
    }

    private sealed class TestNavigation : NavigationManager
    {
        public TestNavigation() => Initialize("http://localhost/", "http://localhost/projects");
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
