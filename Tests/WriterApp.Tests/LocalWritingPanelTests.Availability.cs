using AngleSharp.Html.Parser;
using WriterApp.Device.Shared.Services;
using WriterApp.Shared;
using Xunit;

namespace WriterApp.Tests;

public sealed partial class LocalWritingPanelTests
{
    [Fact]
    public async Task WritingDisposalWaitsForStartedAccountPresetReadToReleaseTheStore()
    {
        await using var h = new Harness(); await h.Start();
        var gate = new PanelReadGateStore(h.Fixture.Root);
        typeof(WriterApp.Device.Shared.Components.LocalWritingPanel).GetProperty("Documents", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(h.Components.Panel, new LocalDocumentRepository(gate));
        Task? disposing = null;
        try {
            await h.Renderer.Dispatcher.InvokeAsync(() => h.Fixture.Account.SignOutAsync());
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            disposing = h.Renderer.DisposeAsync().AsTask();
            Assert.False(disposing.IsCompleted);
        }
        finally { gate.Release.TrySetResult(); if (disposing is not null) await disposing.WaitAsync(TimeSpan.FromSeconds(5)); }
        Assert.Equal(WriterApp.Device.Shared.Storage.LocalDocumentCodec.Encode(h.Source),
            WriterApp.Device.Shared.Storage.LocalDocumentCodec.Encode((await h.Fixture.Repository.LoadAsync(h.Source.DocumentId))!));
        using var unlocked = new FileStream(Path.Combine(h.Fixture.Root, ".store.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    }

    [Theory]
    [InlineData(WritingScope.Selection, "rewrite.selection", "server")]
    [InlineData(WritingScope.Section, "expand.section", "server")]
    [InlineData(WritingScope.Section, "tighten.section", "network")]
    [InlineData(WritingScope.Continuation, "propose.next-paragraph", "json")]
    public async Task OptionalDemoFailureDoesNotDisableOrdinaryWriting(WritingScope scope, string key, string failure)
    {
        await using var h = new Harness();
        h.Api.DemoBefore = _ => throw failure switch {
            "network" => new HttpRequestException("Practice demo lookup failed."),
            "json" => new System.Text.Json.JsonException("Invalid practice demo response."),
            _ => new DeviceAiException(DeviceAiFailure.Server, "Practice demo storage is unavailable.")
        };
        await h.Start();
        h.Set("_scope", scope);
        await h.Event("Refresh");
        var html = new HtmlParser().ParseDocument(await h.Html());
        Assert.All(html.QuerySelectorAll("button").Where(b => b.GetAttribute("aria-label")?.EndsWith(scope.ToString().ToLowerInvariant()) == true || b.TextContent.EndsWith(scope.ToString().ToLowerInvariant()) || scope == WritingScope.Selection && b.TextContent == "Preview rewrite" || b.TextContent == "Propose next paragraph"),
            b => Assert.False(b.HasAttribute("disabled")));
        Assert.Contains("Practice demo unavailable", html.Body!.TextContent);
        Assert.Equal(0, h.Api.Calls);
        await h.Event("Generate", key);
        Assert.Equal(1, h.Api.Calls);
        Assert.Contains("Review before applying", await h.Html());
        Assert.Equal(h.Source.Sections.SelectMany(s => s.Pages).Select(p => p.Content),
            (await h.Fixture.Repository.LoadAsync(h.Source.DocumentId))!.Sections.SelectMany(s => s.Pages).Select(p => p.Content));
        await h.Event("Apply");
        Assert.Contains(await h.Fixture.History.HistoryAsync(h.Source.DocumentId), e => e.Status == "Applied");
    }

    [Fact]
    public async Task OptionalDemoFailureDoesNotOverrideDeniedWritingAccess()
    {
        await using var h = new Harness();
        h.Api.Free = true;
        h.Api.DemoBefore = _ => throw new DeviceAiException(DeviceAiFailure.Server, "Demo storage unavailable.");
        await h.Start();
        var html = new HtmlParser().ParseDocument(await h.Html());
        Assert.All(html.QuerySelectorAll("button").Where(b => b.GetAttribute("aria-label")?.EndsWith("selection") == true || b.TextContent.EndsWith("selection") || b.TextContent == "Preview rewrite"), b => Assert.True(b.HasAttribute("disabled")));
        Assert.Contains("AI is unavailable for this plan or environment", html.Body!.TextContent);
        await h.Event("Generate", "rewrite.selection");
        Assert.Equal(0, h.Api.Calls);
    }
    [Fact]
    public async Task SlowDemoLookupPublishesOrdinaryAvailabilityAndLateFailureCannotRestorePreviousAccount()
    {
        await using var h = new Harness();
        await h.Start();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        h.Api.DemoBefore = async _ => {
            h.Api.DemoBefore = null;
            entered.SetResult();
            await release.Task; // Simulate a backend that ignores cancellation.
            throw new DeviceAiException(DeviceAiFailure.Server, "Late demo outage.");
        };
        var refresh = h.Event("Refresh");
        try {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var ready = new HtmlParser().ParseDocument(await h.Html()).QuerySelector(".writing-rewrite");
            Assert.NotNull(ready);
            Assert.False(ready.HasAttribute("disabled"));
            await h.Renderer.Dispatcher.InvokeAsync(() => h.Fixture.Account.SignOutAsync());
            await (Task)typeof(WriterApp.Device.Shared.Components.LocalWritingPanel).GetField("_availabilityRefresh", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(h.Components.Panel)!;
        }
        finally { release.TrySetResult(); await refresh; }
        var signedOut = new HtmlParser().ParseDocument(await h.Html());
        Assert.True(signedOut.QuerySelector(".writing-rewrite")!.HasAttribute("disabled"));
        Assert.DoesNotContain("Practice demo unavailable", signedOut.Body!.TextContent);
        Assert.Contains("Sign in to use writing AI", signedOut.Body.TextContent);
        Assert.Equal(0, h.Api.Calls);
    }

    [Fact]
    public async Task RefreshRecoversDemoOutageWithoutReopeningThePanel()
    {
        await using var h = new Harness();
        h.Api.DemoBefore = _ => throw new DeviceAiException(DeviceAiFailure.Server, "Demo outage.");
        await h.Start();
        Assert.Contains("Practice demo unavailable", await h.Html());
        h.Api.DemoBefore = null;
        await h.Event("Refresh");
        Assert.DoesNotContain("Practice demo unavailable", await h.Html());
        Assert.False(new HtmlParser().ParseDocument(await h.Html()).QuerySelector(".writing-rewrite")!.HasAttribute("disabled"));
        Assert.Equal(0, h.Api.Calls);
    }
}
