using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WriterApp.UI.Shared;
using Xunit;

#pragma warning disable BL0006 // Dispatch the actual rendered Cancel button in the framework renderer.

namespace WriterApp.Tests;
public sealed partial class DesktopAiWebPersistenceTests
{
    private sealed class CancellationControlRenderer(IServiceProvider services, ILoggerFactory logs) : Renderer(services, logs)
    {
        private int _root;
        private ulong _click;
        public override Dispatcher Dispatcher { get; } = Dispatcher.CreateDefault();
        protected override void HandleException(Exception error) => throw new InvalidOperationException("Cancel control failed", error);
        protected override Task UpdateDisplayAsync(in RenderBatch batch)
        {
            foreach (var frame in batch.ReferenceFrames.Array.Take(batch.ReferenceFrames.Count))
                if (frame.FrameType == RenderTreeFrameType.Attribute && frame.AttributeName == "onclick") _click = frame.AttributeEventHandlerId;
            return Task.CompletedTask;
        }
        public Task Show(ParameterView parameters) => Dispatcher.InvokeAsync(() =>
        {
            if (_root == 0) _root = AssignRootComponentId(new AiRequestProgress());
            return RenderRootComponentAsync(_root, parameters);
        });
        public Task ClickCancel() => Dispatcher.InvokeAsync(() => DispatchEventAsync(_click, null, new MouseEventArgs()));
    }

    [Fact]
    public async Task ClientAiCancellationActualRenderedCancelButtonCancelsTheOwnedRequest()
    {
        await using var f = new TranslationFixture(); await f.Start(); using var provider = new CheckedProvider(f); using var editor = CheckedEditor(f, provider);
        var gate = new RequestGate("/execute"); using var transport = new CancellationTransport(provider, gate);
        var page = (await System.Net.Http.Json.HttpClientJsonExtensions.GetFromJsonAsync<WriterApp.Application.Documents.PageDto[]>(f.Http, $"api/sections/{f.Sections[0]}/pages"))![0];
        await ConfigureCancellationEditor(f, editor, transport, page);
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new TranslationRenderer(services, services.GetRequiredService<ILoggerFactory>()); await renderer.Attach(editor);
        var operation = CancellationFlow(renderer, editor, "writing"); await gate.Ready.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await using var control = new CancellationControlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        await control.Show(ParameterView.FromDictionary(new Dictionary<string, object?> {
            [nameof(AiRequestProgress.IsPending)] = true, [nameof(AiRequestProgress.RequestName)] = "Expand section",
            [nameof(AiRequestProgress.Cancel)] = EventCallback.Factory.Create(new object(), () => CancelRequest(renderer, editor)) }));
        await control.ClickCancel();
        Assert.True(gate.Token.IsCancellationRequested); Assert.Null(TranslationField(editor, "_clientAiRequest"));
        gate.Release.TrySetResult(); await operation.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Null(TranslationField(editor, "_pendingAiProposal")); await AssertCancellationWritingUnchanged(f);
    }

    [Theory][InlineData("Writing suggestion")][InlineData("Quality revision")][InlineData("Consistency check")]
    public async Task ClientAiCancellationUiShowsPendingCancelAndReturnsToIdle(string operation)
    {
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        string pending = await renderer.Dispatcher.InvokeAsync(async () => (await renderer.RenderComponentAsync<AiRequestProgress>(
            ParameterView.FromDictionary(new Dictionary<string, object?> { [nameof(AiRequestProgress.IsPending)] = true, [nameof(AiRequestProgress.RequestName)] = operation }))).ToHtmlString());
        string idle = await renderer.Dispatcher.InvokeAsync(async () => (await renderer.RenderComponentAsync<AiRequestProgress>(
            ParameterView.FromDictionary(new Dictionary<string, object?> { [nameof(AiRequestProgress.IsPending)] = false }))).ToHtmlString());
        Assert.Contains("Cancel AI request", pending); Assert.Contains(operation, pending); Assert.Contains("role=\"status\"", pending);
        Assert.DoesNotContain("Dismiss", pending); Assert.DoesNotContain("Cancel AI request", idle);
        var evidence = Environment.GetEnvironmentVariable("WRITERAPP_P02_EVIDENCE");
        if (!string.IsNullOrWhiteSpace(evidence))
        {
            Directory.CreateDirectory(evidence);
            await File.WriteAllTextAsync(Path.Combine(evidence, operation.Split(' ')[0].ToLowerInvariant() + "-pending.html"), pending);
            await File.WriteAllTextAsync(Path.Combine(evidence, "idle.html"), idle);
        }
    }
}
