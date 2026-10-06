using System.Reflection;
using AngleSharp.Html.Parser;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WriterApp.Shared;
using WriterApp.UI.Shared;
using Xunit;

namespace WriterApp.Tests;

public sealed class TargetedQualityRetryOptionsTests
{
    private sealed class Capture : IComponentActivator
    {
        public TargetedQualityRetryOptions? Options;
        public IComponent CreateInstance(Type type) { var component = (IComponent)Activator.CreateInstance(type)!; if (component is TargetedQualityRetryOptions options) Options = options; return component; }
    }
    [Theory][InlineData(false, false)][InlineData(true, false)][InlineData(false, true)][InlineData(true, true)]
    public async Task SharedChoiceDefaultsOffDisclosesQuotaAndRoutesOnlyExplicitIdleChanges(bool enabled, bool busy)
    {
        var capture = new Capture(); bool? changed = null;
        using var services = new ServiceCollection().AddLogging().AddSingleton<IComponentActivator>(capture).BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        var root = await renderer.Dispatcher.InvokeAsync(() => renderer.RenderComponentAsync<TargetedQualityRetryOptions>(ParameterView.FromDictionary(new Dictionary<string, object?> {
            [nameof(TargetedQualityRetryOptions.Enabled)] = enabled, [nameof(TargetedQualityRetryOptions.Busy)] = busy,
            [nameof(TargetedQualityRetryOptions.EnabledChanged)] = EventCallback.Factory.Create<bool>(new object(), (bool value) => changed = value)
        })));
        string html = await renderer.Dispatcher.InvokeAsync(root.ToHtmlString);
        var document = new HtmlParser().ParseDocument(html); var input = Assert.Single(document.QuerySelectorAll("input"));
        Assert.Equal(enabled, input.HasAttribute("checked")); Assert.Equal(busy, input.HasAttribute("disabled"));
        Assert.Equal("checkbox", input.GetAttribute("type"));
        Assert.Contains(TargetedQualityRetry.Label, document.Body!.TextContent); Assert.Contains(TargetedQualityRetry.Explanation, document.Body.TextContent);
        Assert.NotNull(document.GetElementById(input.GetAttribute("aria-describedby")!));
        await renderer.Dispatcher.InvokeAsync(() => ((IHandleEvent)capture.Options!).HandleEventAsync(new EventCallbackWorkItem((Func<Task>)(() =>
            (Task)typeof(TargetedQualityRetryOptions).GetMethod("Change", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(capture.Options, [new ChangeEventArgs { Value = !enabled }])!)), null));
        if (busy) Assert.Null(changed); else Assert.Equal(!enabled, changed);
        if (Environment.GetEnvironmentVariable("WRITERAPP_P11_UI_EVIDENCE") is { } path)
            await File.WriteAllTextAsync(Path.Combine(path, (enabled ? "enabled" : "disabled") + (busy ? "-busy" : "-idle") + ".html"), html);
    }
}
