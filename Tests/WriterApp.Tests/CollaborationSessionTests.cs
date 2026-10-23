using System.Diagnostics;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WriterApp.Application.Collaboration;
using WriterApp.Application.Security;
using WriterApp.Application.Subscriptions;
using WriterApp.Controllers;
using WriterApp.Data;
using WriterApp.Data.Collaboration;
using WriterApp.Data.Subscriptions;
using WriterApp.Shared.Collaboration;
using Xunit;
using Xunit.Abstractions;

namespace WriterApp.Tests;

public sealed partial class CollaborationSessionTests(ITestOutputHelper testOutput) : IAsyncLifetime
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), "WriterApp_SessionTests_" + Guid.NewGuid().ToString("N") + ".db");
    private DbContextOptions<AppDbContext>? _sqlite;
    private DbContextOptions<SqlServerMigrationsDbContext>? _sql;
    private string? _database;
    private readonly Guid _document = Guid.NewGuid(), _page = Guid.NewGuid(), _generation = Guid.NewGuid();
    private readonly Plans _plans = new();
    private readonly TestClock _clock = new();
    private readonly EphemeralDataProtectionProvider _protection = new();
    private readonly Settings _settings = new();
    private AppDbContext Open() => _sql is null ? new AppDbContext(_sqlite!) : new SqlServerMigrationsDbContext(_sql);
    private CollaborationSessionRequest Request => new(_generation, Guid.NewGuid(), 2, 1, new string('a', 64));
    private CollaborationSessionService Service(AppDbContext db) => new(db, _protection, _settings, _plans, new DeletedUserIdentityService(db), _clock);

    public async Task InitializeAsync()
    {
        string? server = Environment.GetEnvironmentVariable("WRITERAPP_SYNC_SQLSERVER_TEST_SERVER");
        if (string.IsNullOrWhiteSpace(server)) _sqlite = new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={_file};Pooling=False").Options;
        else
        {
            _database = "WriterApp_SessionTests_" + Guid.NewGuid().ToString("N");
            _sql = new DbContextOptionsBuilder<SqlServerMigrationsDbContext>().UseSqlServer(new SqlConnectionStringBuilder
                { DataSource = server, InitialCatalog = _database, IntegratedSecurity = true, TrustServerCertificate = true }.ConnectionString,
                sql => sql.EnableRetryOnFailure()).Options;
        }
        await using var db = Open(); await db.Database.EnsureCreatedAsync();
        var project = Guid.NewGuid(); var section = Guid.NewGuid();
        db.Projects.Add(new() { Id = project, OwnerUserId = "owner" });
        db.Documents.Add(new() { Id = _document, ProjectId = project, OwnerUserId = "owner" });
        db.Sections.Add(new() { Id = section, DocumentId = _document });
        db.Pages.Add(new() { Id = _page, DocumentId = _document, SectionId = section, Content = "<p>Legacy retained</p>" });
        await db.SaveChangesAsync();
        await db.Documents.ExecuteUpdateAsync(s => s.SetProperty(d => d.CollaborationMode, CollaborationMode.Collaborative));
        db.ChangeTracker.Clear();
        db.CollaborationPages.Add(new() { DocumentId = _document, PageId = _page, Generation = _generation, ContentContractVersion = 2, ProtocolVersion = 1, SchemaFingerprint = new string('a', 64) });
        byte[] empty = [0, 0];
        db.CollaborationSnapshots.Add(new() { PageId = _page, Generation = _generation, Sequence = 0, State = empty, Sha256 = Convert.ToHexString(SHA256.HashData(empty)) });
        await db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await using var db = Open();
        if (_database is not null && _database.StartsWith("WriterApp_SessionTests_", StringComparison.Ordinal)
            && db.Database.GetDbConnection().Database == _database) await db.Database.EnsureDeletedAsync();
        if (_sql is null && _loadConnectionPool) { using var pooled = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={_file};Pooling=True"); Microsoft.Data.Sqlite.SqliteConnection.ClearPool(pooled); }
        if (_sql is null && File.Exists(_file)) File.Delete(_file);
    }

    [Theory]
    [InlineData(null, "Collaborator")]
    [InlineData("  ", "Collaborator")]
    [InlineData("owner", "Collaborator")]
    [InlineData("  Johan Writer  ", "Johan Writer")]
    public async Task PresenceDisplayNameUsesAccountProfileWithSafeFallback(string? name, string expected)
    {
        await using var db = Open();
        if (name is not null) { db.UserProfiles.Add(new() { UserId = "owner", DisplayName = name }); await db.SaveChangesAsync(); }
        var service = Service(db);
        var ticket = await service.Issue("owner", _document, _page, Request, default);
        var session = await service.Authorize(new(ticket.Ticket, ticket.RoomName, Guid.NewGuid(), true), default);
        Assert.Equal(expected, await service.PresenceDisplayName(session, default));
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("plan")]
    [InlineData("account")]
    [InlineData("document-deleted")]
    [InlineData("document-archived")]
    [InlineData("mode")]
    [InlineData("page-deleted")]
    [InlineData("schema")]
    [InlineData("protocol")]
    [InlineData("contract")]
    [InlineData("generation")]
    [InlineData("disabled")]
    [InlineData("environment")]
    [InlineData("expired")]
    public async Task IssuedTicketRechecksCurrentAuthorityOnEveryValidation(string mutation)
    {
        await using var db = Open(); var service = Service(db);
        var ticket = await service.Issue("owner", _document, _page, Request, default);
        var request = new CollaborationServiceRequest(ticket.Ticket, ticket.RoomName, Guid.NewGuid(), true);
        await service.Authorize(request, default);
        switch (mutation)
        {
            case "owner": await db.Documents.ExecuteUpdateAsync(s => s.SetProperty(d => d.OwnerUserId, "other")); break;
            case "plan": _plans.Paid = false; break;
            case "account": db.DeletedUserIdentities.Add(new() { UserId = IdNorm.Norm("owner"), DeletedAtUtc = DateTime.UtcNow }); await db.SaveChangesAsync(); break;
            case "document-deleted": await db.Documents.ExecuteUpdateAsync(s => s.SetProperty(d => d.DeletedAtUtc, DateTime.UtcNow)); break;
            case "document-archived": await db.Documents.ExecuteUpdateAsync(s => s.SetProperty(d => d.IsArchived, true)); break;
            case "mode": await db.Documents.ExecuteUpdateAsync(s => s.SetProperty(d => d.CollaborationMode, CollaborationMode.Migrating)); break;
            case "page-deleted": await db.CollaborationPages.ExecuteUpdateAsync(s => s.SetProperty(p => p.IsDeleted, true)); break;
            case "schema": await db.CollaborationPages.ExecuteUpdateAsync(s => s.SetProperty(p => p.SchemaFingerprint, new string('b', 64))); break;
            case "protocol": await db.CollaborationPages.ExecuteUpdateAsync(s => s.SetProperty(p => p.ProtocolVersion, 99)); break;
            case "contract": await db.CollaborationPages.ExecuteUpdateAsync(s => s.SetProperty(p => p.ContentContractVersion, 99)); break;
            case "generation": db.CollaborationPages.Add(new() { PageId = _page, DocumentId = _document, Generation = Guid.NewGuid() }); await db.SaveChangesAsync(); break;
            case "disabled": _settings.CurrentValue.SessionsEnabled = false; break;
            case "environment": _settings.CurrentValue.Environment = "other"; break;
            case "expired": _clock.Advance(TimeSpan.FromSeconds(301)); break;
        }
        await Assert.ThrowsAsync<CollaborationSessionDeniedException>(() => service.Authorize(request with { Acquire = false }, default));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("hash")]
    [InlineData("tail-gap")]
    public async Task CorruptOrMissingBinaryHistoryNeverCreatesAnEmptyRoom(string mutation)
    {
        await using var db = Open();
        if (mutation == "missing") await db.CollaborationSnapshots.ExecuteDeleteAsync();
        if (mutation == "hash") await db.CollaborationSnapshots.ExecuteUpdateAsync(s => s.SetProperty(x => x.Sha256, new string('f', 64)));
        if (mutation == "tail-gap") await db.CollaborationPages.ExecuteUpdateAsync(s => s.SetProperty(x => x.CommittedSequence, 1));
        await Assert.ThrowsAsync<CollaborationSessionDeniedException>(() => Service(db).Issue("owner", _document, _page, Request, default));
        Assert.Empty(await db.CollaborationSessions.ToArrayAsync());
    }

    [Fact]
    public async Task ConnectionReplayRefreshExpiryAndDurableRevocation()
    {
        CollaborationSessionTicket ticket;
        await using (var db = Open()) ticket = await Service(db).Issue("owner", _document, _page, Request, default);
        var request = new CollaborationServiceRequest(ticket.Ticket, ticket.RoomName, Guid.NewGuid(), true);
        await using (var db = Open()) await Service(db).Authorize(request, default);
        await using (var db = Open()) await Assert.ThrowsAsync<CollaborationSessionDeniedException>(() => Service(db).Authorize(request with { ConnectionId = Guid.NewGuid() }, default));
        _clock.Advance(TimeSpan.FromSeconds(301));
        await using (var db = Open()) await Assert.ThrowsAsync<CollaborationSessionDeniedException>(() => Service(db).Authorize(request, default));
        await using (var db = Open()) ticket = await Service(db).Refresh("owner", _document, _page, ticket.SessionId, ticket.Version, default);
        await using (var db = Open()) await Assert.ThrowsAsync<CollaborationSessionDeniedException>(() => Service(db).Authorize(request, default));
        await using (var db = Open()) await Service(db).Authorize(request with { Ticket = ticket.Ticket, ConnectionId = Guid.NewGuid() }, default);
        await using (var db = Open()) await Service(db).Revoke("owner", _document, _page, ticket.SessionId, default);
        await using (var db = Open()) await Assert.ThrowsAsync<CollaborationSessionDeniedException>(() => Service(db).Refresh("owner", _document, _page, ticket.SessionId, ticket.Version, default));
    }

    [Fact]
    public async Task InvalidTicketEncodingsAndCrossEnvironmentRevocationAreDenied()
    {
        await using var db = Open(); var service = Service(db);
        var ticket = await service.Issue("owner", _document, _page, Request, default);
        foreach (string invalid in new[] { "", "%invalid!", new string('x', 8193) })
            await Assert.ThrowsAsync<CollaborationSessionDeniedException>(() => service.Authorize(new(invalid, ticket.RoomName, Guid.NewGuid(), true), default));
        _settings.CurrentValue.Environment = "different";
        await Assert.ThrowsAsync<CollaborationSessionDeniedException>(() => service.Revoke("owner", _document, _page, ticket.SessionId, default));
    }

    [Fact]
    public async Task TwoBackendInstancesCannotAcquireTheSameTicket()
    {
        await using var first = Open(); await using var second = Open();
        var ticket = await Service(first).Issue("owner", _document, _page, Request, default);
        var one = new CollaborationServiceRequest(ticket.Ticket, ticket.RoomName, Guid.NewGuid(), true);
        var two = one with { ConnectionId = Guid.NewGuid() };
        var results = await Task.WhenAll(Acquire(Service(first), one), Acquire(Service(second), two));
        Assert.Single(results, result => result);
        static async Task<bool> Acquire(CollaborationSessionService service, CollaborationServiceRequest request)
        { try { await service.Authorize(request, default); return true; } catch (CollaborationSessionDeniedException) { return false; } }
    }

    [Fact]
    public async Task RealKestrelAndHocuspocusRejectUnauthorizedReplayAndTextWrites()
    {
        await RunProbe("backend-probe.mjs", Request);
        await using var verify = Open();
        Assert.Equal("<p>Legacy retained</p>", (await verify.Pages.SingleAsync()).Content);
        Assert.Empty(await verify.CollaborationUpdates.ToArrayAsync());
    }

    private async Task RunProbe(string script, CollaborationSessionRequest sessionRequest)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = FindRoot(), EnvironmentName = _localTestUi ? "Staging" : null }); builder.Logging.ClearProviders();
        if (_localTestUi) builder.Configuration.AddInMemoryCollection(new Dictionary<string,string?> {
            ["NativeAuth:Issuer"]="https://fixture.test/",["Collaboration:LocalTest:Enabled"]="true",["Collaboration:SessionsEnabled"]="true",["Collaboration:CommitsEnabled"]="true",
            ["Collaboration:Sharing:Enabled"]="true",["Collaboration:Conversion:Enabled"]="true",["Collaboration:Environment"]="test",
            ["Collaboration:WebSocketUrl"]="ws://localhost:1234/",["Collaboration:ServiceKey"]=_settings.CurrentValue.ServiceKey });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddScoped(_ => Open()); builder.Services.AddSingleton<IDataProtectionProvider>(_protection);
        builder.Services.AddSingleton<IOptionsMonitor<CollaborationSessionOptions>>(_settings);
        builder.Services.AddSingleton<IUserEntitlementStore>(_plans); builder.Services.AddSingleton<TimeProvider>(_clock);
        builder.Services.AddSingleton<IOptionsMonitor<CollaborationSharingOptions>>(_sharingOptions);
        builder.Services.AddSingleton<IOptions<CollaborationInteractionOptions>>(Options.Create(_interactions));
        builder.Services.Configure<CollaborationConversionOptions>(o => o.Enabled = _localTestUi);
        builder.Services.AddSingleton<CollaborationStagedTestGate>();
        builder.Services.AddScoped<CollaborationMigrationService>();builder.Services.AddScoped<CollaborationProjectionService>();
        if (_localTestUi) builder.Services.AddHostedService<CollaborationProjectionWorker>();
        builder.Services.AddScoped<ICollaborationCodec, CollaborationCodec>(); builder.Services.AddScoped<CollaborationCommentService>();
        builder.Services.AddScoped<CollaborationAccessService>(); builder.Services.AddScoped<CollaborationSharingService>();
        builder.Services.AddScoped<IDeletedUserIdentityService, DeletedUserIdentityService>();
        builder.Services.AddScoped<CollaborationSessionService>(); builder.Services.AddScoped<CollaborationCommitService>(); builder.Services.AddScoped<CollaborationWriteFilter>();
        builder.Services.AddSingleton<IUserIdResolver, TestUsers>();
        if(_localTestUi)builder.Services.AddSingleton<IEntitlementService,BrowserPlans>();
        if(_brainstormUi)CollaborationConversionTests.BrainstormServices(builder.Services,new BrainstormingConversationTests.Provider {Wire=WriterApp.Shared.Brainstorming.BrainstormingPersistence.Json(new WriterApp.Shared.Brainstorming.BrainstormingAnswer("Draft ready.",1,WriterApp.Shared.Brainstorming.BrainstormingIntent.Draft,new("Hon tvivlade. 日本語 🧭","Understated",[]),[],[]))},"owner");
        builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, TestAuthentication>("Test", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddControllers(o => o.Filters.AddService<CollaborationWriteFilter>()).AddApplicationPart(typeof(CollaborationSessionsController).Assembly);
        await using var app = builder.Build();
        if (_localTestUi) app.Use(async (context,next) => {
            try { await next(context); }
            catch(Exception exception) { testOutput.WriteLine(exception.ToString()); context.Response.StatusCode=500; await context.Response.WriteAsJsonAsync(new{code=exception.GetType().Name+": "+exception.Message}); }
        });
        app.UseAuthentication(); app.UseAuthorization(); app.MapControllers(); if(_localTestUi)MapBrowserAiFixture(app); await app.StartAsync();
        string address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        string root = FindRoot();
        var start = new ProcessStartInfo("node") { WorkingDirectory = root, RedirectStandardError = true, RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
        if (script == "operations-probe.mjs") start.ArgumentList.Add("--expose-gc");
        start.ArgumentList.Add(Path.Combine(root, "WriterApp.Collaboration", "tests", script));
        if (_operationsWorkload is not null) start.Environment["PROBE_WORKLOAD"] = JsonSerializer.Serialize(_operationsWorkload, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        start.Environment["PROBE_BACKEND_URL"] = address;
        start.Environment["PROBE_SESSION_PATH"] = $"/api/documents/{_document}/pages/{_page}/collaboration/sessions";
        start.Environment["PROBE_SESSION_REQUEST"] = JsonSerializer.Serialize(sessionRequest, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        start.Environment["COLLABORATION_SERVICE_KEY"] = _settings.CurrentValue.ServiceKey;
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
        try
        {
            // The Brainstorm journey includes two-way publication and multiple
            // reload/replay cycles; leave bounded time for socket/room shutdown too.
            try { await process.WaitForExitAsync().WaitAsync(script == "operations-probe.mjs" ? TimeSpan.FromMinutes(4) : script=="brainstorm-ui-browser-probe.mjs" ? TimeSpan.FromMinutes(6) : TimeSpan.FromSeconds(150)); }
            catch (TimeoutException)
            {
                process.Kill(entireProcessTree: true); await process.WaitForExitAsync();
                throw new TimeoutException("Owned probe timed out.\n" + await output + await error);
            }
            string evidence = await output + await error;
            Assert.True(process.ExitCode == 0, evidence); Assert.Contains("PASS:", evidence);
            testOutput.WriteLine(evidence);
        }
        finally { if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); } await app.StopAsync(); }
    }

    private static string FindRoot()
    { var directory = new DirectoryInfo(AppContext.BaseDirectory); while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "BlazorApp.csproj"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found"); }
    private sealed class TestClock : TimeProvider
    { private DateTimeOffset _now = DateTimeOffset.UtcNow; public bool RealTime; public override DateTimeOffset GetUtcNow() => RealTime ? DateTimeOffset.UtcNow : _now; public void Advance(TimeSpan duration) => _now += duration; }
    private sealed class Plans : IUserEntitlementStore
    { public bool Paid = true; public Task<UserEntitlement> GetOrCreateAsync(string userId, CancellationToken cancellationToken = default) => Task.FromResult(new UserEntitlement { UserId = userId, PlanKey = Paid ? "standard" : "free", SubscriptionStatus = "active" }); }
    private sealed class Settings : IOptionsMonitor<CollaborationSessionOptions>
    {
        public CollaborationSessionOptions CurrentValue { get; } = new() { SessionsEnabled = true, Environment = "test", ServiceKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)), WebSocketUrl = "ws://127.0.0.1:1234" };
        public CollaborationSessionOptions Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<CollaborationSessionOptions, string?> listener) => null;
    }
    private sealed class TestUsers : IUserIdResolver
    { public string ResolveUserId(ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.NameIdentifier)!; }
    private sealed class TestAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            string user = Request.Headers["X-Test-User"].FirstOrDefault() ?? "anonymous";
            return Task.FromResult(user == "anonymous" ? AuthenticateResult.NoResult() : AuthenticateResult.Success(new AuthenticationTicket(
                new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, user)], Scheme.Name)), Scheme.Name)));
        }
    }
}
