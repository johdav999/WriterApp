using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using WriterApp.UI.Shared;
using Xunit;

namespace WriterApp.Tests;

public sealed class PanelResizeHandleTests
{
    private sealed class Components : IComponentActivator
    {
        public PanelResizeHandle Handle = default!;
        public IComponent CreateInstance(Type type)
        {
            var component = (IComponent)Activator.CreateInstance(type)!;
            if (component is PanelResizeHandle handle) Handle = handle;
            return component;
        }
    }
    private sealed class DelayedJs : IJSRuntime, IJSObjectReference
    {
        public TaskCompletionSource Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Imports, Attachments, Disposals;
        public ValueTask<T> InvokeAsync<T>(string name, object?[]? args) => InvokeAsync<T>(name, default, args);
        public async ValueTask<T> InvokeAsync<T>(string name, CancellationToken ct, object?[]? args)
        {
            if (name == "import") { Imports++; Started.TrySetResult(); await Release.Task; return (T)(object)this; }
            if (name == "attach") Attachments++;
            return default!;
        }
        public ValueTask DisposeAsync() { Disposals++; return ValueTask.CompletedTask; }
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PanelRemovedDuringImportNeverAttachesAndOverlappingRendersReuseOneModule(bool remove)
    {
        var js = new DelayedJs(); var components = new Components();
        using var services = new ServiceCollection().AddLogging().AddSingleton<IJSRuntime>(js)
            .AddSingleton<IComponentActivator>(components).BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            await renderer.RenderComponentAsync<PanelResizeHandle>();
            Task AfterRender() => (Task)typeof(PanelResizeHandle).GetMethod("OnAfterRenderAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(components.Handle, [false])!;
            var first = AfterRender(); await js.Started.Task;
            await AfterRender();
            if (remove) await components.Handle.DisposeAsync();
            js.Release.TrySetResult(); await first;
            Assert.Equal(1, js.Imports);
            Assert.Equal(remove ? 0 : 1, js.Attachments);
            if (remove) Assert.Equal(1, js.Disposals);
        });
    }
}
