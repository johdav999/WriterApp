using System.Reflection;
using AngleSharp.Html.Parser;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using WriterApp.Application.AI;
using WriterApp.Application.Usage;
using WriterApp.Device.Shared.Components;
using WriterApp.Device.Shared.Pages;
using WriterApp.Device.Shared.Services;
using WriterApp.Device.Shared.Storage;
using WriterApp.Shared;
using Xunit;

namespace WriterApp.Tests;

public sealed partial class StyleQualityWorkflowTests
{
    [Theory]
    [InlineData("empty")]
    [InlineData("edits")]
    [InlineData("error")]
    [InlineData("not-ready")]
    [InlineData("busy")]
    public async Task StyleButtonShowsItsOutcomeBeforeLocalChecksWithoutChangingWriting(string outcome)
    {
        string dir = Path.Combine(Path.GetTempPath(), "WriterApp.StyleFeedback", Guid.NewGuid().ToString("N"));
        try
        {
            var components = new CapturedWorkspace();
            var api = new FeedbackApi(outcome);
            var account = new DeviceAccountService(new FeedbackIdentity());
            await account.SignInAsync();
            var registrations = new ServiceCollection().AddLogging().AddSingleton<IJSRuntime, NoJs>()
                .AddSingleton<NavigationManager, Navigation>().AddSingleton<IComponentActivator>(components);
            registrations.AddWriterAppDeviceCore(new("Test", new("https://test.invalid/")), dir);
            // Sync is offline and deterministic; the AI boundary has its own synthetic account and connection.
            registrations.AddSingleton(sp => {
                var offline = new DeviceConnectivity(); offline.SetOnline(false);
                return new DeviceSyncEngine(sp.GetRequiredService<FileLocalDocumentStore>(), sp.GetRequiredService<DeviceSyncJournal>(),
                    sp.GetRequiredService<IDeviceSyncApi>(), sp.GetRequiredService<DeviceAccountService>(), offline,
                    sp.GetRequiredService<LocalDocumentRepository>(), new("Test", new("https://test.invalid/")));
            });
            registrations.AddSingleton(new DeviceAiService(api, account, new DeviceConnectivity()));
            using var services = registrations.BuildServiceProvider();
            var repository = services.GetRequiredService<LocalDocumentRepository>();
            var doc = await repository.CreateImportedAsync("Draft", "<p>Original page</p>");
            doc = await services.GetRequiredService<FileLocalDocumentStore>().ApplySyncAsync(doc with {
                ServerDocumentId = Guid.NewGuid(), ServerVersion = "v1", SyncState = LocalSyncState.Synced,
                Sections = doc.Sections.Select(s => s with { ServerSectionId = Guid.NewGuid() }).ToArray()
            }, doc.LocalRevision, default);
            var page = doc.Sections[0].Pages[0];
            var editorJs = new FeedbackEditor(page.Content);
            await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
            var rendered = await renderer.Dispatcher.InvokeAsync(() => renderer.RenderComponentAsync<QualityWorkspace>(
                ParameterView.FromDictionary(new Dictionary<string, object?> {
                    [nameof(QualityWorkspace.Saved)] = doc, [nameof(QualityWorkspace.Running)] = outcome == "busy"
                })));
            var workspace = components.Workspace!;
            if (outcome is not ("not-ready" or "busy"))
            {
                var editor = (DeviceTextEditor)Field("_editor").GetValue(workspace)!;
                typeof(DeviceTextEditor).GetField("_editor", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(editor, editorJs);
                typeof(DeviceTextEditor).GetField("_module", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(editor, editorJs);
                var session = (LocalEditorSession)Field("_session").GetValue(workspace)!;
                Field("_autosave").SetValue(workspace, new LocalAutosaveCoordinator(session, services.GetRequiredService<LocalRecoveryStore>(), () => Task.CompletedTask));
            }
            else Field("_editor").SetValue(workspace, null);

            if (outcome != "busy") await renderer.Dispatcher.InvokeAsync(() => ((IHandleEvent)workspace).HandleEventAsync(
                new EventCallbackWorkItem((Func<Task>)(() => (Task)typeof(DocumentWorkspace)
                    .GetMethod("RunAiAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(workspace, [DeviceAiAction.StyleQuality])!)), null));

            string html = await renderer.Dispatcher.InvokeAsync(rendered.ToHtmlString);
            var parsed = new HtmlParser().ParseDocument(html);
            var feedback = parsed.QuerySelector(".workspace-ai-feedback")!;
            var button = parsed.QuerySelectorAll("button").Single(b => b.TextContent == "Suggest style & quality revision");
            Assert.Same(feedback, button.NextElementSibling);
            Assert.Equal("Local style and quality checks", feedback.NextElementSibling!.GetAttribute("aria-label"));
            Assert.Equal(page.Content, (await repository.LoadAsync(doc.DocumentId))!.Sections[0].Pages[0].Content);
            if (outcome == "busy")
            {
                Assert.True(button.HasAttribute("disabled"));
                Assert.Contains("Preparing an AI proposal", feedback.QuerySelector("[role=status]")!.TextContent);
                Assert.Contains("Cancel AI request", feedback.TextContent);
                Assert.Equal(0, api.Requests);
                return;
            }
            Assert.False((bool)Field("_aiBusy").GetValue(workspace)!);
            if (outcome is "empty" or "edits")
            {
                Assert.True(api.Requests == 1, feedback.TextContent + " Interop: " + string.Join(", ", editorJs.Calls));
                Assert.Contains("Review before applying", feedback.TextContent);
                var apply = feedback.QuerySelectorAll("button").Single(b => b.TextContent == "Apply style revision");
                Assert.Equal(outcome == "empty", apply.HasAttribute("disabled"));
                Assert.Equal(outcome == "empty" ? 0 : 1, editorJs.Previews);
                Assert.Contains(outcome == "empty" ? "No changes suggested" : "Clearer writing", feedback.TextContent);
                Assert.Single(await services.GetRequiredService<LocalAiStore>().HistoryAsync(doc.DocumentId));
            }
            else
            {
                Assert.Contains(outcome == "not-ready" ? "editor is not ready" : "Synthetic service failure", feedback.QuerySelector("[role=alert]")!.TextContent);
                Assert.Null(feedback.QuerySelector(".workspace-ai-preview"));
            }
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    private static FieldInfo Field(string name) => typeof(DocumentWorkspace).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
    private sealed class FeedbackIdentity : IDeviceIdentityClient
    {
        public bool IsConfigured => true;
        public Task<DeviceAccessToken?> AcquireAsync(bool interactive, CancellationToken ct) => Task.FromResult<DeviceAccessToken?>(new("synthetic", DateTimeOffset.UtcNow.AddHours(1), "Writer", "writer"));
        public Task SignOutAsync() => Task.CompletedTask;
    }
    private sealed class FeedbackApi(string outcome) : IDeviceAiApi
    {
        public int Requests;
        public Task<WritingAvailability> GetWritingAvailabilityAsync(CancellationToken ct) => Task.FromResult(new WritingAvailability([], false, SavedOutlineContext: true));
        public Task<AiUsageStatusDto> GetUsageAsync(CancellationToken ct) => Task.FromResult(new AiUsageStatusDto {
            AiEnabled = true, UiEnabled = true, QuotaRemaining = 10, SupportsDocumentVersionChecks = true, SupportsStyleQualityReview = true
        });
        public Task<AiActionExecuteResponseDto> ExecuteAsync(string key, AiActionExecuteRequestDto request, CancellationToken ct)
        {
            Requests++;
            if (outcome == "error") throw new DeviceAiException(DeviceAiFailure.Server, "Synthetic service failure. Your writing is unchanged.");
            var report = new StyleQualityReport(outcome == "empty" ? [] : [new("Original page", "Clearer writing", "clarity", "preference", "Improve clarity.", "None")]);
            return Task.FromResult(new AiActionExecuteResponseDto(Guid.NewGuid(), request.OriginalText, System.Text.Json.JsonSerializer.Serialize(report), null,
                DateTimeOffset.UtcNow, key, SourceDocumentVersion: request.ExpectedDocumentVersion, SourceOutlineFingerprint: request.WritingOutline?.Fingerprint));
        }
    }
    private sealed class FeedbackEditor(string html) : IJSObjectReference
    {
        public int Previews;
        public List<string> Calls = [];
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => InvokeAsync<TValue>(identifier, default, args);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken ct, object?[]? args)
        {
            Calls.Add(identifier);
            if (identifier == "captureAi") return ValueTask.FromResult((TValue)(object)new AiEditorSnapshot(html, "Original page", "Original page", 0, 13, 1, 14, 0));
            if (identifier == "snapshot") return ValueTask.FromResult((TValue)Activator.CreateInstance(typeof(TValue), html, 0L)!);
            if (identifier == "previewStyleQualityRevision") { Previews++; return ValueTask.FromResult((TValue)(object)html); }
            return ValueTask.FromResult(default(TValue)!);
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
