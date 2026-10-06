using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WriterApp.Shared;
using WriterApp.UI.Shared;
using Xunit;

namespace WriterApp.Tests;
#pragma warning disable BL0006 // Dispatch the compiled shared controls, including their child review.
public sealed partial class DesktopAiWebPersistenceTests
{
    private sealed class StyleControlRenderer(IServiceProvider services, ILoggerFactory logs, ConsistencyPageFixture fixture) : Renderer(services, logs)
    {
        private readonly StyleQualityCoachReview _component = new();
        private int _root; private bool _attached;
        public override Dispatcher Dispatcher { get; } = Dispatcher.CreateDefault();
        protected override void HandleException(Exception e) => throw new InvalidOperationException("Style controls failed", e);
        protected override Task UpdateDisplayAsync(in RenderBatch batch) => Task.CompletedTask;
        public Task Show() => Dispatcher.InvokeAsync(() => {
            if (!_attached) { _root = AssignRootComponentId(_component); _attached = true; }
            var review = TranslationField(fixture.Editor, "_styleReview");
            var report = review?.GetType().GetProperty("Report")?.GetValue(review) as StyleQualityReport;
            return RenderRootComponentAsync(_root, ParameterView.FromDictionary(new Dictionary<string, object?> {
                [nameof(StyleQualityCoachReview.Report)] = report,
                [nameof(StyleQualityCoachReview.Source)] = WriterApp.Client.State.PlainTextMapper.ToPlainText(StyleHtml),
                [nameof(StyleQualityCoachReview.Scope)] = TranslationField(fixture.Editor, "_styleReviewScope"),
                [nameof(StyleQualityCoachReview.CanGenerate)] = true,
                [nameof(StyleQualityCoachReview.Approved)] = TranslationField(fixture.Editor, "_approvedStyleEdits"),
                [nameof(StyleQualityCoachReview.ScopeChanged)] = EventCallback.Factory.Create<string>(this, scope => fixture.Event("StyleReviewScopeChanged", scope)),
                [nameof(StyleQualityCoachReview.Generate)] = EventCallback.Factory.Create(this, () => fixture.Event("RunStyleReviewAsync")),
                [nameof(StyleQualityCoachReview.Apply)] = EventCallback.Factory.Create(this, () => fixture.Event("ApplyStyleReviewAsync")),
                [nameof(StyleQualityCoachReview.Dismiss)] = EventCallback.Factory.Create(this, () => fixture.Renderer.Dispatcher.InvokeAsync(() => typeof(WriterApp.Client.Pages.DocumentEditor).GetMethod("DismissStyleReview", TranslationPrivate)!.Invoke(fixture.Editor, null))),
                [nameof(StyleQualityCoachReview.ApprovedChanged)] = EventCallback.Factory.Create<IReadOnlyCollection<int>>(this, indexes => fixture.Event("StyleEditsChanged", indexes))
            }));
        });
        public Task Click(int index) => Dispatch(_root, "onclick", index, new MouseEventArgs());
        public Task Scope(string value) => Dispatch(_root, "onchange", 0, new ChangeEventArgs { Value = value });
        public Task Check(int index, bool value) => Dispatcher.InvokeAsync(() => {
            var frames = GetCurrentRenderTreeFrames(_root);
            int child = frames.Array.Take(frames.Count).Single(f => f.FrameType == RenderTreeFrameType.Component && f.ComponentType == typeof(StyleQualityRevisionReview)).ComponentId;
            return Dispatch(child, "onchange", index, new ChangeEventArgs { Value = value });
        });
        private Task Dispatch(int component, string attribute, int index, EventArgs args) => Dispatcher.InvokeAsync(() => {
            var frames = GetCurrentRenderTreeFrames(component);
            ulong handler = frames.Array.Take(frames.Count).Where(f => f.FrameType == RenderTreeFrameType.Attribute && f.AttributeName == attribute).ElementAt(index).AttributeEventHandlerId;
            return DispatchEventAsync(handler, null, args);
        });
    }
    [Fact]
    public async Task ClientStyleReviewRenderedScopeGenerateCheckboxApplyAndDismissUseProductionWorkflow()
    {
        await using var h = await StyleFixture();
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new StyleControlRenderer(services, services.GetRequiredService<ILoggerFactory>(), h);
        await renderer.Show(); await renderer.Scope("selection"); Assert.Equal("selection", TranslationField(h.Editor, "_styleReviewScope"));
        await renderer.Scope("page"); await renderer.Click(0); Assert.NotNull(TranslationField(h.Editor, "_styleReview"));
        await renderer.Show(); await renderer.Check(1, false);
        Assert.Equal(new[] { 0 }, (IReadOnlyCollection<int>)TranslationField(h.Editor, "_approvedStyleEdits")!);
        await renderer.Show(); await renderer.Click(1); Assert.Null(StyleError(h)); Assert.Single(h.Saves.Sources);
        Assert.Contains("chime", h.Js.Html); Assert.Contains("very tired", h.Js.Html);
        h.Provider.OutputForCall = (_, _) => "{\"edits\":[]}";
        await renderer.Show(); await renderer.Click(0); await renderer.Show(); await renderer.Click(2);
        Assert.Null(TranslationField(h.Editor, "_styleReview")); Assert.Single(h.Saves.Sources);
    }
    [Theory][InlineData("idle")][InlineData("all")][InlineData("partial")][InlineData("none")][InlineData("empty")][InlineData("busy")][InlineData("error")]
    public async Task ClientStyleReviewSharedControlsRenderApprovalPreviewAndAvailability(string state)
    {
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        var report = new StyleQualityReport([StyleReport.Edits[0] with { Kind = "correction", Reason = "<script>inert 日本語 🧭</script>" }, StyleReport.Edits[1]]);
        int[] approved = state is "all" or "busy" ? [0, 1] : state is "partial" or "error" ? [1] : [];
        var html = await renderer.Dispatcher.InvokeAsync(async () => (await renderer.RenderComponentAsync<StyleQualityCoachReview>(ParameterView.FromDictionary(new Dictionary<string, object?> {
            [nameof(StyleQualityCoachReview.Report)] = state == "idle" ? null : state == "empty" ? new StyleQualityReport([]) : report,
            [nameof(StyleQualityCoachReview.Source)] = WriterApp.Client.State.PlainTextMapper.ToPlainText(StyleHtml),
            [nameof(StyleQualityCoachReview.Approved)] = approved, [nameof(StyleQualityCoachReview.Busy)] = state == "busy",
            [nameof(StyleQualityCoachReview.CanGenerate)] = state != "error",
            [nameof(StyleQualityCoachReview.Unavailable)] = "Update the backend to enable style review.",
            [nameof(StyleQualityCoachReview.Error)] = state == "error" ? "The writing changed. Generate another review." : null
        }))).ToHtmlString());
        var dom = new AngleSharp.Html.Parser.HtmlParser().ParseDocument(html);
        Assert.Equal(2, dom.QuerySelectorAll("select option").Length);
        Assert.DoesNotContain("<script>", html);
        Assert.Equal(approved.Length, dom.QuerySelectorAll("input[checked]").Length);
        if (state is not "idle" and not "empty") {
            Assert.Contains("Possible correction", html); Assert.Contains("Optional style preference", html); Assert.Contains("&lt;script&gt;", html);
            Assert.Contains("Why:", html); Assert.Contains("Effect on voice or emphasis:", html);
            string preview = dom.QuerySelectorAll("details .ai-preview-text")[1].TextContent;
            Assert.Equal(StyleQualityReview.Compose(report, WriterApp.Client.State.PlainTextMapper.ToPlainText(StyleHtml), approved), preview);
        }
        var apply = dom.QuerySelectorAll("button").SingleOrDefault(b => b.TextContent.Contains("Apply selected"));
        if (apply is not null) Assert.Equal(state == "busy" || approved.Length == 0, apply.HasAttribute("disabled"));
        if (state == "busy") Assert.Equal(2, dom.QuerySelectorAll("input[disabled]").Length);
        if (state == "empty") Assert.Contains("No changes suggested", html);
        if (state == "error") { Assert.NotNull(dom.QuerySelector("[role=alert]")); Assert.NotNull(dom.QuerySelector("[role=status]")); }
        string? evidence = Environment.GetEnvironmentVariable("WRITERAPP_PARITY_P06_UI_EVIDENCE");
        if (evidence is not null) { Directory.CreateDirectory(evidence); await File.WriteAllTextAsync(Path.Combine(evidence, state + ".html"), html); }
    }
}
