using System.Reflection;
using Microsoft.JSInterop;
using WriterApp.Device.Shared.Components;
using WriterApp.Device.Shared.Services;
using Xunit;

namespace WriterApp.Tests;

public sealed class DeviceAnnotationInteropTests
{
    [Fact]
    public async Task AnnotationArrayIsOneJavaScriptArgumentIncludingEmptyUpdates()
    {
        var js = new RecordingEditor();
        var editor = new DeviceTextEditor();
        typeof(DeviceTextEditor).GetField("_editor", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(editor, js);
        var update = typeof(DeviceTextEditor).GetMethod("ApplyAnnotationsAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        foreach (var items in new[] {
            new[] { new LocalTextAnnotation(Guid.NewGuid(), "quote", "comment", "open", "Review"), new LocalTextAnnotation(Guid.NewGuid(), "other", "todo", "open", "Fix") },
            Array.Empty<LocalTextAnnotation>() })
        {
            typeof(DeviceTextEditor).GetProperty(nameof(DeviceTextEditor.Annotations))!.SetValue(editor, items);
            await (Task)update.Invoke(editor, null)!;
            Assert.Equal("setAnnotations", js.Identifier);
            Assert.Single(js.Arguments!);
            Assert.Equal(items, Assert.IsType<LocalTextAnnotation[]>(js.Arguments![0]));
        }
    }

    private sealed class RecordingEditor : IJSObjectReference
    {
        public string? Identifier;
        public object?[]? Arguments;
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) { Identifier = identifier; Arguments = args; return ValueTask.FromResult(default(TValue)!); }
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => InvokeAsync<TValue>(identifier, args);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
