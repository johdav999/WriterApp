using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using AngleSharp.Html.Parser;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using WriterApp.Application.AI;
using WriterApp.Application.Documents;
using WriterApp.Application.Security;
using WriterApp.Client.Components.Editor;
using WriterApp.Client.Pages;
using WriterApp.Client.Services;
using WriterApp.Controllers;
using WriterApp.Data;
using WriterApp.Device.Shared.Components;
using WriterApp.Device.Shared.Pages;
using WriterApp.Device.Shared.Services;
using WriterApp.Device.Shared.Storage;
using WriterApp.Shared;
using WriterApp.Shared.Brainstorming;
using Xunit;

namespace WriterApp.Tests;

public sealed class BrainstormingContextAdapterTests
{
    private static void Set(object value, string name, object? field) {
        var type = value.GetType(); var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        if (type.GetField(name, flags) is { } f) f.SetValue(value, field); else type.GetProperty(name, flags)!.SetValue(value, field);
    }
    private static Task<BrainstormingPreparation> Invoke(object value, string method, BrainstormingTargetQuery query, IReadOnlyList<BrainstormingContextChoice>? choices = null, IReadOnlyList<Guid>? supports = null) =>
        (Task<BrainstormingPreparation>)value.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(value, [query, choices ?? Array.Empty<BrainstormingContextChoice>(), supports ?? Array.Empty<Guid>(), 60_000])!;
    private sealed class ProjectionJs : IJSRuntime, IJSObjectReference
    {
        private readonly LocalBrainstormingStore store = new(Path.Combine(Path.GetTempPath(), "BrainstormingContextInterop", Guid.NewGuid().ToString()));
        private BrainstormingScope? scope; private string? account; public string? PendingHtml; public long SnapshotVersion = 1; public Func<Task>? DuringProjection;
        public ValueTask<T> InvokeAsync<T>(string name, object?[]? args) => InvokeAsync<T>(name, default, args);
        public async ValueTask<T> InvokeAsync<T>(string name, CancellationToken ct, object?[]? args) {
            ct.ThrowIfCancellationRequested(); args ??= []; object? result = null;
            switch (name) {
                case "import": result = this; break;
                case "snapshot": result = JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(new { html = PendingHtml, version = SnapshotVersion }), BrainstormingValidation.Json); break;
                case "tiptapEditor.brainstormingPlainText": case "qualityPlainText":
                    if (DuringProjection is { } action) await action();
                    result = (string)args[1]! == "LegacyText" ? args[0] : new HtmlParser().ParseDocument((string)args[0]!).Body!.TextContent; break;
                case "rememberIdentity": account = (string?)args[1]; break;
                case "reviewIdentity": result = account; break;
                case "isOffline": result = false; break;
                case "read": if (scope is not null) { var value = await store.ReadAsync(scope, ct); if (value is not null) result = BrainstormingPersistence.Json(value); } break;
                case "write":
                    var proposed = JsonSerializer.Deserialize<BrainstormingCache>((string)args[3]!, BrainstormingValidation.Json)!; scope = proposed.Session.Scope;
                    var saved = await store.WriteAsync(proposed, (long)args[4]!, (Guid)args[5]!, ct); result = JsonSerializer.SerializeToElement(new { conflict = false, json = BrainstormingPersistence.Json(saved) }); break;
                default: throw new InvalidOperationException("Unexpected interop fixture: " + name);
            }
            return result is null ? default! : (T)result;
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
    private sealed class Handler(Func<HttpRequestMessage,CancellationToken,Task<HttpResponseMessage>> send) : HttpMessageHandler { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => send(request, ct); }
    private static async Task AddVersion(BrainstormingPersistenceTests.Database f, string owner = "author") {
        await using var db = new AppDbContext(f.Options); var doc = await db.Documents.SingleAsync(); doc.OwnerUserId = owner; (await db.Projects.SingleAsync()).OwnerUserId = owner;
        var version = await db.DocumentSyncRecords.SingleOrDefaultAsync(); if (version is null) db.DocumentSyncRecords.Add(new() { DocumentId = f.Document, OwnerUserId = owner, Version = "v1" }); else version.OwnerUserId = owner;
        await db.SaveChangesAsync();
    }
    [Theory][InlineData("document-owner")][InlineData("project-owner")][InlineData("deleted")][InlineData("project-id")]
    public async Task ServerRefusesUnauthorizedDeletedAndContradictoryProjectSources(string failure)
    {
        await using var f = new BrainstormingPersistenceTests.Database(); await f.Start(); await AddVersion(f); await using var db = new AppDbContext(f.Options);
        if (failure == "document-owner") (await db.Documents.SingleAsync()).OwnerUserId = "other";
        if (failure == "project-owner") (await db.Projects.SingleAsync()).OwnerUserId = "other";
        if (failure == "deleted") (await db.Documents.SingleAsync()).DeletedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
        if (failure == "project-id") await Assert.ThrowsAsync<BrainstormingAccessException>(() => new BrainstormingStorySourceReader(db, "author").ReadAsync(f.Document, Guid.NewGuid()));
        else await Assert.ThrowsAsync<DocumentSyncException>(() => new BrainstormingStorySourceReader(db, "author").ReadAsync(f.Document));
        Assert.Empty(await db.BrainstormingSessions.ToListAsync()); Assert.Empty(await db.UsageEvents.ToListAsync());
    }
    [Fact]
    public async Task AuthenticatedProductionSourceRouteUsesActualSqliteMigratedOwnershipAndReturnsNoCanonBypass()
    {
        await using var f = new BrainstormingPersistenceTests.Database(); await f.Start(migrations: true); await AddVersion(f, "synthetic-owner");
        using var host = new HostBuilder().ConfigureWebHost(web => web.UseTestServer().ConfigureServices(services => {
            services.AddAuthentication("test").AddScheme<AuthenticationSchemeOptions, BrainstormingCapabilityTests.TestAuthentication>("test", _ => {});
            services.AddAuthorization(); services.AddSingleton<IUserIdResolver, UserIdResolver>(); services.AddScoped(_ => new AppDbContext(f.Options));
            services.AddControllers().AddApplicationPart(typeof(BrainstormingStorySourcesController).Assembly);
        }).Configure(app => { app.UseRouting(); app.UseAuthentication(); app.UseAuthorization(); app.UseEndpoints(e => e.MapControllers()); })).Build();
        await host.StartAsync(); using var http = host.GetTestClient(); var route = "api/brainstorming/sources/" + f.Document;
        Assert.Equal(HttpStatusCode.Unauthorized, (await http.GetAsync(route)).StatusCode); http.DefaultRequestHeaders.Add("X-Test-Identity", "synthetic-owner");
        using var response = await http.GetAsync(route); Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.True(response.Headers.CacheControl!.NoStore);
        var source = (await response.Content.ReadFromJsonAsync<BrainstormingStorySnapshot>(BrainstormingValidation.Json))!; BrainstormingStorySources.Validate(source);
        Assert.Null(source.Outline.ProjectId); Assert.Equal(f.Page, Assert.Single(source.Pages).Id);
        Assert.All(source.References.Where(r => r.Kind is BrainstormingSourceKind.Character or BrainstormingSourceKind.Place or BrainstormingSourceKind.Timeline), r => { Assert.Empty(r.Text); Assert.Equal(BrainstormingSourceAvailability.NotLoaded, r.Availability); });
        Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync(route + "?projectId=" + Guid.NewGuid())).StatusCode);
        var list = (await http.GetFromJsonAsync<BrainstormingSupportingPage>("api/brainstorming/sources?limit=1",BrainstormingValidation.Json))!; Assert.Equal(f.Document,Assert.Single(list.Items).Id);
        Assert.Equal(HttpStatusCode.BadRequest,(await http.GetAsync("api/brainstorming/sources?limit=51")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await http.GetAsync("api/brainstorming/sources?after="+Guid.NewGuid())).StatusCode);
        await using var db = new AppDbContext(f.Options); (await db.Documents.SingleAsync()).OwnerUserId = "other"; await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync(route)).StatusCode); Assert.Empty(await db.UsageEvents.ToListAsync()); Assert.Empty(await db.AiActionHistoryEntries.ToListAsync());
    }
    [Fact]
    public async Task ReachableWebEditorPreparationPreservesPendingHtmlAndPersistsExactContextWithRefresh()
    {
        await using var f = new BrainstormingPersistenceTests.Database(); await f.Start(); await AddVersion(f); var js = new ProjectionJs(); var calls = new List<string>();
        using var http = new HttpClient(new Handler(async (request,ct) => {
            Assert.Equal(HttpMethod.Get, request.Method); var route = request.RequestUri!.AbsolutePath; calls.Add(route);
            if (route == "/api/auth/me") return new(HttpStatusCode.OK) { Content = JsonContent.Create(new AuthMeDto { IsAuthenticated = true, UserId = "author" }) };
            await using var db = new AppDbContext(f.Options);
            if (route.StartsWith("/api/ai/actions/web-source/")) return new(HttpStatusCode.OK) { Content = JsonContent.Create(await new WebAiSourceService(db,"author").Capture(f.Document,null,null,null,ct)) };
            if (route.StartsWith("/api/brainstorming/sources/")) return new(HttpStatusCode.OK) { Content = JsonContent.Create(await new BrainstormingStorySourceReader(db,"author").ReadAsync(f.Document,ct:ct)) };
            return new(HttpStatusCode.NotFound);
        })) { BaseAddress = new("https://test.invalid/") };
        var auth = new AuthStateService(http, NullLogger<AuthStateService>.Instance, new(), new()); await using var cache = new WebBrainstormingStore(js);
        var sessions = new WebBrainstormingSessions(cache,http,auth); var context = new WebBrainstormingContext(http,new(http,auth),sessions,new(js));
        var page = new PageEditor(); Set(page,"_content","<p>Unsaved <strong>Åsa 🧭</strong>.</p>"); var editor = new DocumentEditor(); Set(editor,"DocumentId",f.Document); Set(editor,"SectionId",f.Section);
        Set(editor,"_pageEditor",page); Set(editor,"_activePage",new PageDto(f.Page,f.Document,f.Section,"Page","old",0,DateTimeOffset.UtcNow,DateTimeOffset.UtcNow)); Set(editor,"BrainstormContext",context); Set(editor,"BrainstormSessions",sessions);
        var result = await Invoke(editor,"PrepareBrainstormingContext",new(SectionId:f.Section)); Assert.Contains("Unsaved Åsa 🧭.",result.Context!.Sources.Single(s=>s.Kind==BrainstormingSourceKind.Manuscript).Text);
        Assert.Contains("Pending editor",result.Context.Policy!.Limitation); Assert.Throws<InvalidDataException>(()=>BrainstormingContextPolicy.GenerationData(result.Context));
        var first = (await sessions.ReadAsync(result.Context.Scope))!; await sessions.Sessions.SaveComposerAsync(first.Session.Scope,"Unsent composer");
        var refreshed = await Invoke(editor,"PrepareBrainstormingContext",new(SectionId:f.Section)); var retained = (await sessions.ReadAsync(refreshed.Context!.Scope))!;
        Assert.Equal(2,retained.Session.Contexts.Count); Assert.Equal("Unsent composer",retained.Composer); Assert.Equal("<p>Unsaved <strong>Åsa 🧭</strong>.</p>",await page.GetContentAsync());
        await using var verify = new AppDbContext(f.Options); Assert.DoesNotContain("Unsaved",(await verify.Pages.SingleAsync()).Content); Assert.Empty(await verify.UsageEvents.ToListAsync());
        js.DuringProjection = () => { Set(page,"_content","<p>Intervening edit</p>"); return Task.CompletedTask; };
        await Assert.ThrowsAsync<InvalidDataException>(()=>Invoke(editor,"PrepareBrainstormingContext",new(SectionId:f.Section)));
        Assert.Equal(2,(await sessions.ReadAsync(first.Session.Scope))!.Session.Contexts.Count); Assert.Contains("Intervening",await page.GetContentAsync());
        Assert.All(calls,c=>Assert.DoesNotContain("execute",c));
    }
    [Fact]
    public async Task ReachableDeviceEditorPreparationFlushesPendingDomWithoutSavingWritingAndWorksOffline()
    {
        string root = Path.Combine(Path.GetTempPath(),"BrainstormingDeviceContext",Guid.NewGuid().ToString()); var repository = new LocalDocumentRepository(new FileLocalDocumentStore(Path.Combine(root,"documents")));
        var doc = await repository.CreateAsync("Offline manuscript"); var session = new LocalEditorSession(repository,doc); var page = doc.Sections[0].Pages[0]; var js = new ProjectionJs { PendingHtml = "<p>New unsaved Åsa 🧭.</p>" };
        var account = new DeviceAccountService(new UnconfiguredDeviceIdentityClient()); var network = new DeviceConnectivity(); network.SetOnline(false); var options = new DeviceHostOptions("Windows fixture",new("https://test.invalid/"));
        using var http = new HttpClient(new Handler((_,_) => throw new Exception("Preparation must not send offline writing"))) { BaseAddress = options.ApiBaseAddress };
        var files = new LocalBrainstormingStore(Path.Combine(root,"sessions")); var sessions = new DeviceBrainstormingSessions(files,account,options,network,repository,http);
        var canon = new DeviceBibleService(new(http),new(Path.Combine(root,"canon")),repository,account,network,options); var context = new DeviceBrainstormingContext(repository,canon,account,sessions,new(js));
        var textEditor = new DeviceTextEditor(); Set(textEditor,"Content",page.Content); Set(textEditor,"Format",page.ContentFormat); Set(textEditor,"ContentChanged",EventCallback.Factory.Create<string>(new object(), html => session.Edit(page.PageId,html))); Set(textEditor,"_editor",js);
        var workspace = new DocumentWorkspace(); Set(workspace,"DocumentId",doc.DocumentId); Set(workspace,"_session",session); Set(workspace,"_editor",textEditor); Set(workspace,"_selectedPage",page.PageId); Set(workspace,"BrainstormContext",context); Set(workspace,"BrainstormSessions",sessions);
        var result = await Invoke(workspace,"PrepareBrainstormingContextAsync",new(SectionId:doc.Sections[0].SectionId));
        Assert.Equal("New unsaved Åsa 🧭.",result.Context!.Sources.Single(s=>s.Kind==BrainstormingSourceKind.Manuscript).Text); Assert.True(session.IsDirty);
        Assert.Equal(page.Content,(await repository.LoadAsync(doc.DocumentId))!.Sections[0].Pages[0].Content); Assert.Contains("No cloud text",result.Context.Policy!.Limitation);
        await sessions.Sessions.AppendUnsentAsync(result.Context.Scope,"Preserve discussion",Guid.NewGuid()); await sessions.Sessions.SaveComposerAsync(result.Context.Scope,"Still typing");
        var second = await Invoke(workspace,"PrepareBrainstormingContextAsync",new(SectionId:doc.Sections[0].SectionId)); var retained = (await new LocalBrainstormingStore(Path.Combine(root,"sessions")).ReadAsync(second.Context!.Scope))!;
        Assert.Equal(2,retained.Session.Contexts.Count); Assert.Equal("Still typing",retained.Composer); Assert.Equal("Preserve discussion",Assert.Single(retained.Session.Turns).Text);
        js.DuringProjection = () => { js.PendingHtml = "<p>Intervening DOM writing.</p>"; js.SnapshotVersion = 2; return Task.CompletedTask; };
        await Assert.ThrowsAsync<InvalidDataException>(()=>Invoke(workspace,"PrepareBrainstormingContextAsync",new(SectionId:doc.Sections[0].SectionId)));
        Assert.Contains("Intervening",session.ContentFor(page)); Assert.Equal(2,(await sessions.ReadAsync(result.Context.Scope))!.Session.Contexts.Count); js.DuringProjection = null;
        var support = await repository.CreateImportedAsync("Explicit local notes","<p>Author-selected local support.</p>");
        var supported = await context.PrepareAsync(session,new(SectionId:doc.Sections[0].SectionId),[],[support.DocumentId],60_000);
        Assert.Contains(supported.Context!.Sources,s=>s.DocumentId==support.DocumentId && s.Included && s.ExplicitlyEnabled);
        Assert.Equal(DeviceBrainstormingContext.Capture(support,support).Outline.Fingerprint,supported.Context.Sources.Single(s=>s.DocumentId==support.DocumentId).Source.StructureRevision);
        await repository.MoveToTrashAsync(support); await Assert.ThrowsAsync<InvalidDataException>(()=>context.PrepareAsync(session,new(SectionId:doc.Sections[0].SectionId),[],[support.DocumentId],60_000));
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>context.PrepareAsync(session,new(SectionId:doc.Sections[0].SectionId),[],[],60_000,ct:cancelled.Token));
        Directory.Delete(root,true);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task ServerSessionStorageAuthorizesEveryGroupedChapterPage(bool foreign)
    {
        await using var f = new BrainstormingPersistenceTests.Database();await f.Start();var session=f.Empty();
        var part=new BrainstormingContextPart(f.Section,foreign?Guid.NewGuid():f.Page,"v1",new('F',64),0,4);
        var source=new BrainstormingContextSource(Guid.NewGuid(),f.Document,BrainstormingSourceKind.Manuscript,"Chapter",new("v1","outline",new('F',64)),BrainstormingFreshness.Saved,false,true,true,false,"Full source","Text",Parts:[part]);
        session=session with{Contexts=[new(1,Guid.NewGuid(),session.Scope,DateTimeOffset.UtcNow,[source])]};await using var db=new AppDbContext(f.Options);var service=new BrainstormingSessions(db,"author",f.Backend);
        if(foreign)await Assert.ThrowsAsync<BrainstormingAccessException>(()=>service.WriteAsync(new(1,Guid.NewGuid(),0,session),default));
        else Assert.Equal(1,(await service.WriteAsync(new(1,Guid.NewGuid(),0,session),default)).Revision);
        Assert.Equal(foreign?0:1,await db.BrainstormingSessions.CountAsync());Assert.Empty(await db.UsageEvents.ToListAsync());
    }
}
