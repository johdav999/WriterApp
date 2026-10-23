using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WriterApp.Application.Collaboration;
using WriterApp.Application.Exporting;
using WriterApp.Application.Security;
using WriterApp.Application.Subscriptions;
using WriterApp.Controllers;
using WriterApp.Data.Documents;
using WriterApp.Data.Exporting;
using WriterApp.Shared.Collaboration;
using Xunit;

namespace WriterApp.Tests;

public sealed partial class CollaborationConversionTests
{
    private sealed class Users : IUserIdResolver
    { public string ResolveUserId(ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.NameIdentifier)!; }
    private sealed class Authentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync() => Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, Request.Headers["X-Test-Owner"].FirstOrDefault() ?? "owner")], Scheme.Name)), Scheme.Name)));
    }
    private sealed class Outline : IOutlineOrderResolver
    { public Task<OutlineSectionOrderResult> ResolveForManuscriptAsync(Guid projectId, Guid documentId, CancellationToken ct) => Task.FromResult(new OutlineSectionOrderResult([], new Dictionary<Guid, string>())); }
    private sealed class Templates : IExportTemplateResolver
    { public Task<ExportTemplate> ResolveAsync(string owner, Guid? id, CancellationToken ct) => Task.FromResult(new ExportTemplate { Id = id ?? Guid.NewGuid(), PageWidthMm = 210, PageHeightMm = 297, BodyFontSizePt = 12, LineHeight = 1.5m }); }
    private sealed class ExportEntitlements : IEntitlementService
    {
        public Task<UserEntitlements> GetEntitlementsAsync(string owner) => Task.FromResult(new UserEntitlements(owner, "professional", "professional", new Dictionary<string, string>()));
        public PlanTier GetUserTier(UserEntitlements value) => PlanTier.Professional;
        public Task<bool> HasAsync(string owner, string key) => Task.FromResult(true);
        public Task<int?> GetIntAsync(string owner, string key) => Task.FromResult<int?>(null);
        public void InvalidateForUser(string owner) { }
    }
    private TestServer Api(bool sharing = false, string? assetRoot = null, Action<IServiceCollection>? configure = null) => new(new WebHostBuilder().ConfigureServices(services =>
    {
        services.AddLogging(); services.AddSingleton(_db); services.AddSingleton<ICollaborationCodec>(_codec);
        services.AddSingleton<IOptions<CollaborationConversionOptions>>(Options.Create(_options));
        services.AddSingleton<IUserEntitlementStore>(_plans); services.AddSingleton(TimeProvider.System);
        services.AddScoped<IDeletedUserIdentityService, DeletedUserIdentityService>(); services.AddScoped<CollaborationMigrationService>();
        services.AddSingleton<IUserIdResolver, Users>(); services.AddScoped<CollaborationWriteFilter>();
        services.AddSingleton<IOutlineOrderResolver, Outline>(); services.AddSingleton<IExportTemplateResolver, Templates>();
        services.AddSingleton<IEntitlementService, ExportEntitlements>();
        services.AddSingleton(new ExportService([new TemplatedHtmlExportRenderer()], new Templates()));
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Images:StoragePath"] = assetRoot }).Build());
        if (sharing)
        {
            services.AddSingleton<IOptionsMonitor<CollaborationSharingOptions>>(_sharingSettings);
            services.AddScoped<CollaborationAccessService>(); services.AddScoped<CollaborationSharingService>();
            services.AddSingleton<IOptions<CollaborationInteractionOptions>>(Options.Create(_interactionOptions)); services.AddScoped<CollaborationCommentService>();
            services.AddSingleton<IOptionsMonitor<CollaborationSessionOptions>>(new SessionOptions());
            services.AddSingleton<Microsoft.AspNetCore.DataProtection.IDataProtectionProvider>(new Microsoft.AspNetCore.DataProtection.EphemeralDataProtectionProvider());
            services.AddScoped<CollaborationSessionService>();
            services.AddScoped<WriterApp.Application.Documents.IDocumentRepository, DocumentRepository>();
            services.AddSingleton<WriterApp.Application.Search.ISearchIndexBackfillQueue, WriterApp.Application.Search.SearchIndexBackfillQueue>();
            services.AddScoped<WriterApp.Application.Search.ISearchIndexService, WriterApp.Application.Search.SearchIndexService>();
        }
        configure?.Invoke(services);
        services.AddAuthentication("Fixture").AddScheme<AuthenticationSchemeOptions, Authentication>("Fixture", _ => { }); services.AddAuthorization();
        services.AddControllers(o => o.Filters.AddService<CollaborationWriteFilter>()).AddApplicationPart(typeof(CollaborationManuscriptsController).Assembly);
    }).Configure(app => { app.UseRouting(); app.UseAuthentication(); app.UseAuthorization(); app.UseEndpoints(e => e.MapControllers()); }));

    [Fact]
    public async Task HttpMigrationAndConsistentConsumersUseWatermarksAndNeverExportLegacyText()
    {
        using var api = Api(); using var client = api.CreateClient(); string root = $"/api/documents/{_document}/collaboration";
        var request = new CollaborationMigrationRequest(Guid.NewGuid(), _version, true);
        client.DefaultRequestHeaders.Add("X-Test-Owner", "other");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(root + "/migration", request)).StatusCode);
        client.DefaultRequestHeaders.Remove("X-Test-Owner");
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(root + "/migration", request)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(root + $"/migration/{request.OperationId}/resume", new { })).StatusCode);
        var capabilities = await client.GetFromJsonAsync<CollaborationCapabilities>(root + "/capabilities");
        Assert.False(capabilities!.FeatureAvailable); Assert.False(capabilities.CanWriteCollaboratively);
        var read = await (await client.PostAsJsonAsync(root + "/read", new CollaborationReadRequest())).Content.ReadFromJsonAsync<CollaborationReadSnapshot>();
        var vector = new CollaborationReadRequest(read!.StructureRevision, read.Pages.Select(p => new CollaborationWatermark(p.PageId, p.Generation, p.Sequence)).ToArray());
        var export = new ExportDocumentRequest(_document, "html", null, "document", IncludeToc: false, IncludeTitlePage: false, Collaboration: vector);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/documents/{_document}/export/print", export with { Collaboration = null })).StatusCode);
        var page = await _db.CollaborationPages.SingleAsync(p => p.PageId == _page);
        var seed = await _codec.Seed("<p>Fresh committed fixture</p>", "Html", default);
        page.CommittedSequence = 1; page.SnapshotSequence = 1;
        _db.CollaborationSnapshots.Add(new() { PageId = _page, Generation = page.Generation, Sequence = 1, State = seed.State, Sha256 = seed.Sha256 }); await _db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(root + "/ai-context", vector)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/documents/{_document}/export/print", export)).StatusCode);
        var preview = new ExportPreviewRequest(_document, null, "html", false, "document", IncludeTitlePage: false, Collaboration: vector);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/export/preview", preview)).StatusCode);
        await new CollaborationProjectionService(_db, _codec).Project(_page, page.Generation, default);
        var printed = await client.PostAsJsonAsync($"/api/documents/{_document}/export/print", export);
        Assert.Equal(HttpStatusCode.OK, printed.StatusCode);
        var payload = JsonDocument.Parse(await printed.Content.ReadAsStringAsync()).RootElement.GetProperty("html").GetString();
        Assert.Contains("Fresh committed fixture", payload); Assert.DoesNotContain("Bold", payload);
        var previewed = await client.PostAsJsonAsync("/api/export/preview", preview);
        Assert.Equal(HttpStatusCode.OK, previewed.StatusCode); Assert.Contains("Fresh committed fixture", await previewed.Content.ReadAsStringAsync());
        var ai = await client.PostAsJsonAsync(root + "/ai-context", vector);
        Assert.Equal(HttpStatusCode.OK, ai.StatusCode); Assert.Contains("Fresh committed fixture", await ai.Content.ReadAsStringAsync());
        var legacyAi = new WriterApp.Application.AI.WebAiSourceService(_db, "owner");
        await Assert.ThrowsAsync<WriterApp.Application.Documents.DocumentSyncException>(() => legacyAi.Capture(_document, null, null, null, default));
        client.DefaultRequestHeaders.Add("X-Test-Owner", "other");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(root + "/read", vector)).StatusCode);
    }
}
