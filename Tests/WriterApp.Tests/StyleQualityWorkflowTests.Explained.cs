using System.Reflection;
using AngleSharp.Html.Parser;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using WriterApp.Device.Shared.Pages;
using WriterApp.Device.Shared.Services;
using WriterApp.Shared;
using WriterApp.UI.Shared;
using Xunit;

namespace WriterApp.Tests;
public sealed partial class StyleQualityWorkflowTests
{
    [Fact]
    public async Task ActualWorkspaceSelectsIndividualExplainedEditsAndDisablesEmptyApproval()
    {
        string dir = Path.Combine(Path.GetTempPath(), "WriterApp.StyleQuality", Guid.NewGuid().ToString("N"));
        try {
            var components = new CapturedWorkspace();
            var registrations = new ServiceCollection().AddLogging().AddSingleton<IJSRuntime, NoJs>()
                .AddSingleton<NavigationManager, Navigation>().AddSingleton<IComponentActivator>(components);
            registrations.AddWriterAppDeviceCore(new("Test", new("https://test.invalid/")), dir);
            using var services = registrations.BuildServiceProvider();
            var repository = services.GetRequiredService<LocalDocumentRepository>();
            const string source = "Elin walked very slowly. She was really tired.";
            var doc = await repository.CreateImportedAsync("Draft", "<p>" + source + "</p>");
            doc = doc with { ServerDocumentId = Guid.NewGuid(), ServerVersion = "v1", SyncState = WriterApp.Device.Shared.Storage.LocalSyncState.Synced };
            var page = doc.Sections[0].Pages[0];
            var snapshot = new WriterApp.Device.Shared.Components.AiEditorSnapshot(page.Content, source, source, 0, source.Length, 1, source.Length + 1, 0);
            var prepared = DeviceAiRequests.Build(doc, doc.Sections[0], page, snapshot, DeviceAiAction.StyleQuality, styleGoal: "concise");
            var report = new StyleQualityReport([
                new("Elin walked very slowly.", "Elin walked slowly.", "word_choice", "preference", "Remove an unnecessary intensifier.", "Less emphasis on her pace."),
                new("She was really tired.", "She was tired.", "clarity", "preference", "Remove redundant emphasis <script>.", "Less emphasis on fatigue.")]);
            var proposal = new DeviceAiProposal(prepared, StyleQualityReview.Compose(report, source, [0, 1]), "Run custom prompt", source, new()) { StyleReview = report };
            await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
            var rendered = await renderer.Dispatcher.InvokeAsync(() => renderer.RenderComponentAsync<QualityWorkspace>(ParameterView.FromDictionary(new Dictionary<string, object?> {
                [nameof(QualityWorkspace.Saved)] = doc, [nameof(QualityWorkspace.Preview)] = proposal
            })));
            string html = await renderer.Dispatcher.InvokeAsync(rendered.ToHtmlString);
            Assert.Contains("More concise", html); Assert.Contains("What the AI reviews", html);
            Assert.Contains("Optional style preference", html); Assert.Contains("Less emphasis on her pace", html);
            Assert.Contains("&lt;script&gt;", html); Assert.DoesNotContain("Run custom prompt", html);
            Assert.Contains("ai-change-before", html); Assert.Contains("ai-change-after", html);
            await Select(renderer, components.Workspace!, [0]);
            var selected = (DeviceAiProposal)typeof(DocumentWorkspace).GetField("_aiProposal", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(components.Workspace)!;
            Assert.Equal("Elin walked slowly. She was really tired.", selected.ProposedText);
            html = await renderer.Dispatcher.InvokeAsync(rendered.ToHtmlString);
            Assert.Contains("1 of 2 changes selected", html);
            await Select(renderer, components.Workspace!, []);
            html = await renderer.Dispatcher.InvokeAsync(rendered.ToHtmlString);
            Assert.Contains("0 of 2 changes selected", html);
            var button = new HtmlParser().ParseDocument(html).QuerySelectorAll("button").Single(b => b.TextContent == "Apply style revision");
            Assert.True(button.HasAttribute("disabled"));
            Assert.Equal(source, ((DeviceAiProposal)typeof(DocumentWorkspace).GetField("_aiProposal", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(components.Workspace)!).ProposedText);
            Assert.Empty(await services.GetRequiredService<WriterApp.Device.Shared.Storage.LocalAiStore>().HistoryAsync(doc.DocumentId));

            string? evidence = Environment.GetEnvironmentVariable("WRITERAPP_STYLE_EVIDENCE");
            if (evidence is not null) {
                Directory.CreateDirectory(evidence);
                string options = await renderer.Dispatcher.InvokeAsync(async () => (await renderer.RenderComponentAsync<StyleQualityOptions>(ParameterView.FromDictionary(new Dictionary<string, object?> { [nameof(StyleQualityOptions.Goal)] = "concise" }))).ToHtmlString());
                string review = await renderer.Dispatcher.InvokeAsync(async () => (await renderer.RenderComponentAsync<StyleQualityRevisionReview>(ParameterView.FromDictionary(new Dictionary<string, object?> {
                    [nameof(StyleQualityRevisionReview.Report)] = new StyleQualityReport(report.Edits.Select(e => e with { Reason = e.Reason.Replace(" <script>", "") }).ToArray()), [nameof(StyleQualityRevisionReview.Source)] = source,
                    [nameof(StyleQualityRevisionReview.Goal)] = "concise", [nameof(StyleQualityRevisionReview.Approved)] = new int[] { 0 }
                }))).ToHtmlString());
                await File.WriteAllTextAsync(Path.Combine(evidence, "review.html"), "<h2>Style &amp; quality Coach</h2>" + options + "<h3>Review before applying</h3>" + review);
            }
        } finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }
    private static Task Select(HtmlRenderer renderer, DocumentWorkspace workspace, IReadOnlyCollection<int> indexes) => renderer.Dispatcher.InvokeAsync(() =>
        ((IHandleEvent)workspace).HandleEventAsync(new EventCallbackWorkItem((Func<Task>)(() =>
            (Task)typeof(DocumentWorkspace).GetMethod("StyleEditsChanged", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(workspace, [indexes])!)), null));
    private sealed class CapturedWorkspace : IComponentActivator {
        public QualityWorkspace? Workspace;
        public IComponent CreateInstance(Type type) {
            var component = (IComponent)Activator.CreateInstance(type)!;
            if (component is QualityWorkspace workspace) Workspace = workspace;
            return component;
        }
    }
}
