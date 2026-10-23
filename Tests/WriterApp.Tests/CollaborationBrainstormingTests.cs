using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WriterApp.AI.Abstractions;
using WriterApp.Application.AI;
using WriterApp.Application.Collaboration;
using WriterApp.Application.Continuity;
using WriterApp.Application.Usage;
using WriterApp.AI.Core;
using WriterApp.Application.Security;
using WriterApp.Application.Subscriptions;
using WriterApp.Shared.Brainstorming;
using WriterApp.Shared.Collaboration;
using Xunit;

namespace WriterApp.Tests;

public sealed partial class CollaborationConversionTests
{
    internal static void BrainstormServices(IServiceCollection services, BrainstormingConversationTests.Provider provider, string actor)
    {
        services.AddSingleton<IBibleStore>(provider);
        services.AddSingleton<IAiRouter>(provider);
        // Exercise the production license/policy/quota path. Only the external provider is deterministic.
        services.AddMemoryCache(); services.AddHttpContextAccessor();
        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<UserEntitlementStore>();
        services.AddScoped<IUserEntitlementStore, LocalCollaborationEntitlements>();
        services.AddScoped<IPlanRepository, WriterApp.Data.Subscriptions.PlanRepository>();
        services.AddScoped<IEntitlementService, EntitlementService>();
        services.AddScoped<IAiUsagePolicy, AiUsagePolicy>();
        services.AddScoped<IAiQuotaService, AiQuotaService>();
        services.AddScoped<IUsageMeter, UsageMeter>();
        services.AddSingleton(new BrainstormingGenerationStops());
        services.AddSingleton<IOptions<WriterAiOptions>>(Options.Create(new WriterAiOptions { Enabled = true, Streaming = new() { Enabled = true } }));
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("author")]
    public async Task BrainstormUsesCommittedSharedWritingAndRetainsPrivateConversationThroughHttp(string actor)
    {
        await Convert(); await InviteMember("author", CollaborationRole.Editor);
        // Replace a committed generation while deliberately retaining the old legacy HTML.
        var current = await new WebAiSourceService(_db, "owner", Access).Capture(_document, null, null, null, default);
        var oldHtml = (await _db.Pages.AsNoTracking().SingleAsync(p => p.Id == _page)).Content;
        var sharedHtml = (await CollaborationProjectionService.Read(_db, "owner", _document, new(), default, Access)).Pages.Single(p => p.PageId == _page).Html;
        var section = (await _db.Pages.AsNoTracking().SingleAsync(p => p.Id == _page)).SectionId;
        var pageSource = await new WebAiSourceService(_db, "owner", Access).Capture(_document, section, _page, null, default);
        await new CollaborationCheckedEdits(_db, _codec, Access).Apply("owner", _document,
            new(Guid.NewGuid(), pageSource, sharedHtml, "<p>Fresh shared Åsa 日本語 🧭.</p>"), default);
        _db.ChangeTracker.Clear();
        var provider = new BrainstormingConversationTests.Provider();
        using var api = Api(true, configure: services => BrainstormServices(services, provider, actor));
        using var http = api.CreateClient(); http.DefaultRequestHeaders.Add("X-Test-Owner", actor);
        var route = $"/api/brainstorming/sources/{_document}";
        using var read = await http.GetAsync(route); Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        var story = (await read.Content.ReadFromJsonAsync<BrainstormingStorySnapshot>(BrainstormingValidation.Json))!;
        Assert.Equal("<p>Fresh shared Åsa 日本語 🧭.</p>", story.Pages.Single(p => p.Id == _page).Content);
        Assert.NotEqual(current.DocumentVersion, story.Outline.DocumentVersion);
        Assert.Equal(oldHtml, (await _db.Pages.AsNoTracking().SingleAsync(p => p.Id == _page)).Content);
        var scope = new BrainstormingScope(1, Guid.NewGuid(), _document, story.Outline.ProjectId, BrainstormingIdentitySpace.Server,
            BrainstormingValidation.Fingerprint(http.BaseAddress!.AbsoluteUri), BrainstormingValidation.Fingerprint(actor), null);
        var request = new BrainstormingServerContextRequest(1, scope, new(SectionId: section), [], [], 60_000);
        using var prepared = await http.PostAsJsonAsync("/api/brainstorming/conversation/context", request, BrainstormingValidation.Json);
        Assert.Equal(HttpStatusCode.OK, prepared.StatusCode);
        var context = (await prepared.Content.ReadFromJsonAsync<BrainstormingServerContext>(BrainstormingValidation.Json))!.Preparation;
        Assert.True(context.Context!.Policy!.Ready);
        Assert.Contains(context.Context.Sources, s => s.Included && s.Text.Contains("Fresh shared Åsa"));
        var session = BrainstormingPersistence.Empty(scope) with { Revision = 1, State = BrainstormingConversationState.Saved, Contexts = [context.Context], Targets = [context.Resolution.Target!] };
        using var saved = await http.PostAsJsonAsync("/api/brainstorming/sessions", new BrainstormingWrite(1, Guid.NewGuid(), 0, session), BrainstormingValidation.Json);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        using var recovery = await http.PostAsJsonAsync($"/api/brainstorming/sessions/{scope.SessionId}/recovery?documentId={_document}", (BrainstormingRecoveryCopy?)null, BrainstormingValidation.Json);
        Assert.Equal(HttpStatusCode.OK, recovery.StatusCode);
        var send = new BrainstormingSend(1, scope, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1,
            context.Context.Id, context.Resolution.Target!.Id, null, "Explore three possible next scenes.");
        using var generation = await http.PostAsJsonAsync("/api/brainstorming/conversation/stream", send, BrainstormingValidation.Json);
        Assert.Equal(HttpStatusCode.OK, generation.StatusCode);
        var events = (await generation.Content.ReadAsStringAsync()).Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => System.Text.Json.JsonSerializer.Deserialize<BrainstormingConversationEvent>(line, BrainstormingValidation.Json)!).ToArray();
        Assert.True(events[^1].Kind==BrainstormingEventKind.Completed,BrainstormingPersistence.Json(events[^1])); Assert.Equal(1, provider.Calls);
        Assert.Single(await _db.UsageEvents.Where(e => e.UserId == actor).ToArrayAsync());
        var charged = await _db.UserEntitlements.AsNoTracking().SingleAsync(e => e.UserId == actor);
        Assert.Equal("Free", charged.PlanKey); Assert.Equal(0, charged.AiMonthlyTokenBudget);
        Assert.Equal(30, charged.AiTokensUsedThisPeriod);
        var retained = await http.GetFromJsonAsync<BrainstormingSession>($"/api/brainstorming/sessions/{scope.SessionId}?documentId={_document}", BrainstormingValidation.Json);
        Assert.Equal(2, retained!.Turns.Count);
        http.DefaultRequestHeaders.Remove("X-Test-Owner"); http.DefaultRequestHeaders.Add("X-Test-Owner", actor == "owner" ? "author" : "owner");
        Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync($"/api/brainstorming/sessions/{scope.SessionId}?documentId={_document}")).StatusCode);
        Assert.Empty(await _db.AiActionHistoryEntries.ToArrayAsync());
        Assert.Equal("<p>Fresh shared Åsa 日本語 🧭.</p>", (await CollaborationProjectionService.Read(_db, "owner", _document, new(), default, Access)).Pages.Single(p => p.PageId == _page).Html);
    }

    [Fact]
    public async Task BrainstormRejectsPendingProjectionViewerRevocationAndPrivateSiblings()
    {
        var support = await SupportingFixtures(); await Convert(); await InviteMember("author", CollaborationRole.Editor);
        using var api = Api(true); using var http = api.CreateClient(); http.DefaultRequestHeaders.Add("X-Test-Owner", "author");
        string route = $"/api/brainstorming/sources/{_document}";
        Assert.Equal(HttpStatusCode.OK, (await http.GetAsync(route)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync($"/api/brainstorming/sources/{support.Private}")).StatusCode);
        var page = await _db.CollaborationPages.SingleAsync(p => p.PageId == _page && !p.IsDeleted);
        page.CommittedSequence++; await _db.SaveChangesAsync();
        var pending = await http.GetAsync(route); Assert.Equal(HttpStatusCode.Conflict, pending.StatusCode);
        Assert.Contains("collaboration_projection_pending", await pending.Content.ReadAsStringAsync());
        page.CommittedSequence--; await _db.SaveChangesAsync();
        var manifest = await _db.CollaborationManifests.AsNoTracking().SingleAsync(m => m.DocumentId == _document);
        var removed = await _db.CollaborationPages.AsNoTracking().SingleAsync(p => p.PageId == _other && !p.IsDeleted);
        await CollaborationProjectionService.DeletePage(_db, "owner", _document, new(Guid.NewGuid(), manifest.StructureRevision, _other, removed.Generation), default);
        var live = await http.GetFromJsonAsync<BrainstormingStorySnapshot>(route, BrainstormingValidation.Json);
        Assert.DoesNotContain(live!.Pages, p => p.Id == _other);
        await Sharing.Change("owner", _document, "author", new(1, CollaborationRole.Viewer, false), default);
        Assert.Equal(HttpStatusCode.Forbidden, (await http.GetAsync(route)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync($"/api/brainstorming/sessions?documentId={_document}")).StatusCode);
        await Sharing.Change("owner", _document, "author", new(2, CollaborationRole.Viewer, true), default);
        Assert.Equal(HttpStatusCode.Forbidden, (await http.GetAsync(route)).StatusCode);
        Assert.Empty(await _db.BrainstormingSessions.ToArrayAsync()); Assert.Empty(await _db.UsageEvents.ToArrayAsync());
    }

    [Fact]
    public async Task BrainstormRejectsChangedSharedTextBeforeProviderDispatchAndRetainedSessionsAfterRevocation()
    {
        await Convert(); await InviteMember("author", CollaborationRole.Editor);
        var provider = new BrainstormingConversationTests.Provider();
        const string backend = "https://fixture.test/";
        var source = await new BrainstormingStorySourceReader(_db, "author", Access).ReadAsync(_document);
        var scope = new BrainstormingScope(1, Guid.NewGuid(), _document, source.Outline.ProjectId, BrainstormingIdentitySpace.Server,
            BrainstormingValidation.Fingerprint(backend), BrainstormingValidation.Fingerprint("author"), null);
        var section = source.Pages.Single(p => p.Id == _page).SectionId;
        var sources = new BrainstormingGenerationContext(_db, provider, provider, "author", Access);
        var prepared = (await sources.PrepareAsync(new(1, scope, new(SectionId: section), [], [], 60_000), default)).Preparation;
        var sessions = new BrainstormingSessions(_db, "author", scope.BackendKey, Access);
        var session = BrainstormingPersistence.Empty(scope) with { Revision = 1, State = BrainstormingConversationState.Saved, Contexts = [prepared.Context!], Targets = [prepared.Resolution.Target!] };
        await sessions.WriteAsync(new(1, Guid.NewGuid(), 0, session), default);
        var checkedSource = await new WebAiSourceService(_db, "owner", Access).Capture(_document, section, _page, null, default);
        await new CollaborationCheckedEdits(_db, _codec, Access).Apply("owner", _document,
            new(Guid.NewGuid(), checkedSource, source.Pages.Single(p => p.Id == _page).Content, "<p>Another collaborator changed this page.</p>"), default);
        var service = new BrainstormingConversationService(_db, provider, provider, provider, provider, Options.Create(new WriterAiOptions()),
            new(), sources, "author", scope.BackendKey, Access);
        var send = new BrainstormingSend(1, scope, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, prepared.Context!.Id,
            prepared.Resolution.Target!.Id, null, "Explore the next scene.");
        await Assert.ThrowsAsync<InvalidDataException>(async () => { await foreach (var item in service.SendAsync(send, default)) { } });
        Assert.Equal(0, provider.Calls); Assert.Empty(await _db.BrainstormingGenerations.ToArrayAsync());
        await Sharing.Change("owner", _document, "author", new(1, CollaborationRole.Editor, true), default);
        await Assert.ThrowsAsync<BrainstormingAccessException>(() => sessions.ReadAsync(_document, scope.SessionId, default));
        await Assert.ThrowsAsync<BrainstormingAccessException>(() => sessions.WriteAsync(new(1, Guid.NewGuid(), 1, session with { Revision = 2 }), default));
    }
}

public sealed class BrainstormingSharedTransportTests
{
    private sealed class SourceHandler(BrainstormingStorySnapshot source, HttpStatusCode status, string code) : HttpMessageHandler
    {
        public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(++Calls == 1
                ? new HttpResponseMessage(status) { Content = JsonContent.Create(new { code }) }
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(source, options: BrainstormingValidation.Json) });
    }
    [Theory]
    [InlineData(HttpStatusCode.Conflict, "collaboration_projection_pending", true)]
    [InlineData(HttpStatusCode.Conflict, "collaboration_source_changed", false)]
    [InlineData(HttpStatusCode.Forbidden, "collaboration_access_unavailable", false)]
    public async Task ContextWaitsOnlyForPendingSharedProjection(HttpStatusCode status, string code, bool retry)
    {
        var source = BrainstormingContextTests.Source(); var handler = new SourceHandler(source, status, code);
        using var http = new HttpClient(handler) { BaseAddress = new("https://fixture.test/") };
        var context = new WriterApp.Client.Services.WebBrainstormingContext(http, null!, null!, null!);
        if (retry) Assert.Equal(source.Stamp, (await context.ReadAsync(source.Outline.DocumentId)).Stamp);
        else await Assert.ThrowsAsync<InvalidOperationException>(() => context.ReadAsync(source.Outline.DocumentId));
        Assert.Equal(retry ? 2 : 1, handler.Calls);
    }
}
