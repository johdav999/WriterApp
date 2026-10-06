using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using WriterApp.Application.AI;
using WriterApp.Application.Documents;
using WriterApp.Client.Pages;
using WriterApp.Client.Services;
using WriterApp.Shared;
using Xunit;

#pragma warning disable BL0006 // Intentional minimal framework renderer exercises the production component handlers.

namespace WriterApp.Tests;

public sealed partial class DesktopAiWebPersistenceTests
{
    private const BindingFlags TranslationPrivate=BindingFlags.Instance|BindingFlags.NonPublic;
    private sealed class TranslationEditor : DocumentEditor, IDisposable {
        public int Reloads;
        public Func<Guid, Task>? ConsistencyEditorReady;
        public new void Dispose() { Http.Dispose();AiCommandStatusService.Dispose(); }
        protected override void OnInitialized() { }
        protected override Task OnInitializedAsync() => Task.CompletedTask;
        protected override Task OnParametersSetAsync() => Task.CompletedTask;
        protected override Task OnAfterRenderAsync(bool firstRender) => Task.CompletedTask;
        protected override void BuildRenderTree(RenderTreeBuilder builder) { }
        protected override Task ReloadTranslatedDocumentAsync() { Reloads++;return Task.CompletedTask; }
        protected override Task WaitForConsistencyPageEditorAsync(Guid pageId, CancellationToken ct) =>
            ConsistencyEditorReady is { } ready ? ready(pageId) : base.WaitForConsistencyPageEditorAsync(pageId, ct);
    }
    private sealed class TranslationRenderer(IServiceProvider services,ILoggerFactory logs) : Renderer(services,logs) {
        public override Dispatcher Dispatcher { get; }=Dispatcher.CreateDefault();
        protected override void HandleException(Exception e) => throw new InvalidOperationException("Component failed",e);
        protected override Task UpdateDisplayAsync(in RenderBatch renderBatch) => Task.CompletedTask;
        public Task Attach(IComponent component) => Dispatcher.InvokeAsync(() => RenderRootComponentAsync(AssignRootComponentId(component)));
    }
    private sealed class TranslationJs : IJSRuntime {
        private readonly HistoryMemoryModule _history=new();
        private readonly ConsistencyMemoryModule _consistency = new();
        public Func<Task>? AfterCapture;
        public Func<Task>? AfterPreview;
        public ValueTask<T> InvokeAsync<T>(string name,object?[]? args) => InvokeAsync<T>(name,default,args);
        public async ValueTask<T> InvokeAsync<T>(string name,CancellationToken ct,object?[]? args) {
            if(name=="import" && args?[0]?.ToString()=="./js/web-ai-history-outbox.js")return (T)(object)_history;
            if(name=="import" && args?[0]?.ToString()=="./_content/WriterApp.UI.Shared/consistency-decisions.js")return (T)(object)_consistency;
            object value=name switch {
                "tiptapEditor.captureTranslation" => new { runs=TranslationFixture.Runs((string)args![0]!) },
                "tiptapEditor.previewTranslation" => WebTranslationHtml.Map((string)args![0]!,new(Guid.Empty,TranslationFixture.Runs((string)args[0]!)),
                    new(Guid.Empty,(IReadOnlyList<TranslationRun>)args[2]!)),
                _ => throw new InvalidOperationException(name)
            };
            if(name=="tiptapEditor.captureTranslation" && AfterCapture is not null)await AfterCapture();
            if(name=="tiptapEditor.previewTranslation" && AfterPreview is not null)await AfterPreview();
            return JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value),new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        }
    }
    private sealed class ConsistencyMemoryModule : IJSObjectReference {
        private readonly HashSet<string> _values = [];
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public ValueTask<T> InvokeAsync<T>(string name, object?[]? args) => InvokeAsync<T>(name, default, args);
        public ValueTask<T> InvokeAsync<T>(string name, CancellationToken ct, object?[]? args) {
            ct.ThrowIfCancellationRequested(); string key = string.Join("|", args!.Take(3));
            if (name == "isIntentional") return ValueTask.FromResult((T)(object)_values.Contains(key));
            if (name == "setIntentional") { if ((bool)args![3]!) _values.Add(key); else _values.Remove(key); return ValueTask.FromResult(default(T)!); }
            throw new NotSupportedException(name);
        }
    }
    private sealed class TranslationNavigation : NavigationManager {
        public TranslationNavigation() => Initialize("http://localhost/","http://localhost/");
        protected override void NavigateToCore(string uri,bool forceLoad) => Uri=ToAbsoluteUri(uri).ToString();
    }
    private sealed class TranslationProvider(TranslationFixture f) : DelegatingHandler(f.Server.GetTestServer().CreateHandler()) {
        public int ProviderCalls,Approvals,Commits,Originals,LegacyReports;public string? Failure;
        public Func<Task>? AfterProvider;
        public Func<Task>? AfterOperations;
        public Func<Task>? AfterCommit;
        public AiActionExecuteRequestDto? Request;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct) {
            string path=request.RequestUri!.AbsolutePath;
            if(path.Contains("/history/") && path.EndsWith("/applied")) LegacyReports++;
            if(path.EndsWith("/approve"))Approvals++;
            if(path.EndsWith("/original"))Originals++;
            if(path.EndsWith("/commit")) {
                Commits++;
                if(Failure=="before-commit")return new(HttpStatusCode.ServiceUnavailable);
                if(Failure=="lost-ack") {
                    using var committed=await base.SendAsync(request,ct);Assert.Equal(HttpStatusCode.OK,committed.StatusCode);
                    return new(HttpStatusCode.ServiceUnavailable);
                }
            }
            if(!path.Contains("/api/ai/actions/translate.")) {
                var response=await base.SendAsync(request,ct);
                if(path.EndsWith("/approve") && Failure=="wrong-approval") {
                    var receipt=(await response.Content.ReadFromJsonAsync<WebTranslationReceipt>(ct))!;
                    response.Dispose();
                    return new(HttpStatusCode.OK){Content=JsonContent.Create(receipt with {OperationId=Guid.NewGuid()})};
                }
                if(path.EndsWith("/operations") && AfterOperations is not null)await AfterOperations();
                if(path.EndsWith("/commit") && AfterCommit is not null)await AfterCommit();
                return response;
            }
            ProviderCalls++;Request=(await request.Content!.ReadFromJsonAsync<AiActionExecuteRequestDto>())!;
            var source=JsonSerializer.Deserialize<WebTranslationSource>(Request.Parameters!["web_translation_source"]!.ToString()!,new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
            var original=TranslationStructures.Parse(Request.Parameters[TranslationStructures.Parameter]!.ToString()!);
            var translated=TranslationFixture.Translate(original);var proposal=Guid.NewGuid();
            await f.SaveProposal(source,original,translated,proposal,Request.WebSource);
            if(AfterProvider is not null)await AfterProvider();
            return new(HttpStatusCode.OK) { Content=JsonContent.Create(new AiActionExecuteResponseDto(proposal,TranslationStructures.Serialize(original),
                Failure=="malformed" ? "{}" : TranslationStructures.Serialize(translated),null,DateTimeOffset.UtcNow,"translate."+source.Scope,
                SourceDocumentVersion:Failure=="unchecked" ? null : source.DocumentVersion,WebSource:Failure=="unchecked" ? null : Request.WebSource)) };
        }
    }
    private static object? TranslationField(DocumentEditor editor,string name) => typeof(DocumentEditor).GetField(name,TranslationPrivate)!.GetValue(editor);
    private static void TranslationSet(DocumentEditor editor,string name,object? value) => typeof(DocumentEditor).GetField(name,TranslationPrivate)!.SetValue(editor,value);
    private static Task TranslationEvent(TranslationRenderer renderer,DocumentEditor editor,string name,params object?[] args) => renderer.Dispatcher.InvokeAsync(() =>
        (Task)typeof(DocumentEditor).GetMethod(name,TranslationPrivate)!.Invoke(editor,args)!);
    private static object TranslationAction(string scope) => Activator.CreateInstance(typeof(DocumentEditor).GetNestedType("AiActionOption",BindingFlags.NonPublic)!,
        ["translate."+scope,"Translate "+scope,"Translate",false,new Dictionary<string,object?>(),null,true,false,null])!;
    private static TranslationEditor CreateTranslationEditor(TranslationFixture f,TranslationProvider provider) {
        var editor=new TranslationEditor { Http=new HttpClient(provider){BaseAddress=new("http://localhost/")},
            JSRuntime=new TranslationJs(),AiCommandStatusService=new(),Navigation=new TranslationNavigation() };
        typeof(DocumentEditor).GetProperty(nameof(DocumentEditor.DocumentId))!.SetValue(editor,f.Document);
        editor.Http.DefaultRequestHeaders.Add("X-Test-Owner","ordinary-author");
        TranslationSet(editor,"_activeSection",new SectionDto(f.Sections[0],f.Document,"Section 1","Authored purpose",0,default,default,"sv",null));
        return editor;
    }
    [Theory]
    [InlineData("section","replace")][InlineData("document","replace")]
    [InlineData("section","duplicate-section")][InlineData("document","duplicate-document")]
    public async Task ProductionWebTranslationHandlersRequestReviewDismissAndApplyOnlyConfirmedPersistence(string scope,string mode) {
        await using var f=new TranslationFixture();await f.Start();using var provider=new TranslationProvider(f);
        var editor=CreateTranslationEditor(f,provider);
        using var services=new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer=new TranslationRenderer(services,services.GetRequiredService<ILoggerFactory>());await renderer.Attach(editor);
        await TranslationEvent(renderer,editor,"ExecuteStructuredTranslationAsync",TranslationAction(scope));
        Assert.Null(TranslationField(editor,"_webTranslationError"));Assert.NotNull(TranslationField(editor,"_webTranslationPreview"));
        Assert.Equal(0,provider.Approvals);Assert.Equal(0,provider.Commits);
        Assert.NotNull(provider.Request!.ExpectedDocumentVersion);Assert.Equal(f.Document,provider.Request.DocumentId);
        Assert.Equal(scope,TranslationStructures.Parse(provider.Request.Parameters![TranslationStructures.Parameter]!.ToString()!).Scope);
        await TranslationEvent(renderer,editor,"OnDiscardPendingAiProposal");Assert.Null(TranslationField(editor,"_webTranslationPreview"));
        await TranslationEvent(renderer,editor,"ExecuteStructuredTranslationAsync",TranslationAction(scope));
        TranslationSet(editor,"_translationApplyMode",mode);
        await TranslationEvent(renderer,editor,"OnApplyPendingAiProposal");
        Assert.Null(TranslationField(editor,"_webTranslationError"));Assert.Null(TranslationField(editor,"_pendingAiProposal"));
        Assert.Equal(1,provider.Approvals);Assert.Equal(1,provider.Commits);Assert.Equal(0,provider.LegacyReports);
        Assert.False((bool)TranslationField(editor,"_hasAiUndoHistory")!);
        Assert.Equal(mode=="duplicate-document" ? 0 : 1,editor.Reloads);
        Assert.Equal("Committed",Assert.Single((IEnumerable<WebTranslationReceipt>)TranslationField(editor,"_webTranslationReceipts")!).State);
    }
    [Theory]
    [InlineData("malformed")][InlineData("unchecked")][InlineData("cancelled")][InlineData("account")][InlineData("backend")][InlineData("stale")]
    public async Task ChangedOrInvalidWebTranslationResultsCannotCreateApproval(string failure) {
        await using var f=new TranslationFixture();await f.Start();using var provider=new TranslationProvider(f){Failure=failure};
        var editor=CreateTranslationEditor(f,provider);
        using var services=new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer=new TranslationRenderer(services,services.GetRequiredService<ILoggerFactory>());await renderer.Attach(editor);
        provider.AfterProvider=async () => {
            if(failure=="cancelled")await renderer.Dispatcher.InvokeAsync(() => typeof(DocumentEditor).GetMethod("CancelStructuredTranslation",TranslationPrivate)!.Invoke(editor,null));
            if(failure=="account")await renderer.Dispatcher.InvokeAsync(() => typeof(DocumentEditor).GetMethod("InvalidateStructuredTranslation",TranslationPrivate)!.Invoke(editor,null));
            if(failure=="backend")editor.Http=new HttpClient(provider,disposeHandler:false){BaseAddress=new("http://other-backend/")};
            if(failure=="stale")await f.Http.PutAsJsonAsync($"/api/pages/{f.Pages[0]}",new{content="<p>Later writing</p>"});
        };
        await TranslationEvent(renderer,editor,"ExecuteStructuredTranslationAsync",TranslationAction("document"));
        Assert.Null(TranslationField(editor,"_webTranslationPreview"));Assert.Equal(0,provider.Approvals);Assert.Equal(0,provider.Commits);
        Assert.Equal(0,provider.LegacyReports);
        Assert.Empty((await f.Http.GetFromJsonAsync<WebTranslationReceipt[]>(f.Endpoint+"/operations"))!);
    }
    [Theory]
    [InlineData("before-commit")][InlineData("lost-ack")]
    public async Task WebHandlerFailedSaveNeverReportsAppliedAndReloadReconcilesTheDurableOperation(string failure) {
        await using var f=new TranslationFixture();await f.Start();using var provider=new TranslationProvider(f){Failure=failure};
        var editor=CreateTranslationEditor(f,provider);
        using var services=new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer=new TranslationRenderer(services,services.GetRequiredService<ILoggerFactory>());await renderer.Attach(editor);
        await TranslationEvent(renderer,editor,"ExecuteStructuredTranslationAsync",TranslationAction("document"));
        await TranslationEvent(renderer,editor,"OnApplyPendingAiProposal");
        Assert.NotNull(TranslationField(editor,"_pendingAiProposal"));Assert.NotNull(TranslationField(editor,"_webTranslationError"));
        Assert.NotNull(TranslationField(editor,"_webTranslationUncertainOperation"));Assert.Equal(0,provider.LegacyReports);
        var approved=Assert.Single((IEnumerable<WebTranslationReceipt>)TranslationField(editor,"_webTranslationReceipts")!);
        Assert.Equal("Approved",approved.State);
        provider.Failure=null;
        await TranslationEvent(renderer,editor,"RefreshTranslationRecoveryAsync");
        if(failure=="before-commit") {
            Assert.NotNull(TranslationField(editor,"_webTranslationUncertainOperation"));
            await TranslationEvent(renderer,editor,"ResumeTranslationAsync",approved);
        }
        Assert.Null(TranslationField(editor,"_pendingAiProposal"));Assert.Null(TranslationField(editor,"_webTranslationUncertainOperation"));
        Assert.Equal("Committed",Assert.Single((IEnumerable<WebTranslationReceipt>)TranslationField(editor,"_webTranslationReceipts")!).State);
        Assert.Equal(1,provider.Approvals);Assert.Equal(1,provider.ProviderCalls);Assert.Equal(0,provider.LegacyReports);
    }

    [Theory]
    [InlineData("cancelled")][InlineData("account")][InlineData("backend")][InlineData("section")]
    public async Task ChangedContextDuringEditorPreviewCannotRestoreTheTranslationProposal(string change) {
        await using var f=new TranslationFixture();await f.Start();using var provider=new TranslationProvider(f);
        var editor=CreateTranslationEditor(f,provider);
        using var services=new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer=new TranslationRenderer(services,services.GetRequiredService<ILoggerFactory>());await renderer.Attach(editor);
        ((TranslationJs)editor.JSRuntime).AfterPreview=async () => {
            if(change is "cancelled" or "account")await renderer.Dispatcher.InvokeAsync(() =>
                typeof(DocumentEditor).GetMethod(change=="cancelled" ? "CancelStructuredTranslation" : "InvalidateStructuredTranslation",TranslationPrivate)!.Invoke(editor,null));
            if(change=="backend")editor.Http=new HttpClient(provider,disposeHandler:false){BaseAddress=new("http://other-backend/")};
            if(change=="section")TranslationSet(editor,"_activeSection",new SectionDto(f.Sections[1],f.Document,"Section 2","",1,default,default,"sv",null));
        };
        await TranslationEvent(renderer,editor,"ExecuteStructuredTranslationAsync",TranslationAction("document"));
        Assert.Null(TranslationField(editor,"_webTranslationPreview"));
        Assert.Null(TranslationField(editor,"_pendingAiProposal"));
        Assert.Equal(0,provider.Approvals);Assert.Equal(0,provider.Commits);
    }

    [Fact]
    public async Task BackendSwitchDuringRecoveryCannotReconcileAnotherBackendsReceipt() {
        await using var f=new TranslationFixture();await f.Start();using var provider=new TranslationProvider(f){Failure="lost-ack"};
        var editor=CreateTranslationEditor(f,provider);
        using var services=new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer=new TranslationRenderer(services,services.GetRequiredService<ILoggerFactory>());await renderer.Attach(editor);
        await TranslationEvent(renderer,editor,"ExecuteStructuredTranslationAsync",TranslationAction("document"));
        await TranslationEvent(renderer,editor,"OnApplyPendingAiProposal");
        provider.AfterOperations=() => {
            editor.Http=new HttpClient(provider,disposeHandler:false){BaseAddress=new("http://other-backend/")};
            return Task.CompletedTask;
        };
        await TranslationEvent(renderer,editor,"RefreshTranslationRecoveryAsync");
        Assert.NotNull(TranslationField(editor,"_pendingAiProposal"));
        Assert.NotNull(TranslationField(editor,"_webTranslationUncertainOperation"));
        Assert.Equal(0,editor.Reloads);
        Assert.Equal("Approved",Assert.Single((IEnumerable<WebTranslationReceipt>)TranslationField(editor,"_webTranslationReceipts")!).State);
    }

    [Fact]
    public async Task BackendSwitchDuringCommitCannotReportAppliedInTheNewContext() {
        await using var f=new TranslationFixture();await f.Start();using var provider=new TranslationProvider(f);
        var editor=CreateTranslationEditor(f,provider);
        var originalClient=editor.Http;
        using var services=new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer=new TranslationRenderer(services,services.GetRequiredService<ILoggerFactory>());await renderer.Attach(editor);
        await TranslationEvent(renderer,editor,"ExecuteStructuredTranslationAsync",TranslationAction("document"));
        provider.AfterCommit=() => {
            editor.Http=new HttpClient(provider,disposeHandler:false){BaseAddress=new("http://other-backend/")};
            return Task.CompletedTask;
        };
        await TranslationEvent(renderer,editor,"OnApplyPendingAiProposal");
        Assert.NotNull(TranslationField(editor,"_pendingAiProposal"));
        Assert.Equal(0,editor.Reloads);Assert.Equal(0,provider.LegacyReports);
        Assert.Equal("Committed",Assert.Single((await f.Http.GetFromJsonAsync<WebTranslationReceipt[]>(f.Endpoint+"/operations"))!).State);
        Assert.Equal("Approved",Assert.Single((IEnumerable<WebTranslationReceipt>)TranslationField(editor,"_webTranslationReceipts")!).State);
        editor.Http=originalClient;
        await TranslationEvent(renderer,editor,"RefreshTranslationRecoveryAsync");
        Assert.Null(TranslationField(editor,"_pendingAiProposal"));Assert.Null(TranslationField(editor,"_webTranslationUncertainOperation"));
        Assert.Equal(1,editor.Reloads);Assert.Equal(1,provider.Commits);Assert.Equal(1,provider.ProviderCalls);
    }

    [Theory]
    [InlineData("ResumeTranslationAsync")][InlineData("RecoverTranslationOriginalAsync")]
    public async Task ReceiptActionsCannotSendAnOldApprovalToAnotherBackend(string handler) {
        await using var f=new TranslationFixture();await f.Start();using var provider=new TranslationProvider(f){Failure="before-commit"};
        var editor=CreateTranslationEditor(f,provider);
        using var services=new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer=new TranslationRenderer(services,services.GetRequiredService<ILoggerFactory>());await renderer.Attach(editor);
        await TranslationEvent(renderer,editor,"ExecuteStructuredTranslationAsync",TranslationAction("document"));
        await TranslationEvent(renderer,editor,"OnApplyPendingAiProposal");
        var receipt=Assert.Single((IEnumerable<WebTranslationReceipt>)TranslationField(editor,"_webTranslationReceipts")!);
        editor.Http=new HttpClient(provider,disposeHandler:false){BaseAddress=new("http://other-backend/")};
        editor.Http.DefaultRequestHeaders.Add("X-Test-Owner","ordinary-author");
        provider.Failure=null;
        await TranslationEvent(renderer,editor,handler,receipt);
        Assert.Equal(1,provider.Commits);Assert.Equal(0,provider.Originals);
        Assert.Contains("backend",(string)TranslationField(editor,"_webTranslationError")!,StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Approved",Assert.Single((await f.Http.GetFromJsonAsync<WebTranslationReceipt[]>(f.Endpoint+"/operations"))!).State);
    }

    [Fact]
    public async Task MismatchedApprovalReceiptCannotCommitOrReportAppliedAndTheRealApprovalRemainsRecoverable() {
        await using var f=new TranslationFixture();await f.Start();using var provider=new TranslationProvider(f){Failure="wrong-approval"};
        var editor=CreateTranslationEditor(f,provider);
        using var services=new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer=new TranslationRenderer(services,services.GetRequiredService<ILoggerFactory>());await renderer.Attach(editor);
        await TranslationEvent(renderer,editor,"ExecuteStructuredTranslationAsync",TranslationAction("document"));
        await TranslationEvent(renderer,editor,"OnApplyPendingAiProposal");
        Assert.NotNull(TranslationField(editor,"_pendingAiProposal"));Assert.NotNull(TranslationField(editor,"_webTranslationError"));
        Assert.Equal(0,provider.Commits);Assert.Equal(0,provider.LegacyReports);Assert.Equal(0,editor.Reloads);
        var approved=Assert.Single((await f.Http.GetFromJsonAsync<WebTranslationReceipt[]>(f.Endpoint+"/operations"))!);
        Assert.Equal("Approved",approved.State);
        provider.Failure=null;
        await TranslationEvent(renderer,editor,"RefreshTranslationRecoveryAsync");
        await TranslationEvent(renderer,editor,"ResumeTranslationAsync",approved);
        Assert.Null(TranslationField(editor,"_pendingAiProposal"));Assert.Equal(1,provider.Commits);
        Assert.Equal(1,provider.ProviderCalls);Assert.Equal(1,provider.Approvals);Assert.Equal(1,editor.Reloads);
    }

    [Theory]
    [InlineData("cancelled")][InlineData("backend")]
    public async Task ChangedContextDuringCaptureCannotIssueAProviderRequest(string change) {
        await using var f=new TranslationFixture();await f.Start();using var provider=new TranslationProvider(f);
        var editor=CreateTranslationEditor(f,provider);
        using var services=new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer=new TranslationRenderer(services,services.GetRequiredService<ILoggerFactory>());await renderer.Attach(editor);
        ((TranslationJs)editor.JSRuntime).AfterCapture=async () => {
            if(change=="cancelled")await renderer.Dispatcher.InvokeAsync(() =>
                typeof(DocumentEditor).GetMethod("CancelStructuredTranslation",TranslationPrivate)!.Invoke(editor,null));
            else editor.Http=new HttpClient(provider,disposeHandler:false){BaseAddress=new("http://other-backend/")};
        };
        await TranslationEvent(renderer,editor,"ExecuteStructuredTranslationAsync",TranslationAction("document"));
        Assert.Equal(0,provider.ProviderCalls);Assert.Equal(0,provider.Approvals);Assert.Equal(0,provider.Commits);
        Assert.Null(TranslationField(editor,"_pendingAiProposal"));
    }

    [Fact]
    public async Task RecoveryOnAnotherBackendCannotConsumeTheOriginalBackendsUncertainApproval() {
        await using var f=new TranslationFixture();await f.Start();using var provider=new TranslationProvider(f){Failure="lost-ack"};
        var editor=CreateTranslationEditor(f,provider);
        var originalClient=editor.Http;
        using var services=new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer=new TranslationRenderer(services,services.GetRequiredService<ILoggerFactory>());await renderer.Attach(editor);
        await TranslationEvent(renderer,editor,"ExecuteStructuredTranslationAsync",TranslationAction("document"));
        await TranslationEvent(renderer,editor,"OnApplyPendingAiProposal");
        editor.Http=new HttpClient(provider,disposeHandler:false){BaseAddress=new("http://other-backend/")};
        editor.Http.DefaultRequestHeaders.Add("X-Test-Owner","ordinary-author");
        // The same owned fixture is deliberately reachable at both URLs: identity equality is not backend authority.
        await TranslationEvent(renderer,editor,"RefreshTranslationRecoveryAsync");
        Assert.NotNull(TranslationField(editor,"_pendingAiProposal"));Assert.NotNull(TranslationField(editor,"_webTranslationUncertainOperation"));
        Assert.Equal(0,editor.Reloads);
        editor.Http=originalClient;
        await TranslationEvent(renderer,editor,"RefreshTranslationRecoveryAsync");
        Assert.Null(TranslationField(editor,"_pendingAiProposal"));Assert.Null(TranslationField(editor,"_webTranslationUncertainOperation"));
        Assert.Equal(1,editor.Reloads);Assert.Equal(1,provider.Commits);Assert.Equal(1,provider.ProviderCalls);
    }
}
