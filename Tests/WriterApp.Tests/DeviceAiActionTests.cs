using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using WriterApp.Application.AI;
using WriterApp.Application.Usage;
using WriterApp.Device.Shared.Components;
using WriterApp.Device.Shared.Services;
using WriterApp.Device.Shared.Storage;
using Xunit;

namespace WriterApp.Tests;

public sealed partial class DeviceAiActionTests
{
    private static readonly Guid DocumentId = Guid.NewGuid();
    private static readonly Guid SectionId = Guid.NewGuid();
    private static readonly Guid PageId = Guid.NewGuid();
    private static readonly AiEditorSnapshot Selected = new("<p>Alpha beta</p>", "Alpha beta", "beta", 6, 10, 7, 11, 0);
    private static (LocalDocument Document, LocalSection Section, LocalPage Page) Source()
    {
        var now = DateTimeOffset.UtcNow;
        var page = new LocalPage { PageId = PageId, Title = "One", OrderIndex = 0, Content = Selected.Html,
            ContentFormat = LocalContentFormat.Html, CreatedAtUtc = now, UpdatedAtUtc = now };
        var section = new LocalSection { SectionId = SectionId, Title = "First", OrderIndex = 0,
            CreatedAtUtc = now, UpdatedAtUtc = now, Pages = [page, page with { PageId = Guid.NewGuid(), Content = "<p>Second page</p>", OrderIndex = 1 }] };
        var doc = new LocalDocument { DocumentId = DocumentId, ServerDocumentId = Guid.NewGuid(), ServerVersion = "v1",
            Title = "Draft", CreatedAtUtc = now, UpdatedAtUtc = now, LocalRevision = 2,
            SyncState = LocalSyncState.Synced, Sections = [section] };
        return (doc, section, page);
    }
    [Theory]
    [InlineData(DeviceAiAction.Rewrite, "rewrite.selection", "replace")]
    [InlineData(DeviceAiAction.Expand, "expand.selection", "replace")]
    [InlineData(DeviceAiAction.Shorten, "tighten.selection", "replace")]
    [InlineData(DeviceAiAction.Summarize, "custom_transform", "append")]
    [InlineData(DeviceAiAction.Custom, "custom_transform", "replace")]
    public void FiveActionsRouteToExistingBackendWithExpectedTextAndScope(DeviceAiAction action, string key, string mode)
    {
        var (document, section, page) = Source();
        var prepared = DeviceAiRequests.Build(document, section, page, Selected, action, "Make it clearer");
        Assert.Equal(key, prepared.Key); Assert.Equal(mode, prepared.ApplyMode);
        Assert.Equal(document.ServerDocumentId, prepared.Request.DocumentId);
        Assert.Equal(SectionId, prepared.Request.SectionId);
        Assert.Equal(PageId, prepared.Request.PageId);
        Assert.Equal(mode == "replace" ? "beta" : null, prepared.Request.OriginalText);
        Assert.Equal(mode == "replace" ? 6 : null, prepared.Request.SelectionStart);
        Assert.Equal(mode == "replace" ? 10 : null, prepared.Request.SelectionEnd);
        Assert.Equal(mode == "replace" ? "Alpha beta" : "Alpha beta\n\nSecond page", prepared.Request.SurroundingText);
        if (action is DeviceAiAction.Custom or DeviceAiAction.Summarize)
        {
            Assert.Equal(action == DeviceAiAction.Summarize ? "section" : "selection", prepared.Request.Parameters!["scope"]);
            Assert.NotNull(prepared.Request.Parameters["template"]);
        }
    }
    [Fact]
    public void CustomInstructionRequiresMappedSectionReviewWhenNoSelectionAndRejectsUnsafePreconditions()
    {
        var (document, section, page) = Source();
        var noSelection = Selected with { SelectedText = "", From = 7, To = 7 };
        Assert.Equal(DeviceAiFailure.Invalid, Assert.Throws<DeviceAiException>(() =>
            DeviceAiRequests.Build(document, section, page, noSelection, DeviceAiAction.Custom, "Improve flow")).Kind);
        var custom = DeviceAiRequests.Build(document, section, page, Selected, DeviceAiAction.Custom, "Improve flow");
        Assert.Throws<DeviceAiException>(() => (custom with { ApplyMode = "append" }).RequireManuscriptTarget());
        Assert.Equal(DeviceAiFailure.Invalid, Assert.Throws<DeviceAiException>(() => DeviceAiRequests.Build(document, section, page, noSelection, DeviceAiAction.Rewrite)).Kind);
        Assert.Equal(DeviceAiFailure.Invalid, Assert.Throws<DeviceAiException>(() => DeviceAiRequests.Build(document, section, page, noSelection, DeviceAiAction.Custom, "  ")).Kind);
        Assert.Equal(DeviceAiFailure.Offline, Assert.Throws<DeviceAiException>(() => DeviceAiRequests.Build(document with { SyncState = LocalSyncState.PendingUpload }, section, page, Selected, DeviceAiAction.Summarize)).Kind);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StyleRevisionIsAnApplicableRewriteOfTheCapturedSelectionOrPage(bool wholePage)
    {
        var (document, section, page) = Source();
        var capture = wholePage ? Selected with { SelectedText = Selected.PlainText, SelectionStart = 0,
            SelectionEnd = Selected.PlainText.Length, From = 0, To = 12 } : Selected;
        var prepared = DeviceAiRequests.Build(document, section, page, capture, DeviceAiAction.StyleQuality);
        prepared.RequireManuscriptTarget();
        Assert.Equal("custom_transform", prepared.Key);
        Assert.Equal("replace", prepared.ApplyMode);
        Assert.Equal(wholePage ? DeviceAiApplyTarget.ManuscriptPage : DeviceAiApplyTarget.ManuscriptSelection, prepared.Target);
        Assert.Equal("selection", prepared.Request.Parameters!["scope"]);
        Assert.Contains("polish the author's existing style", prepared.Request.Parameters["template"]!.ToString());
        Assert.Equal(WriterApp.Shared.StyleQualityReview.DefaultGoal, prepared.Request.Parameters[WriterApp.Shared.StyleQualityReview.Parameter]);
        Assert.DoesNotContain("Do not rewrite", prepared.Request.Parameters["template"]!.ToString());
        Assert.Equal(capture.SelectedText, prepared.Request.OriginalText);
        Assert.Equal(capture.SelectionStart, prepared.Request.SelectionStart);
        Assert.Equal(capture.SelectionEnd, prepared.Request.SelectionEnd);
        Assert.Equal("Alpha beta", prepared.Request.SurroundingText);
        Assert.DoesNotContain("Second page", prepared.Request.SurroundingText);
        Assert.Equal("v1", prepared.Request.ExpectedDocumentVersion);
        Assert.Equal(page.PageId, prepared.LocalPageId);
        Assert.Throws<DeviceAiException>(() => (prepared with { ApplyMode = "append" }).RequireManuscriptTarget());
        Assert.Throws<DeviceAiException>(() => (prepared with { Key = "scene.suggest" }).RequireManuscriptTarget());
        Assert.Equal("<p>Alpha beta</p>", page.Content);
    }
    [Fact]
    public void StyleRevisionRequiresAnExplicitNonemptyCapturedRange()
    {
        var (document, section, page) = Source();
        Assert.Throws<DeviceAiException>(() => DeviceAiRequests.Build(document, section, page,
            Selected with { SelectedText = "", From = 7, To = 7 }, DeviceAiAction.StyleQuality));
    }
    [Fact]
    public void HtmlContextDecodesMarkupWhileLeavingLiteralTextInert()
    {
        var (_, section, page) = Source();
        Assert.Equal("One & two\nNext", DeviceAiRequests.PlainText(page with { Content = "<p>One &amp; two</p><p>Next</p>" }));
        Assert.Equal("Alpha beta\n\nSecond page", DeviceAiRequests.SectionText(section, page.PageId, Selected.PlainText));
        var unsafeSection = section with { Pages = [page, section.Pages[1] with { ContentFormat = LocalContentFormat.LegacyJson }] };
        Assert.Equal(DeviceAiFailure.Invalid,
            Assert.Throws<DeviceAiException>(() => DeviceAiRequests.SectionText(unsafeSection, page.PageId, Selected.PlainText)).Kind);
    }
    [Theory]
    [InlineData(401, "authentication_required", DeviceAiFailure.Authentication)]
    [InlineData(402, "entitlement_denied", DeviceAiFailure.Upgrade)]
    [InlineData(402, "AI_QUOTA_EXCEEDED", DeviceAiFailure.Quota)]
    [InlineData(429, "ai.quota_exceeded", DeviceAiFailure.Quota)]
    [InlineData(429, "ai.rate_limited", DeviceAiFailure.Server)]
    [InlineData(400, "ai.safety_blocked", DeviceAiFailure.Safety)]
    [InlineData(504, "ai.timeout", DeviceAiFailure.Timeout)]
    [InlineData(502, "ai.invalid_section_revision", DeviceAiFailure.Invalid)]
    public async Task BackendProblemCodesMapToDistinctUserStates(int status, string code, DeviceAiFailure expected)
    {
        var api = Api(request => new((HttpStatusCode)status)
        { Content = JsonContent.Create(new { status, code, detail = "Provider internals are not shown" }) });
        var error = await Assert.ThrowsAsync<DeviceAiException>(() => api.ExecuteAsync("rewrite.selection", new(null, null, null, null, null, null, null, null, null), default));
        Assert.Equal(expected, error.Kind);
        Assert.DoesNotContain("Provider internals", error.Message);
    }
    [Theory]
    [InlineData(502, "ai.invalid_section_revision", "incomplete or invalid section revision")]
    [InlineData(502, "ai.style_review_rejected", "could not be validated against your writing")]
    [InlineData(502, "ai.style_review_incomplete", "stopped before it was complete")]
    [InlineData(429, "ai.rate_limited", "too many requests")]
    [InlineData(503, "ai.misconfigured", "not configured for this backend")]
    public async Task ActionableBackendFailuresDoNotMasqueradeAsServiceOutages(int status, string code, string explanation)
    {
        var api = Api(_ => new((HttpStatusCode)status) {
            Content = JsonContent.Create(new { code, detail = "Untrusted provider internals", message = "Untrusted manuscript text" }) });
        var error = await Assert.ThrowsAsync<DeviceAiException>(() => api.ExecuteAsync("custom_transform",
            new(null, null, null, null, null, null, null, null, null), default));
        Assert.Contains(explanation, error.Message);
        Assert.Contains("writing is unchanged", error.Message);
        Assert.DoesNotContain("Untrusted", error.Message);
        Assert.DoesNotContain("AI service is unavailable", error.Message);
    }
    [Fact]
    public async Task TransportUsesAuthenticatedBackendRoutesAndSharedDtos()
    {
        var (document, section, page) = Source();
        var prepared = DeviceAiRequests.Build(document, section, page, Selected, DeviceAiAction.Rewrite);
        int calls = 0;
        var api = Api(request =>
        {
            calls++;
            if (calls == 1)
            {
                Assert.Equal("/api/ai/status", request.RequestUri!.AbsolutePath);
                return new(HttpStatusCode.OK) { Content = JsonContent.Create(Usage()) };
            }
            Assert.Equal("/api/ai/actions/rewrite.selection/execute", request.RequestUri!.AbsolutePath);
            var body = request.Content!.ReadFromJsonAsync<AiActionExecuteRequestDto>().GetAwaiter().GetResult()!;
            Assert.Equal("beta", body.OriginalText);
            Assert.Equal(6, body.SelectionStart);
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(Response(prepared.Key)) };
        });
        var auth = await SignedInAsync();
        var proposal = await new DeviceAiService(api, auth, new DeviceConnectivity()).ProposeAsync(prepared, default);
        Assert.Equal("new text", proposal.ProposedText);
        Assert.Equal("beta", proposal.SourceText);
        Assert.Equal(2, calls);
    }
    [Fact]
    public async Task CancellationAndOfflineKeepLocalWritingUntouched()
    {
        string root = Path.Combine(Path.GetTempPath(), "WriterApp_AiTests_" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new FileLocalDocumentStore(root); var local = await store.CreateAsync("Original");
            var (document, section, page) = Source();
            var prepared = DeviceAiRequests.Build(document, section, page, Selected, DeviceAiAction.Expand);
            var waiting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var api = new FakeApi { OnExecute = async ct => { waiting.SetResult(); await Task.Delay(Timeout.InfiniteTimeSpan, ct); return Response(prepared.Key); } };
            var network = new DeviceConnectivity(); var service = new DeviceAiService(api, await SignedInAsync(), network);
            using var canceled = new CancellationTokenSource();
            var pending = service.ProposeAsync(prepared, canceled.Token); await waiting.Task.WaitAsync(TimeSpan.FromSeconds(10)); canceled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
            Assert.Equal("", (await store.GetAsync(local.DocumentId))!.Sections[0].Pages[0].Content);
            network.SetOnline(false);
            Assert.Equal(DeviceAiFailure.Offline, (await Assert.ThrowsAsync<DeviceAiException>(() => service.ProposeAsync(prepared, default))).Kind);
            Assert.Equal("", (await store.GetAsync(local.DocumentId))!.Sections[0].Pages[0].Content);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
    [Theory]
    [InlineData(false, 500, DeviceAiFailure.Upgrade)]
    [InlineData(true, 0, DeviceAiFailure.Quota)]
    public async Task LocalUsageGateStopsRequestBeforeCharge(bool enabled, long remaining, DeviceAiFailure expected)
    {
        var api = new FakeApi { Usage = Usage(enabled, remaining) };
        var (document, section, page) = Source();
        var prepared = DeviceAiRequests.Build(document, section, page, Selected, DeviceAiAction.Shorten);
        var auth = await SignedInAsync();
        var error = await Assert.ThrowsAsync<DeviceAiException>(() => new DeviceAiService(api, auth, new()).ProposeAsync(prepared, default));
        Assert.Equal(expected, error.Kind); Assert.Equal(0, api.ExecuteCount);
    }
    [Fact]
    public async Task OlderBackendIsRejectedBeforeAnyBillableAiCall()
    {
        var api = new FakeApi { Usage = new() { AiEnabled=true,UiEnabled=true,QuotaRemaining=100 } };
        var (doc,section,page) = Source();
        var request = DeviceAiRequests.Build(doc,section,page,Selected,DeviceAiAction.Translate,"Swedish");
        var service = new DeviceAiService(api,await SignedInAsync(),new());
        var error = await Assert.ThrowsAsync<DeviceAiException>(()=>service.ProposeAsync(request,default));
        Assert.Equal(DeviceAiFailure.Invalid,error.Kind); Assert.Equal(0,api.ExecuteCount);
    }
    [Fact]
    public async Task SourceBackupSurvivesRestartAndFailedReplacementAttempt()
    {
        string root = Path.Combine(Path.GetTempPath(), "WriterApp_AiUndoTests_" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new DeviceAiUndoStore(root);
            var first = new DeviceAiUndoRecord(DocumentId, PageId, "<p>Original</p>", null, DateTimeOffset.UtcNow);
            await store.SaveBeforeAsync(first);
            Assert.Equal("<p>Original</p>", (await new DeviceAiUndoStore(root).LoadPendingAsync(DocumentId))!.BeforeHtml);
            await store.ConfirmAsync(first with { AfterHtml = "<p>Suggested</p>" });
            Assert.Null(await store.LoadPendingAsync(DocumentId));
            await store.SaveBeforeAsync(first with { BeforeHtml = "<p>Later attempt</p>" });
            var restarted = new DeviceAiUndoStore(root);
            var committed = await restarted.LoadAsync(DocumentId);
            Assert.Equal("<p>Original</p>", committed!.BeforeHtml);
            Assert.Equal("<p>Suggested</p>", committed.AfterHtml);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
    private static AiUsageStatusDto Usage(bool enabled = true, long remaining = 100) => new()
    { AiEnabled = enabled, UiEnabled = true, SupportsDocumentVersionChecks = true, SupportsStyleQualityReview = true, PlanKey = "standard", QuotaRemaining = remaining };
    private static AiActionExecuteResponseDto Response(string key) => new(Guid.NewGuid(), "beta", "new text", "Preview",
        DateTimeOffset.UtcNow, key, SourceDocumentVersion: "v1");
    private static DeviceAiApi Api(Func<HttpRequestMessage, HttpResponseMessage> respond) =>
        new(new HttpClient(new Handler(respond)) { BaseAddress = new Uri("https://test.invalid/") });
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(respond(request)); }
    private sealed class Identity : IDeviceIdentityClient
    {
        public bool IsConfigured => true;
        public Task<DeviceAccessToken?> AcquireAsync(bool interactive, CancellationToken cancellationToken) =>
            Task.FromResult<DeviceAccessToken?>(new("token", DateTimeOffset.UtcNow.AddHours(1), "Writer"));
        public Task SignOutAsync() => Task.CompletedTask;
    }
    private static async Task<DeviceAccountService> SignedInAsync()
    { var service = new DeviceAccountService(new Identity()); await service.SignInAsync(); return service; }
    private sealed class FakeApi : IDeviceAiApi
    {
        public AiUsageStatusDto Usage { get; set; } = DeviceAiActionTests.Usage();
        public Func<CancellationToken, Task<AiActionExecuteResponseDto>>? OnExecute { get; set; }
        public int ExecuteCount { get; private set; }
        public Task<AiUsageStatusDto> GetUsageAsync(CancellationToken ct) => Task.FromResult(Usage);
        public Task<AiActionExecuteResponseDto> ExecuteAsync(string key, AiActionExecuteRequestDto request, CancellationToken ct)
        { ExecuteCount++; return OnExecute is null ? Task.FromResult(Response(key)) : OnExecute(ct); }
    }
}
