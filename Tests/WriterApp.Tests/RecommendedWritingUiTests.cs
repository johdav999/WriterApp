using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WriterApp.Shared;
using WriterApp.UI.Shared;
using Xunit;
namespace WriterApp.Tests;
#pragma warning disable BL0006 // Dispatch compiled copy-only selection controls.
public sealed class RecommendedWritingUiTests
{
    private static readonly RecommendedTextResult Headlines = new(["First headline", "<script>inert 日本語 🧭</script>", "Third headline", "Fourth headline", "Fifth headline"]);
    private sealed class Controls(IServiceProvider services, ILoggerFactory logs) : Renderer(services, logs) {
        private readonly RecommendedTextReview _component = new(); private int _root; public string? Copied; public bool Dismissed;
        public override Dispatcher Dispatcher { get; } = Dispatcher.CreateDefault();
        protected override void HandleException(Exception e) => throw new InvalidOperationException("Recommendation UI failed", e);
        protected override Task UpdateDisplayAsync(in RenderBatch batch) => Task.CompletedTask;
        public Task Show() => Dispatcher.InvokeAsync(() => { _root = AssignRootComponentId(_component); return RenderRootComponentAsync(_root, ParameterView.FromDictionary(new Dictionary<string, object?> {
            [nameof(RecommendedTextReview.Result)] = Headlines,
            [nameof(RecommendedTextReview.Copy)] = EventCallback.Factory.Create<string>(this, text => Copied = text),
            [nameof(RecommendedTextReview.Dismiss)] = EventCallback.Factory.Create(this, () => Dismissed = true)
        })); });
        public Task Event(string attribute, int index, EventArgs args) => Dispatcher.InvokeAsync(() => { var frames = GetCurrentRenderTreeFrames(_root); return DispatchEventAsync(frames.Array.Take(frames.Count).Where(f => f.FrameType == RenderTreeFrameType.Attribute && f.AttributeName == attribute).ElementAt(index).AttributeEventHandlerId, null, args); });
    }
    [Fact]
    public async Task RecommendedCompiledRadioCopyAndDismissUseTheSelectedInertText() {
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider(); await using var renderer = new Controls(services, services.GetRequiredService<ILoggerFactory>());
        await renderer.Show(); await renderer.Event("onchange", 2, new ChangeEventArgs { Value = true }); await renderer.Event("onclick", 0, new MouseEventArgs()); Assert.Equal("Third headline", renderer.Copied);
        await renderer.Event("onchange", 1, new ChangeEventArgs { Value = true }); await renderer.Event("onclick", 0, new MouseEventArgs()); Assert.Equal(Headlines.Items[1], renderer.Copied);
        await renderer.Event("onclick", 1, new MouseEventArgs()); Assert.True(renderer.Dismissed);
    }
    [Theory][InlineData("client", "headlines", false)][InlineData("desktop", "headlines", false)][InlineData("client", "summary", false)][InlineData("desktop", "summary", false)][InlineData("client", "busy", true)][InlineData("desktop", "busy", true)]
    public async Task RecommendedSharedCopyReviewShowsEncodedCandidatesAndNoManuscriptApply(string host, string state, bool busy) {
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider(); await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        string html = await renderer.Dispatcher.InvokeAsync(async () => (await renderer.RenderComponentAsync<RecommendedTextReview>(ParameterView.FromDictionary(new Dictionary<string, object?> {
            [nameof(RecommendedTextReview.ToolId)] = state == "summary" ? "other.summarize_clearly" : "blog.generate_headlines",
            [nameof(RecommendedTextReview.Result)] = state == "summary" ? new RecommendedTextResult(["The narrator discovers an old letter. Its contents change her next decision."]) : Headlines,
            [nameof(RecommendedTextReview.Busy)] = busy
        }))).ToHtmlString());
        var dom = new AngleSharp.Html.Parser.HtmlParser().ParseDocument(html);
        Assert.DoesNotContain("<script>", html); Assert.DoesNotContain("Apply", dom.Body!.TextContent);
        Assert.Equal(state == "summary" ? 1 : 5, dom.QuerySelectorAll("input[type=radio]").Length);
        Assert.Contains("manuscript stays unchanged", html); Assert.Equal(busy, dom.QuerySelector("fieldset")!.HasAttribute("disabled"));
        string? evidence = Environment.GetEnvironmentVariable("WRITERAPP_PARITY_P07_UI_EVIDENCE");
        if (evidence is not null) { Directory.CreateDirectory(evidence); await File.WriteAllTextAsync(Path.Combine(evidence, host + "-" + state + ".html"), html); }
    }
}
