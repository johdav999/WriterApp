using System.Net;
using System.Net.Http.Json;
using WriterApp.Application.Documents;
using WriterApp.Shared.Sync;
using WriterApp.Application.AI;
using WriterApp.Application.Usage;
using WriterApp.Controllers;
using WriterApp.Device.Shared.Components;
using WriterApp.Device.Shared.Services;
using WriterApp.Device.Shared.Storage;
using Xunit;

namespace WriterApp.Tests;

public sealed class AdvancedDeviceAiTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "WriterApp.AdvancedAi", Guid.NewGuid().ToString("N"));
    private async Task<LocalDocument> Book()
    {
        var store = new FileLocalDocumentStore(_root);
        var doc = await store.CreateProjectAsync("Book");
        doc = await store.SaveAsync(doc with
        {
            Sections = doc.Sections.Select(s => s with
            {
                Pages = s.Pages.Select(p => p with { Content = "<p>Alpha beta</p>" }).ToArray()
            }).ToArray()
        });
        return doc with { ServerDocumentId = Guid.NewGuid(), ServerVersion = "v1", SyncState = LocalSyncState.Synced };
    }
    [Theory]
    [InlineData(AdvancedAiAction.Consistency, "continuity.check_section", LocalAiTarget.Analysis)]
    [InlineData(AdvancedAiAction.Custom, "custom_transform", LocalAiTarget.Analysis)]
    [InlineData(AdvancedAiAction.SceneCard, "scene.suggest", LocalAiTarget.SceneCard)]
    [InlineData(AdvancedAiAction.Synopsis, "synopsis.story_coach", LocalAiTarget.SynopsisField)]
    [InlineData(AdvancedAiAction.Storyboard, "storyboard.check-subplot-continuity", LocalAiTarget.Analysis)]
    public async Task RequestsPinRevisionAndTypedTarget(AdvancedAiAction action, string key, LocalAiTarget target)
    {
        var doc = await Book();
        var prepared = AdvancedAiRequests.Build(doc, doc.Sections[0].SectionId, action, instruction: "Analyze pacing");
        Assert.Equal(key, prepared.Request.Key); Assert.Equal(target, prepared.Target);
        Assert.Equal("v1", prepared.Request.Request.ExpectedDocumentVersion);
        Assert.Equal("Alpha beta", prepared.Request.Request.SurroundingText);
        Assert.Throws<DeviceAiException>(() => prepared.Request.RequireManuscriptTarget());
        Assert.Throws<InvalidOperationException>(() => AdvancedAiRequests.RequireFresh(doc with { LocalRevision = doc.LocalRevision - 1 }, prepared));
        Assert.Throws<InvalidOperationException>(() => AdvancedAiRequests.RequireFresh(doc with { ServerVersion = "v2" }, prepared));
        Assert.Throws<InvalidOperationException>(() => AdvancedAiRequests.RequireFresh(doc with { DeletedAtUtc = DateTimeOffset.UtcNow }, prepared));
        if (target == LocalAiTarget.Analysis)
            Assert.Throws<InvalidDataException>(() => AdvancedAiRequests.Apply(doc, prepared, Proposal(prepared.Request)));
    }
    [Fact]
    public async Task TranslationRequiresSelectionAndLanguageAndCannotBecomePlanning()
    {
        var doc = await Book(); var section = doc.Sections[0]; var page = section.Pages[0];
        var capture = new AiEditorSnapshot(page.Content, "Alpha beta", "beta", 6, 10, 7, 11, 0);
        var p = DeviceAiRequests.Build(doc, section, page, capture, DeviceAiAction.Translate, "Swedish");
        Assert.Equal("translate.selection", p.Key); Assert.Equal(DeviceAiApplyTarget.TranslationSelection, p.Target);
        Assert.Equal("Swedish", p.Request.Parameters!["target_language"]); p.RequireManuscriptTarget();
        Assert.Throws<DeviceAiException>(() => (p with { Key = "scene.suggest" }).RequireManuscriptTarget());
        Assert.Throws<DeviceAiException>(() => DeviceAiRequests.Build(doc, section, page, capture with { SelectedText = "" }, DeviceAiAction.Translate, "Swedish"));
        Assert.Throws<DeviceAiException>(() => DeviceAiRequests.Build(doc, section, page, capture, DeviceAiAction.Translate, ""));
    }
    [Fact]
    public async Task PlanningApplyPreservesProseOtherFieldsAndRejectsMismatchedOrMissingTypedCard()
    {
        var doc = await Book();
        doc = LocalPlanning.Synopsis(doc, new(Logline: "Old", Stakes: "Keep stakes"));
        var p = AdvancedAiRequests.Build(doc, doc.Sections[0].SectionId, AdvancedAiAction.Synopsis);
        var changed = AdvancedAiRequests.Apply(doc, p, Proposal(p.Request));
        Assert.Equal("Proposed", changed.Project!.Synopsis!.Logline);
        Assert.Equal("Keep stakes", changed.Project.Synopsis.Stakes); Assert.Equal(doc.Sections, changed.Sections);
        var card = AdvancedAiRequests.Build(doc, doc.Sections[0].SectionId, AdvancedAiAction.SceneCard);
        Assert.Throws<InvalidDataException>(() => AdvancedAiRequests.Apply(doc, card, Proposal(card.Request)));
        Assert.Throws<InvalidDataException>(() => AdvancedAiRequests.Apply(doc, p, Proposal(card.Request)));
        changed = AdvancedAiRequests.Apply(doc, card, Proposal(card.Request) with { SceneCard = new("Purpose", "Beat", "Events", "Questions", Summary: "Summary") });
        Assert.Equal(doc.Sections, changed.Sections);
        Assert.Equal("Summary", changed.Project!.Nodes.Single(n => n.NodeType == "scene").Card!.Summary);
    }
    [Fact]
    public async Task HistoryAndPromptsSurviveRestartAndRecoveryKeepsPlanningInSeparateUnlinkedCopy()
    {
        var doc = LocalPlanning.Synopsis(await Book(), new(Logline: "Original intent"));
        var ai = new LocalAiStore(Path.Combine(_root, "ai"));
        var prompt = await ai.SavePromptAsync("Pacing", "Analyze pacing");
        var history = new LocalAiHistory(1, Guid.NewGuid(), doc.DocumentId, "synopsis.story_coach", "SynopsisField", doc.LocalRevision, doc.ServerVersion, DateTimeOffset.UtcNow, "Reviewed", doc, "New intent");
        await ai.SaveHistoryAsync(history);
        ai = new LocalAiStore(Path.Combine(_root, "ai"));
        Assert.Equal(prompt, Assert.Single(await ai.PromptsAsync()));
        var saved = Assert.Single(await ai.HistoryAsync(doc.DocumentId));
        Assert.Equal("Original intent", saved.Before.Project!.Synopsis!.Logline);
        await Assert.ThrowsAsync<InvalidDataException>(() => ai.SaveHistoryAsync(history with { Proposed = "Overwritten" }));
        var store = new FileLocalDocumentStore(_root);
        var copy = await store.RecoverSnapshotAsync(saved.Before);
        Assert.NotEqual(doc.DocumentId, copy.DocumentId); Assert.Null(copy.ServerDocumentId);
        Assert.Equal(LocalSyncState.LocalOnly, copy.SyncState); Assert.Null(copy.Project!.ServerProjectId);
        Assert.Equal(copy.DocumentId, copy.Project.ManuscriptId);
        Assert.Equal("Original intent", copy.Project.Synopsis!.Logline);
        Assert.Equal(doc.Sections[0].Pages[0].Content, copy.Sections[0].Pages[0].Content);
        Assert.NotEqual(doc.Project!.ProjectId, copy.Project.ProjectId);
        Assert.Equal("Original intent", (await store.GetAsync(copy.DocumentId))!.Project!.Synopsis!.Logline);
        string path = Path.Combine(_root, "ai", "history", doc.DocumentId.ToString("N"), history.Id.ToString("N") + ".json");
        var future = (await File.ReadAllTextAsync(path)).Replace("\"Version\":1", "\"Version\":99");
        await File.WriteAllTextAsync(path, future);
        await Assert.ThrowsAsync<InvalidDataException>(() => ai.SaveHistoryAsync(history));
        Assert.Equal(future, await File.ReadAllTextAsync(path));
    }
    [Fact]
    public async Task ServiceRejectsUnconfirmedRevisionAccountSwitchExpiryAndCanceledLateResponses()
    {
        var doc = await Book(); var p = AdvancedAiRequests.Build(doc, doc.Sections[0].SectionId, AdvancedAiAction.Consistency);
        var account = new DeviceAccountService(new Identity()); await account.SignInAsync();
        var api = new Api(); var service = new DeviceAiService(api, account, new());
        api.Version = null;
        Assert.Equal(DeviceAiFailure.Invalid, (await Assert.ThrowsAsync<DeviceAiException>(() => service.ProposeAsync(p.Request, default))).Kind);
        api.Version = "v1"; var proposal = await service.ProposeAsync(p.Request, default); service.RequireCurrentAccount(proposal);
        Assert.Throws<DeviceAiException>(() => service.RequireCurrentAccount(proposal with { PreparedAt = DateTimeOffset.UtcNow.AddHours(-1) }));
        await account.SignOutAsync(); Assert.Throws<DeviceAiException>(() => service.RequireCurrentAccount(proposal));
        await account.SignInAsync(); api.BeforeResponse = () => account.SignOutAsync();
        Assert.Equal(DeviceAiFailure.Authentication, (await Assert.ThrowsAsync<DeviceAiException>(() => service.ProposeAsync(p.Request, default))).Kind);
        await account.SignInAsync(); using var cancellation = new CancellationTokenSource();
        api.BeforeResponse = () => { cancellation.Cancel(); return Task.CompletedTask; };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.ProposeAsync(p.Request, cancellation.Token));
    }
    [Theory]
    [InlineData(AdvancedAiAction.Consistency)]
    [InlineData(AdvancedAiAction.SceneCard)]
    [InlineData(AdvancedAiAction.Synopsis)]
    [InlineData(AdvancedAiAction.Storyboard)]
    [InlineData(AdvancedAiAction.Custom)]
    public async Task EveryAdvancedActionPreservesSourceOnBackendStaleError(AdvancedAiAction action)
    {
        var doc = await Book(); var p = AdvancedAiRequests.Build(doc, doc.Sections[0].SectionId, action, instruction: "Analyze");
        using var http = new HttpClient(new ErrorHandler()) { BaseAddress = new("https://test.invalid/") };
        var error = await Assert.ThrowsAsync<DeviceAiException>(() => new DeviceAiApi(http).ExecuteAsync(p.Request.Key, p.Request.Request, default));
        Assert.Equal(DeviceAiFailure.Invalid, error.Kind); Assert.Contains("Synchronize", error.Message);
        Assert.Equal("<p>Alpha beta</p>", doc.Sections[0].Pages[0].Content);
    }
    private static DeviceAiProposal Proposal(DeviceAiPrepared p) => new(p, "Proposed", null, "Original", new());
    [Theory]
    [InlineData("scene.suggest")]
    [InlineData("scene.refine")]
    public async Task JsonOnlySceneResponseUpdatesPlanningAndPreservesAuthoredMetadata(string key)
    {
        var doc = await Book();
        var store = new FileLocalDocumentStore(_root);
        doc = await store.ApplySyncAsync(doc, doc.LocalRevision, default, projects: true);
        var node = doc.Project!.Nodes.Single(n => n.NodeType == "scene");
        var original = new SyncSceneCard("Existing purpose", "Decision", "Existing intent", "Old beat", "Old events", "Old questions", "Keep summary", "Final",
            "Authored POV", "Authored place", "Authored event", "Morning", "[\"Keep\"]", "[\"Mystery\"]", "[{\"kind\":\"object\",\"targetId\":\"keep\"}]");
        doc = LocalPlanning.Scene(doc, node.NodeId, original, "Private notes");
        var prepared = AdvancedAiRequests.Build(doc, doc.Sections[0].SectionId, AdvancedAiAction.SceneCard);
        prepared = prepared with { Request = prepared.Request with { Key = key } };
        // Mirrors the reported provider response: no summary or typed proposal, plus suggested links.
        const string json = """
            {"narrativeRole":"Setup","narrativeIntent":"Introduce Elin's return to Vinterhamn.","emotionalBeat":"Nostalgia and unease.","keyEvents":"Elin arrives; Jonas takes her suitcase.","openQuestions":"Why did Elin leave?","povCharacterId":"Elin","placeId":"Vinterhamn Station","timelineEventId":"arrival_at_vinterhamn","timeRef":"after sunset","tags":["arrival"],"subplotTags":["New"],"status":"Draft","references":[{"kind":"character","targetId":"Jonas"}]}
            """;
        var account = new DeviceAccountService(new Identity()); await account.SignInAsync();
        var service = new DeviceAiService(new Api { Proposed = json }, account, new());
        var proposal = await service.ProposeAsync(prepared.Request, default);
        Assert.NotNull(proposal.SceneCard);
        var changed = AdvancedAiRequests.Apply(doc, prepared, proposal with { SceneCard = null });
        var card = changed.Project!.Nodes.Single(n => n.NodeId == node.NodeId).Card!;
        Assert.Equal(original with { NarrativePurpose = "Setup", NarrativeRole = "Setup", NarrativeIntent = "Introduce Elin's return to Vinterhamn.",
            EmotionalBeat = "Nostalgia and unease.", KeyEvents = "Elin arrives; Jonas takes her suitcase.", OpenQuestions = "Why did Elin leave?" }, card);
        Assert.Equal("Private notes", changed.Project.Nodes.Single(n => n.NodeId == node.NodeId).Notes);
        Assert.Equal(doc.Sections, changed.Sections);
        await store.SaveAsync(changed);
        Assert.Equal(card, (await new FileLocalDocumentStore(_root).GetAsync(doc.DocumentId))!.Project!.Nodes.Single(n => n.NodeId == node.NodeId).Card);
        var data = new LocalStoryboardData(new LocalDocumentRepository(store), service);
        await data.LoadAsync(doc.Project!.ProjectId);
        Assert.Contains("Setup", data.Tree(doc.Project.ProjectId).Nodes.Single(n => n.Id == node.NodeId).MetadataJson);
        Assert.Equal("Nostalgia and unease.", (await data.GetSceneCardAsync(doc.Project.ProjectId, node.NodeId))!.EmotionalBeat);
    }
    [Fact]
    public async Task StyleQualityCannotUseTheOldAnalysisOnlyPlanningRoute()
    {
        var doc = await Book();
        var error = Assert.Throws<InvalidDataException>(() => AdvancedAiRequests.Build(doc, doc.Sections[0].SectionId, AdvancedAiAction.StyleQuality));
        Assert.Contains("preview and apply", error.Message);
        Assert.Equal("<p>Alpha beta</p>", doc.Sections[0].Pages[0].Content);
    }
    [Fact]
    public async Task PromptCopiesUseExistingAuthorizedContractAndNeverOverwriteLocalDefinitions()
    {
        var account = new DeviceAccountService(new Identity()); await account.SignInAsync();
        var store = new LocalAiStore(Path.Combine(_root, "prompts"));
        var local = await store.SavePromptAsync("Pacing", "Analyze pacing");
        var cloud = new PromptPresetDto(Guid.NewGuid(), null, "Cloud", "Test", "custom", null, "Analyze clarity", new(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        int posts = 0;
        using var http = new HttpClient(new Handler(async request =>
        {
            Assert.Equal("/api/ai/presets", request.RequestUri!.AbsolutePath);
            if (request.Method == HttpMethod.Post)
            {
                posts++; var body = await request.Content!.ReadFromJsonAsync<UpsertPromptPresetRequest>();
                Assert.Equal(local.Instruction, body!.TemplateText); Assert.Null(body.ProjectId); Assert.Equal("custom", body.Kind);
                return new(HttpStatusCode.Created) { Content = JsonContent.Create(cloud) };
            }
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new[] { cloud, cloud with { Id = Guid.NewGuid(), Kind = "builtin" } }) };
        }))
        { BaseAddress = new("https://test.invalid/") };
        var library = new DevicePromptLibrary(http, account, store);
        Assert.Single(await library.ListAsync(default)); await library.UploadCopyAsync(local, default);
        Assert.Equal(1, posts); await library.ImportCopyAsync(cloud.Id, default);
        Assert.Equal(2, (await store.PromptsAsync()).Count);
        Assert.Contains(await store.PromptsAsync(), p => p.Id == local.Id && p.Instruction == local.Instruction);
    }
    [Fact]
    public async Task InterruptedPromptUploadIsNotRetriedAndAccountSwitchDiscardsCloudList()
    {
        var account = new DeviceAccountService(new Identity()); await account.SignInAsync();
        var store = new LocalAiStore(Path.Combine(_root, "prompts")); var prompt = await store.SavePromptAsync("Keep", "Original");
        int posts = 0;
        using var http = new HttpClient(new Handler(async request =>
        {
            if (request.Method == HttpMethod.Post) { posts++; throw new HttpRequestException("Interrupted"); }
            await account.SignOutAsync(); return new(HttpStatusCode.OK) { Content = JsonContent.Create(Array.Empty<PromptPresetDto>()) };
        }))
        { BaseAddress = new("https://test.invalid/") };
        var library = new DevicePromptLibrary(http, account, store);
        await Assert.ThrowsAsync<HttpRequestException>(() => library.UploadCopyAsync(prompt, default)); Assert.Equal(1, posts);
        await Assert.ThrowsAsync<DeviceAiException>(() => library.ListAsync(default));
        Assert.Equal(prompt, Assert.Single(await store.PromptsAsync()));
    }
    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handle) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => handle(request); }
    private sealed class Identity : IDeviceIdentityClient
    {
        public bool IsConfigured => true;
        public Task<DeviceAccessToken?> AcquireAsync(bool interactive, CancellationToken cancellationToken) => Task.FromResult<DeviceAccessToken?>(new("test", DateTimeOffset.UtcNow.AddHours(1), "Tester"));
        public Task SignOutAsync() => Task.CompletedTask;
    }
    private sealed class Api : IDeviceAiApi
    {
        public string? Version = "v1"; public string Proposed = "Proposed"; public Func<Task>? BeforeResponse;
        public Task<AiUsageStatusDto> GetUsageAsync(CancellationToken ct) => Task.FromResult(new AiUsageStatusDto { AiEnabled = true, UiEnabled = true, SupportsDocumentVersionChecks = true, QuotaRemaining = 10 });
        public async Task<AiActionExecuteResponseDto> ExecuteAsync(string key, AiActionExecuteRequestDto request, CancellationToken ct)
        { if (BeforeResponse is not null) await BeforeResponse(); return new(Guid.NewGuid(), "Original", Proposed, null, DateTimeOffset.UtcNow, key, SourceDocumentVersion: Version); }
    }
    private sealed class ErrorHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Conflict) { Content = JsonContent.Create(new { code = "ai.stale_source" }) });
    }
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
