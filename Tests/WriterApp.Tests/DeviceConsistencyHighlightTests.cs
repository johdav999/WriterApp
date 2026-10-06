using System.Reflection;
using Microsoft.JSInterop;
using WriterApp.Device.Shared.Components;
using WriterApp.Device.Shared.Services;
using Xunit;

namespace WriterApp.Tests;

public sealed class DeviceConsistencyHighlightTests
{
    [Fact]
    public async Task NullPassageClearsEditorAndAllowsSamePassageToBeRequestedAgain()
    {
        var editor = new DeviceTextEditor();
        var js = new EditorJs();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(DeviceTextEditor).GetField("_editor", flags)!.SetValue(editor, js);
        var apply = typeof(DeviceTextEditor).GetMethod("ApplyAnnotationsAsync", flags)!;
        Task Refresh() => (Task)apply.Invoke(editor, null)!;
        var passage = new ConsistencyPassage(Guid.NewGuid(), "First passage", 0, "First", Guid.NewGuid(), Guid.NewGuid());
        typeof(DeviceTextEditor).GetProperty(nameof(DeviceTextEditor.ConsistencyPassage))!.SetValue(editor, passage);
        await Refresh(); await Refresh();
        Assert.Equal(1, js.Jumps);
        typeof(DeviceTextEditor).GetProperty(nameof(DeviceTextEditor.ConsistencyPassage))!.SetValue(editor, null);
        await Refresh(); await Refresh();
        Assert.Equal(1, js.Clears);
        typeof(DeviceTextEditor).GetProperty(nameof(DeviceTextEditor.ConsistencyPassage))!.SetValue(editor, passage);
        await Refresh();
        Assert.Equal(2, js.Jumps);
    }

    private sealed class EditorJs : IJSObjectReference
    {
        public int Jumps, Clears;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public ValueTask<T> InvokeAsync<T>(string id, object?[]? args) => InvokeAsync<T>(id, default, args);
        public ValueTask<T> InvokeAsync<T>(string id, CancellationToken ct, object?[]? args)
        {
            if (id == "navigateToConsistency") { Jumps++; return ValueTask.FromResult((T)(object)true); }
            if (id == "clearConsistencyHighlight") Clears++;
            else Assert.Equal("setAnnotations", id);
            return ValueTask.FromResult(default(T)!);
        }
    }
}
