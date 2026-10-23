using System.Reflection;
using WriterApp.Device.Shared.Components;
using Xunit;

namespace WriterApp.Tests;

public sealed partial class LocalWritingPanelTests
{
    [Fact]
    public async Task CloudLinkAcknowledgmentDoesNotCancelPreviewOrDiscardTheProposal()
    {
        await using var h = new Harness(); await h.Start();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        h.Api.Before = async ct => { entered.SetResult(); await release.Task.WaitAsync(ct); };
        var run = h.Event("Generate", "rewrite.selection");
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        h.Set("_cloudLinked", false);
        await h.Renderer.Dispatcher.InvokeAsync(async () => {
            h.Components.Panel.HasCloudCopy = true;
            await (Task)typeof(LocalWritingPanel).GetMethod("OnParametersSetAsync", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(h.Components.Panel, [])!;
        });
        release.SetResult(); await run;
        Assert.Contains("Review writing proposal", await h.Html());
        Assert.DoesNotContain("canceled", await h.Html());
        Assert.Equal(1, h.Api.Calls);
        Assert.Equal(h.Source.Sections[0].Pages[0].Content, (await h.Fixture.Repository.LoadAsync(h.Source.DocumentId))!.Sections[0].Pages[0].Content);
    }
}
