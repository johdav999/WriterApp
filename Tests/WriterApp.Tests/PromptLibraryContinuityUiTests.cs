using AngleSharp.Html.Parser;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WriterApp.UI.Shared;
using Xunit;

namespace WriterApp.Tests;

public sealed class PromptLibraryContinuityUiTests
{
    [Theory][InlineData(false)][InlineData(true)]
    public async Task CompiledHelpExplainsTheActualHostLibraryAndExplicitTransferSteps(bool cloud)
    {
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        var root = await renderer.Dispatcher.InvokeAsync(() => renderer.RenderComponentAsync<PromptLibraryContinuity>(
            ParameterView.FromDictionary(new Dictionary<string, object?> { [nameof(PromptLibraryContinuity.CloudLibrary)] = cloud })));
        var html = await renderer.Dispatcher.InvokeAsync(root.ToHtmlString);
        var doc = new HtmlParser().ParseDocument(html);
        Assert.Contains(cloud ? "Cloud library" : "Local device library", doc.Body!.TextContent);
        Assert.Contains("Libraries do not synchronize automatically", doc.Body.TextContent);
        Assert.Contains("no confirmed receipt", doc.Body.TextContent);
        Assert.Contains("same account and backend", doc.Body.TextContent);
        Assert.Contains("Later local edits are not added", doc.Body.TextContent);
        Assert.Contains("does not undo a cloud change", doc.Body.TextContent);
        Assert.Contains("Import separate local copy", doc.Body.TextContent);
        Assert.Single(doc.QuerySelectorAll("details > summary"));
        Assert.Equal(4, doc.QuerySelectorAll("ol > li").Length);
        if (Environment.GetEnvironmentVariable("WRITERAPP_P12_EVIDENCE") is { } path)
            await File.WriteAllTextAsync(Path.Combine(path, cloud ? "library-cloud-help.html" : "library-device-help.html"), html);
    }
}
