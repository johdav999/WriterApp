using WriterApp.Device.Shared.Components;
using WriterApp.Device.Shared.Services;
using WriterApp.Device.Shared.Storage;
using WriterApp.Shared;
using Xunit;

namespace WriterApp.Tests;

public sealed partial class LocalWritingPanelTests
{
    [Fact]
    public async Task WorkspaceCustomInputSendsLatestTextWithoutBlurToSectionReplacementReview()
    {
        await using var h = new Harness(); await h.Start();
        h.Selection = h.Selection with { Editor = h.Selection.Editor with { SelectedText = "", From = 1, To = 1 } };
        var workspace = new WriterApp.Device.Shared.Pages.DocumentWorkspace();
        void Set(string name, object value) => workspace.GetType().GetField(name,
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(workspace, value);
        Set("_writingPanel", h.Components.Panel);
        Set("_session", new LocalEditorSession(h.Fixture.Repository, h.Source));
        Set("_editor", new DeviceTextEditor());
        Set("_selectedPage", h.Source.Sections[0].Pages[0].PageId);
        const string instruction = "Add movement in the woods.";
        var subviews = (Dictionary<DeviceEditorPanel, string>)workspace.GetType().GetField("_subviews",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(workspace)!;
        subviews[DeviceEditorPanel.Writing] = "tools";
        using var tree = new Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder();
        workspace.GetType().GetMethod("BuildRenderTree", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(workspace, [tree]);
        var input = FindCustomInput(tree);
        typeof(LocalWritingPanel).GetProperty(nameof(LocalWritingPanel.ChildContent))!
            .SetValue(h.Components.Panel, FindWritingContent(tree));
        Assert.Equal("oninput", input.Event);
        // Invoke the generated binder with an input event, without dispatching blur/change.
        var callback = (MulticastDelegate)input.Callback.GetType().GetField("Delegate",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(input.Callback)!;
        await new Microsoft.AspNetCore.Components.EventCallbackWorkItem(callback).InvokeAsync(
            new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = instruction });
        await h.Renderer.Dispatcher.InvokeAsync(() => (Task)workspace.GetType().GetMethod("RunAiAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(workspace, [DeviceAiAction.Custom])!);
        var html = await h.Html();
        Assert.Contains("Review writing proposal", html);
        Assert.True(html.IndexOf("id=\"custom-ai-instruction\"", StringComparison.Ordinal) >= 0);
        Assert.True(html.IndexOf("id=\"custom-ai-instruction\"", StringComparison.Ordinal)
            < html.IndexOf("aria-label=\"Review writing proposal\"", StringComparison.Ordinal));
        Assert.True(h.Api.Last!.Parameters!.ContainsKey(WritingActions.Parameter));
        Assert.Equal("section", h.Api.Last.Parameters["scope"]);
        Assert.Equal(instruction, h.Api.Last.Parameters["template"]);
    }

#pragma warning disable BL0006 // Inspect the real generated input binding rather than assigning its backing field.
    private static Microsoft.AspNetCore.Components.RenderFragment? FindWritingContent(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder tree)
    {
        var frames = tree.GetFrames();
        for (int i = 0; i < frames.Count; i++) {
            var frame = frames.Array[i];
            if (frame.FrameType == Microsoft.AspNetCore.Components.RenderTree.RenderTreeFrameType.Component
                && frame.ComponentType == typeof(LocalWritingPanel)) {
                return (Microsoft.AspNetCore.Components.RenderFragment)frames.Array.Skip(i + 1)
                    .TakeWhile(f => f.FrameType == Microsoft.AspNetCore.Components.RenderTree.RenderTreeFrameType.Attribute)
                    .Single(f => f.AttributeName == nameof(LocalWritingPanel.ChildContent)).AttributeValue;
            }
            if (frame.FrameType == Microsoft.AspNetCore.Components.RenderTree.RenderTreeFrameType.Attribute
                && frame.AttributeName != "Main"
                && frame.AttributeValue is Microsoft.AspNetCore.Components.RenderFragment fragment) {
                using var child = new Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder(); fragment(child);
                if (FindWritingContent(child) is { } found) return found;
            }
        }
        return null;
    }

    private static (string Event, object Callback) FindCustomInput(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder tree)
    {
        var frames = tree.GetFrames();
        for (int i = 0; i < frames.Count; i++) {
            var frame = frames.Array[i];
            if (frame.FrameType == Microsoft.AspNetCore.Components.RenderTree.RenderTreeFrameType.Element && frame.ElementName == "textarea") {
                var attrs = frames.Array.Skip(i + 1).TakeWhile(f => f.FrameType == Microsoft.AspNetCore.Components.RenderTree.RenderTreeFrameType.Attribute).ToArray();
                if (attrs.Any(f => f.AttributeName == "id" && Equals(f.AttributeValue, "custom-ai-instruction"))) {
                    var binder = attrs.Single(f => f.AttributeName is "oninput" or "onchange");
                    return (binder.AttributeName, binder.AttributeValue);
                }
            }
            if (frame.FrameType == Microsoft.AspNetCore.Components.RenderTree.RenderTreeFrameType.Attribute
                && frame.AttributeName != "Main"
                && frame.AttributeValue is Microsoft.AspNetCore.Components.RenderFragment fragment) {
                using var child = new Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder(); fragment(child);
                var found = FindCustomInput(child); if (found.Callback is not null) return found;
            }
        }
        return default;
    }
#pragma warning restore BL0006

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CustomPromptReplacesSelectedPassageOrAllSectionPagesWithoutAppendingAndSupportsUndoRedo(bool selected)
    {
        await using var h = new Harness(); await h.Start();
        const string instruction = "Add movement in the dark woods that Elin notices but Jonas does not.";
        const string addition = " Something moved in the dark woods. Elin noticed; Jonas did not.";
        if (!selected) h.Selection = h.Selection with { Editor = h.Selection.Editor with {
            SelectedText = "", From = 1, To = 1, SelectionStart = 0, SelectionEnd = 0 } };
        h.Api.Before = _ => {
            if (h.Api.Last!.Parameters!.TryGetValue(WritingActions.Parameter, out var json)) {
                var source = WritingActions.Parse(json!.ToString()!);
                // A complete revised section repeats its original prose once and adds new prose.
                h.Api.Invalid = WritingActions.Serialize(source with { Pages = source.Pages.Select((p, i) =>
                    i == 0 ? p with { Runs = p.Runs.Select((r, j) => j == 0 ? r with { Text = r.Text + addition } : r).ToArray() } : p).ToArray() });
            } else h.Api.Invalid = h.Selection.Editor.SelectedText + addition;
            return Task.CompletedTask;
        };
        await h.Renderer.Dispatcher.InvokeAsync(() => h.Components.Panel.RunCustomAsync(instruction));
        Assert.Contains("Review writing proposal", await h.Html());
        Assert.Equal("custom_transform", h.Api.LastKey);
        var preset = ReusablePrompts.Parse(h.Api.Last!.Parameters![ReusablePrompts.Parameter]!.ToString()!);
        Assert.Equal(instruction, preset.Template);
        Assert.Equal(selected ? WritingScope.Selection : WritingScope.Section, preset.Scope);
        Assert.Equal(selected, h.Api.Last.SelectionStart is not null);
        Assert.Equal(!selected, h.Api.Last.Parameters.ContainsKey(WritingActions.Parameter));
        Assert.Equal(LocalDocumentCodec.Encode(h.Source), LocalDocumentCodec.Encode((await h.Fixture.Repository.LoadAsync(h.Source.DocumentId))!));
        await h.Event("Apply");
        var saved = (await h.Fixture.Repository.LoadAsync(h.Source.DocumentId))!;
        var original = h.Source.Sections[0].Pages[0].Content[3..^4];
        Assert.Equal(original + addition, DeviceAiRequests.PlainText(saved.Sections[0].Pages[0]));
        Assert.Equal(DeviceAiRequests.PlainText(h.Source.Sections[0].Pages[1]), DeviceAiRequests.PlainText(saved.Sections[0].Pages[1]));
        Assert.Equal(h.Source.Sections[1].Pages[0].Content, saved.Sections[1].Pages[0].Content);
        var entry = Assert.Single(await h.Fixture.History.HistoryAsync(h.Source.DocumentId), e => e.Status == "Applied");
        Assert.Equal(selected ? "Writing:selection" : "Writing:section", entry.Target);
        var history = new LocalAiHistoryActions(h.Fixture.Repository, new LocalAiStore(h.Fixture.Root + "/ai"));
        await history.ChangeAsync(h.Source.DocumentId, entry.Id, false);
        Assert.Equal(h.Source.Sections.SelectMany(s => s.Pages).Select(p => p.Content),
            (await h.Fixture.Repository.LoadAsync(h.Source.DocumentId))!.Sections.SelectMany(s => s.Pages).Select(p => p.Content));
        await history.ChangeAsync(h.Source.DocumentId, entry.Id, true);
        Assert.Equal(saved.Sections.SelectMany(s => s.Pages).Select(p => p.Content),
            (await h.Fixture.Repository.LoadAsync(h.Source.DocumentId))!.Sections.SelectMany(s => s.Pages).Select(p => p.Content));
    }

    [Theory]
    [InlineData("partial")]
    [InlineData("unsupported")]
    [InlineData("stale")]
    public async Task CustomSectionPromptRejectsIncompleteResultsUnsupportedBackendAndLaterEdits(string failure)
    {
        await using var h = new Harness(); await h.Start();
        h.Selection = h.Selection with { Editor = h.Selection.Editor with { SelectedText = "", From = 1, To = 1 } };
        if (failure == "partial") h.Api.Invalid = "Only some revised text, without the mapped section.";
        if (failure == "unsupported") h.Api.Presets = false;
        await h.Renderer.Dispatcher.InvokeAsync(() => h.Components.Panel.RunCustomAsync("Improve the scene."));
        if (failure == "stale") {
            Assert.Contains("Review writing proposal", await h.Html());
            h.Source = await h.Fixture.Repository.SaveAsync(h.Source with { Title = "Later edit" });
            await h.Event("Apply");
        }
        Assert.Contains("role=\"alert\"", await h.Html());
        Assert.DoesNotContain(await h.Fixture.History.HistoryAsync(h.Source.DocumentId), e => e.Status == "Applied");
        Assert.Equal(LocalDocumentCodec.Encode(h.Source), LocalDocumentCodec.Encode((await h.Fixture.Repository.LoadAsync(h.Source.DocumentId))!));
        if (failure == "unsupported") Assert.Equal(0, h.Api.Calls);
    }
}
