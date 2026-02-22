
using BlazorApp.Components;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Microsoft.Data.Sqlite;
using System.IO;
using System.Security;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Runtime.InteropServices;
using System.Threading;
using System.Data.Common;
using System.Globalization;
using System.Diagnostics;
using WriterApp.AI.Abstractions;
using WriterApp.AI.Actions;
using WriterApp.AI.Core;
using WriterApp.AI.Providers.Mock;
using WriterApp.AI.Providers.OpenAI;
using WriterApp.Application.Security;
using WriterApp.Application.Subscriptions;
using WriterApp.Application.Usage;
using WriterApp.Application.Commands;
using WriterApp.Application.Documents;
using WriterApp.Application.Exporting;
using WriterApp.Application.State;
using WriterApp.Application.AI.StoryCoach;
using WriterApp.Application.Synopsis;
using WriterApp.Application.Diagnostics;
using WriterApp.Application.Diagnostics.Circuits;
using WriterApp.Application.Importing;
using WriterApp.Application.Search;
using WriterApp.Application.Continuity;
using WriterApp.Data;
using WriterApp.Data.Subscriptions;
using WriterApp.Shared;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddFilter("Microsoft.AspNetCore.Components.Server.Circuits", LogLevel.Information);
builder.Logging.AddFilter(
    "Microsoft.AspNetCore.SignalR",
    builder.Environment.IsDevelopment() ? LogLevel.Debug : LogLevel.Information);

// Auth mode configuration:
// - Local Development always uses FakeAuth (DEV_AUTH_* claims), regardless of UseExternalIdAuth.
// - Staging/Production use External ID via EasyAuth when UseExternalIdAuth=true.
// Required when UseExternalIdAuth=true:
// - ExternalIdTenantId (tenant guid)
// - ExternalIdClientId (app registration client id for the current slot/environment)
bool useExternalIdAuthConfigured =
    builder.Configuration.GetValue<bool?>("UseExternalIdAuth")
    ?? builder.Configuration.GetValue<bool?>("WriterApp:Auth:UseExternalIdAuth")
    ?? true;
bool useExternalIdAuth = !builder.Environment.IsDevelopment() && useExternalIdAuthConfigured;
string externalIdTenantId =
    builder.Configuration["ExternalIdTenantId"]
    ?? builder.Configuration["WriterApp:Auth:ExternalIdTenantId"]
    ?? string.Empty;
string externalIdClientId =
    builder.Configuration["ExternalIdClientId"]
    ?? builder.Configuration["WriterApp:Auth:ExternalIdClientId"]
    ?? string.Empty;

if (useExternalIdAuth
    && (string.IsNullOrWhiteSpace(externalIdTenantId) || string.IsNullOrWhiteSpace(externalIdClientId)))
{
    throw new InvalidOperationException(
        "External ID auth is enabled, but ExternalIdTenantId and/or ExternalIdClientId is missing.");
}

// --------------------
// Services
// --------------------

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddDbContext<AppDbContext>(options =>
{
    string connectionString = ResolveSqliteConnectionString(builder.Configuration, builder.Environment);
    options.UseSqlite(connectionString);
});

builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = useExternalIdAuth
            ? EasyAuthAuthenticationHandler.SchemeName
            : FakeAuthAuthenticationHandler.SchemeName;
        options.DefaultChallengeScheme = useExternalIdAuth
            ? EasyAuthAuthenticationHandler.SchemeName
            : FakeAuthAuthenticationHandler.SchemeName;
    })
    .AddScheme<AuthenticationSchemeOptions, EasyAuthAuthenticationHandler>(
        EasyAuthAuthenticationHandler.SchemeName,
        _ => { })
    .AddScheme<AuthenticationSchemeOptions, FakeAuthAuthenticationHandler>(
        FakeAuthAuthenticationHandler.SchemeName,
        _ => { });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireAuthenticatedUser()
            .RequireAssertion(context =>
            {
                ClaimsPrincipal user = context.User;
                bool isRoleAdmin = user.IsInRole("Admin");

                string? userOid = user.FindFirstValue("http://schemas.microsoft.com/identity/claims/objectidentifier")
                    ?? user.FindFirstValue("oid");

                string? bootstrapEnabledValue = builder.Configuration["BOOTSTRAP_ADMIN_ENABLED"];
                bool bootstrapEnabled = string.Equals(bootstrapEnabledValue, "true", StringComparison.OrdinalIgnoreCase);
                string? bootstrapOid = builder.Configuration["BOOTSTRAP_ADMIN_OID"];

                bool bootstrapMatch =
                    bootstrapEnabled
                    && !string.IsNullOrWhiteSpace(bootstrapOid)
                    && !string.IsNullOrWhiteSpace(userOid)
                    && string.Equals(bootstrapOid, userOid, StringComparison.OrdinalIgnoreCase);

                bool allowed = isRoleAdmin || bootstrapMatch;

                AdminPolicyDiagnostics.LogDecision(
                    isRoleAdmin,
                    bootstrapEnabled,
                    !string.IsNullOrWhiteSpace(bootstrapOid),
                    !string.IsNullOrWhiteSpace(userOid),
                    allowed,
                    bootstrapOid,
                    userOid);

                return allowed;
            }));
});
builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped(sp =>
{
    NavigationManager navigation = sp.GetRequiredService<NavigationManager>();
    return new HttpClient { BaseAddress = new Uri(navigation.BaseUri) };
});
builder.Services.AddScoped<OutlineTemplatesClient>();

builder.Services.AddMemoryCache();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto |
        ForwardedHeaders.XForwardedHost;

    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddHttpsRedirection(_ => { });
}
else
{
    builder.Services.AddHttpsRedirection(options => options.HttpsPort = 443);
}

// Domain services
builder.Services.AddScoped<IPlanRepository, PlanRepository>();
builder.Services.AddScoped<IUserEntitlementStore, UserEntitlementStore>();
builder.Services.AddScoped<IEntitlementService, EntitlementService>();
builder.Services.AddScoped<IUserIdResolver, UserIdResolver>();
builder.Services.AddScoped<IPlanAssignmentService, PlanAssignmentService>();
builder.Services.AddScoped<IUsageMeter, UsageMeter>();
builder.Services.AddSingleton<IClock, WriterApp.Application.Usage.SystemClock>();
builder.Services.AddScoped<IAiQuotaService, AiQuotaService>();
builder.Services.AddScoped<IAiUsageStatusService, AiUsageStatusService>();
builder.Services.AddScoped<IAiUsagePolicy, AiUsagePolicy>();
builder.Services.AddSingleton<ISectionImportService, SectionImportService>();
builder.Services.AddScoped<IBibleStore, EfCoreBibleStore>();
builder.Services.AddSingleton<BiblePatchApplier>();
builder.Services.AddScoped<BibleRefreshService>();
builder.Services.Configure<QualityRewriteValidationOptions>(builder.Configuration.GetSection("WriterApp:QualityRewriteValidation"));
builder.Services.AddScoped<WriterApp.Application.Documents.IDocumentRepository, WriterApp.Data.Documents.DocumentRepository>();
builder.Services.AddScoped<WriterApp.Application.Documents.ISectionRepository, WriterApp.Data.Documents.SectionRepository>();
builder.Services.AddScoped<WriterApp.Application.Documents.IPageRepository, WriterApp.Data.Documents.PageRepository>();
builder.Services.AddScoped<WriterApp.Application.Documents.IPageVersionService, WriterApp.Application.Documents.PageVersionService>();
builder.Services.AddScoped<WriterApp.Application.Documents.IPageVersionDiffService, WriterApp.Application.Documents.PageVersionDiffService>();
builder.Services.AddScoped<WriterApp.Application.Documents.IQualityCheckService, WriterApp.Application.Documents.QualityCheckService>();
builder.Services.AddScoped<WriterApp.Application.Documents.IProjectWordCountService, WriterApp.Application.Documents.ProjectWordCountService>();
builder.Services.AddScoped<WriterApp.Application.Documents.IProjectGoalsService, WriterApp.Application.Documents.ProjectGoalsService>();
builder.Services.AddScoped<WriterApp.Application.Documents.IProjectSceneLinkingService, WriterApp.Application.Documents.ProjectSceneLinkingService>();
builder.Services.AddScoped<WriterApp.Application.Documents.ISceneContentBackfillService, WriterApp.Application.Documents.SceneContentBackfillService>();
builder.Services.AddSingleton<WriterApp.Application.Commands.IStructureCommandProcessor, WriterApp.Application.Commands.StructureCommandProcessor>();
builder.Services.AddSingleton<ISearchIndexBackfillQueue, SearchIndexBackfillQueue>();
builder.Services.AddHostedService<SearchIndexBackfillHostedService>();
builder.Services.AddScoped<ISearchIndexBackfillWorker, SearchIndexService>();
builder.Services.AddScoped<ISearchIndexService, SearchIndexService>();

builder.Services.AddSingleton<StoryCoachContextBuilder>();
builder.Services.AddSingleton<SynopsisAiContextBuilder>();
builder.Services.Configure<WriterAiOptions>(builder.Configuration.GetSection("WriterApp:AI"));

builder.Services.AddSingleton<IAiTextService, MockAiTextService>();
builder.Services.AddSingleton<IArtifactStore, InMemoryArtifactStore>();
builder.Services.AddSingleton<IAiAttachmentStore, InMemoryAiAttachmentStore>();
builder.Services.AddSingleton<IAiProvider, MockTextProvider>();
builder.Services.AddSingleton<IAiProvider, MockImageProvider>();

WriterAiOpenAiOptions openAiOptions = builder.Configuration
    .GetSection("WriterApp:AI:Providers:OpenAI")
    .Get<WriterAiOpenAiOptions>() ?? new();

OpenAiKeyProvider openAiKeyProvider = OpenAiKeyProvider.FromEnvironment();
builder.Services.AddSingleton(openAiKeyProvider);

if (openAiOptions.Enabled && openAiKeyProvider.HasKey)
{
    builder.Services.AddHttpClient(nameof(OpenAiProvider), client =>
    {
        client.Timeout = TimeSpan.FromSeconds(Math.Max(1, openAiOptions.TimeoutSeconds));
    });
    builder.Services.AddSingleton<IAiProvider, OpenAiProvider>();
}

builder.Services.AddSingleton<IAiProviderRegistry, DefaultAiProviderRegistry>();
builder.Services.AddSingleton<IAiRouter, DefaultAiRouter>();
builder.Services.AddSingleton<IAiAction, RewriteSelectionAction>();
builder.Services.AddSingleton<IAiAction, TranslateSelectionAction>();
builder.Services.AddSingleton<IAiAction, TranslateSectionAction>();
builder.Services.AddSingleton<IAiAction, TranslateDocumentAction>();
builder.Services.AddSingleton<IAiAction, GenerateCoverImageAction>();
builder.Services.AddSingleton<IAiAction, StoryCoachAction>();
builder.Services.AddSingleton<IAiAction, SynopsisEvaluateAction>();
builder.Services.AddSingleton<IAiAction, SynopsisQuestionsAction>();
builder.Services.AddSingleton<IAiAction, GenerateOutlineAction>();
builder.Services.AddSingleton<IAiAction, SceneSuggestAction>();
builder.Services.AddSingleton<IAiAction, SceneRefineAction>();
builder.Services.AddSingleton<IAiAction, SceneFindOpenQuestionsAction>();
builder.Services.AddSingleton<IAiAction, ProposeNextParagraphAction>();
bool reviseToolsEnabled =
    builder.Configuration.GetValue<bool?>("AI:ReviseToolsEnabled")
    ?? builder.Configuration.GetValue<bool?>("WriterApp:AI:ReviseToolsEnabled")
    ?? false;
if (reviseToolsEnabled)
{
    builder.Services.AddSingleton<IAiAction, TightenSelectionAction>();
    builder.Services.AddSingleton<IAiAction, TightenSectionAction>();
    builder.Services.AddSingleton<IAiAction, ExpandSelectionAction>();
    builder.Services.AddSingleton<IAiAction, ExpandSectionAction>();
    builder.Services.AddSingleton<IAiAction, ChangeToneSelectionAction>();
    builder.Services.AddSingleton<IAiAction, ChangeToneSectionAction>();
    builder.Services.AddSingleton<IAiAction, ShowDontTellSelectionAction>();
    builder.Services.AddSingleton<IAiAction, ShowDontTellSectionAction>();
}
bool outlineGeneratorEnabled =
    builder.Configuration.GetValue<bool?>("AI:OutlineGeneratorEnabled")
    ?? builder.Configuration.GetValue<bool?>("WriterApp:AI:OutlineGeneratorEnabled")
    ?? false;
if (outlineGeneratorEnabled)
{
    builder.Services.AddSingleton<IAiAction, GenerateOutlineFromSynopsisAction>();
}
bool continuityCoachEnabled =
    builder.Configuration.GetValue<bool?>("AI:ContinuityCoachEnabled")
    ?? builder.Configuration.GetValue<bool?>("WriterApp:AI:ContinuityCoachEnabled")
    ?? false;
if (continuityCoachEnabled)
{
    builder.Services.AddSingleton<IAiAction, ExtractCharacterBibleAction>();
    builder.Services.AddSingleton<IAiAction, ExtractPlaceBibleAction>();
    builder.Services.AddSingleton<IAiAction, ExtractTimelineBibleAction>();
    builder.Services.AddSingleton<IAiAction, RefreshCharacterBibleAction>();
    builder.Services.AddSingleton<IAiAction, RefreshPlaceBibleAction>();
    builder.Services.AddSingleton<IAiAction, RefreshTimelineBibleAction>();
    builder.Services.AddSingleton<IAiAction, ContinuityCheckAction>();
}
bool continuityCoachFixesEnabled =
    builder.Configuration.GetValue<bool?>("AI:ContinuityCoachFixesEnabled")
    ?? builder.Configuration.GetValue<bool?>("WriterApp:AI:ContinuityCoachFixesEnabled")
    ?? false;
if (continuityCoachFixesEnabled)
{
    builder.Services.AddSingleton<IAiAction, ApplyContinuityFixAction>();
}
bool promptLibraryEnabled =
    builder.Configuration.GetValue<bool?>("AI:PromptLibraryEnabled")
    ?? builder.Configuration.GetValue<bool?>("WriterApp:AI:PromptLibraryEnabled")
    ?? false;
if (promptLibraryEnabled)
{
    builder.Services.AddSingleton<IAiAction, CustomTransformAction>();
}
builder.Services.AddSingleton<IAiActionExecutor, AiActionExecutor>();
builder.Services.AddSingleton<IAiProposalApplier, DefaultProposalApplier>();
builder.Services.AddScoped<IAiOrchestrator, AiOrchestrator>();
builder.Services.AddScoped<WriterApp.Application.AI.IAiActionHistoryStore, WriterApp.Application.AI.EfCoreAiActionHistoryStore>();

builder.Services.AddScoped<DocumentStorageService>();
builder.Services.AddScoped<LegacyDocumentMigrationService>();
builder.Services.AddScoped<AppHeaderState>();
builder.Services.AddScoped<LayoutStateService>();
builder.Services.AddSingleton<ClientEventLog>();
builder.Services.AddSingleton<CircuitHandler, CircuitLoggingHandler>();

builder.Services.AddSingleton<IExportRenderer, MarkdownExportRenderer>();
builder.Services.AddSingleton<IExportRenderer, TemplatedHtmlExportRenderer>();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IExportRenderer, DocxExportRenderer>();
builder.Services.AddSingleton<IExportRenderer, EpubExportRenderer>();
builder.Services.AddSingleton<IExportRenderer, SynopsisDocxExportRenderer>();
builder.Services.AddSingleton<IExportRenderer, SynopsisMarkdownExportRenderer>();
builder.Services.AddSingleton<IExportRenderer, SynopsisHtmlExportRenderer>();
builder.Services.AddScoped<IExportTemplateSeeder, ExportTemplateSeeder>();
builder.Services.AddScoped<IExportTemplateResolver, ExportTemplateResolver>();
builder.Services.AddScoped<IExportPresetService, ExportPresetService>();
builder.Services.AddScoped<ExportService>();
builder.Services.AddScoped<IDocumentLifecycleService, DocumentLifecycleService>();

builder.Services.AddServerSideBlazor()
    .AddHubOptions(options =>
    {
        options.MaximumReceiveMessageSize = builder.Environment.IsDevelopment()
            ? 10 * 1024 * 1024
            : 2 * 1024 * 1024; // Keep production payloads tighter; increase if needed.
    })
    .AddCircuitOptions(o => o.DetailedErrors = true);

var app = builder.Build();
QualityRewriteOutputValidator.Configure(app.Services.GetRequiredService<IOptions<QualityRewriteValidationOptions>>().Value);

AdminPolicyDiagnostics.Configure(app.Services.GetRequiredService<ILoggerFactory>());

// Manual verification:
// 1) Local: delete sqlite file, start app, verify migrations create schema and startup logs "Migrations ok; schema up to date."
// 2) Azure: POST /api/admin/db/migrate (Admin + optional X-DB-MIGRATE-KEY), then retry the failing workflow.
if (args.Any(arg => string.Equals(arg, "--migrate", StringComparison.OrdinalIgnoreCase)))
{
    using IServiceScope migrateScope = app.Services.CreateScope();
    ILogger migrateLogger = migrateScope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DbMigrateCli");
    AppDbContext migrateDbContext = migrateScope.ServiceProvider.GetRequiredService<AppDbContext>();

    LogSqliteConnectionDetails(migrateDbContext, migrateLogger);

    bool diagnosticsDbOnly =
        app.Environment.IsDevelopment()
        || app.Configuration.GetValue<bool?>("DIAGNOSTICS_DB") == true;
    if (diagnosticsDbOnly)
    {
        LogSqliteTables(migrateDbContext, migrateLogger, "pre-migrate-cli");
    }

    await ApplyDatabaseMigrationsAsync(migrateDbContext, migrateLogger, CancellationToken.None);
    migrateLogger.LogInformation("Migrations ok; schema up to date.");

    if (diagnosticsDbOnly)
    {
        LogSqliteTables(migrateDbContext, migrateLogger, "post-migrate-cli");
    }

    return;
}

// --------------------
// Startup probes
// --------------------

using (IServiceScope scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");

    LogRuntimeProbe(logger);
    ProbeSqlite(logger);

    AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    LogSqliteConnectionDetails(dbContext, logger);

    bool diagnosticsDb =
        app.Environment.IsDevelopment()
        || app.Configuration.GetValue<bool?>("DIAGNOSTICS_DB") == true;
    if (diagnosticsDb)
    {
        LogSqliteTables(dbContext, logger, "pre-migrate");
    }

    LogSchemaHistoryMismatchWarning(dbContext, logger);

    bool autoMigrate = app.Configuration.GetValue<bool?>("AUTO_MIGRATE") ?? true;
    if (autoMigrate)
    {
        await ApplyDatabaseMigrationsAsync(dbContext, logger, CancellationToken.None);
        logger.LogInformation("Migrations ok; schema up to date.");
    }
    else
    {
        logger.LogWarning("AUTO_MIGRATE is disabled. Skipping Database.Migrate().");
    }

    if (diagnosticsDb)
    {
        LogTablePresence(dbContext, logger, "PageVersions");
        LogTablePresence(dbContext, logger, "OutlineTemplates");
        LogSqliteTables(dbContext, logger, "post-migrate");
    }

    try
    {
        IDocumentLifecycleService lifecycle =
            scope.ServiceProvider.GetRequiredService<IDocumentLifecycleService>();
        int removed = await lifecycle.CleanupExpiredTrashAsync(TimeSpan.FromDays(30), CancellationToken.None);
        if (removed > 0)
        {
            logger.LogInformation("Startup trash cleanup removed {Count} documents.", removed);
        }
    }
    catch (Exception ex)
    {
        try
        {
            logger.LogWarning(ex, "Startup trash cleanup failed.");
        }
        catch (Exception logEx)
        {
            Console.Error.WriteLine(
                $"Startup trash cleanup failed and could not be logged via ILogger. " +
                $"CleanupError: {ex.GetType().Name}: {ex.Message}; " +
                $"LoggingError: {logEx.GetType().Name}: {logEx.Message}");
        }
    }

    ApplySqlitePragmas(dbContext, logger);

    bool runSceneContentBackfillOnStartup =
        app.Configuration.GetValue<bool?>("Workflow:SceneContentBackfillRunOnStartup")
        ?? app.Configuration.GetValue<bool?>("WriterApp:Workflow:SceneContentBackfillRunOnStartup")
        ?? false;
    if (runSceneContentBackfillOnStartup)
    {
        try
        {
            ISceneContentBackfillService backfill = scope.ServiceProvider.GetRequiredService<ISceneContentBackfillService>();
            SceneContentBackfillResult result = await backfill.BackfillAsync(CancellationToken.None);
            logger.LogInformation(
                "Startup scene-content backfill completed. TotalScenes={TotalScenes}, ExistingSceneContent={ExistingSceneContent}, CreatedSceneContent={CreatedSceneContent}, CreatedSceneNotes={CreatedSceneNotes}, CreatedSceneCards={CreatedSceneCards}, FailedScenes={FailedScenes}.",
                result.TotalScenes,
                result.ExistingSceneContent,
                result.CreatedSceneContent,
                result.CreatedSceneNotes,
                result.CreatedSceneCards,
                result.FailedScenes);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Startup scene-content backfill failed.");
        }
    }
}


if (app.Environment.IsDevelopment())
{
    IServiceScope scope = app.Services.CreateScope();
       var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");

    app.UseWebAssemblyDebugging();
    logger.LogInformation("Applied Web assembly loggin.");
}


// Log registered auth schemes
{
    var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
    AuthenticationOptions authOptions = app.Services.GetRequiredService<IOptions<AuthenticationOptions>>().Value;
    string slotName = Environment.GetEnvironmentVariable("WEBSITE_SLOT_NAME") ?? "production";
    logger.LogInformation(
        "Authentication schemes registered: {Schemes}",
        string.Join(", ", authOptions.Schemes.Select(s => s.Name)));
    logger.LogInformation(
        "Authentication mode: Environment={Environment} Slot={Slot} UseExternalIdAuth={UseExternalIdAuth} ExternalIdTenantIdSet={TenantSet} ExternalIdClientIdSet={ClientSet}",
        app.Environment.EnvironmentName,
        slotName,
        useExternalIdAuth,
        !string.IsNullOrWhiteSpace(externalIdTenantId),
        !string.IsNullOrWhiteSpace(externalIdClientId));
}

if (openAiOptions.Enabled && !openAiKeyProvider.HasKey)
{
    app.Services.GetRequiredService<ILoggerFactory>()
        .CreateLogger("Startup")
        .LogWarning("OPENAI_API_KEY is not set. OpenAI provider is disabled.");
}

// --------------------
// Middleware
// --------------------

app.UseForwardedHeaders();
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

bool wasmEnabled = app.Configuration.GetValue<bool>("WriterApp:WasmClient:Enabled");
List<string> wasmFrameworkRoots = new();
if (wasmEnabled)
{
    app.UseBlazorFrameworkFiles("/app");

    string[] pathAnchors =
    {
        app.Environment.ContentRootPath,
        Directory.GetCurrentDirectory(),
        AppContext.BaseDirectory,
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."))
    };

    static string ResolveFromAnchors(IEnumerable<string> anchors, params string[] segments)
    {
        foreach (string anchor in anchors)
        {
            string candidate = Path.Combine(new[] { anchor }.Concat(segments).ToArray());
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        return string.Empty;
    }

    // Serve client static files from multiple local locations so /app assets resolve in debug.
    List<string> appAssetRoots = new()
    {
        ResolveFromAnchors(pathAnchors, "WriterApp.Client", "bin", "Debug", "net9.0", "wwwroot"),
        ResolveFromAnchors(pathAnchors, "WriterApp.Client", "bin", "Release", "net9.0", "wwwroot"),
        ResolveFromAnchors(pathAnchors, "WriterApp.Client", "wwwroot")
    };

    foreach (string assetRoot in appAssetRoots.Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.OrdinalIgnoreCase))
    {
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(assetRoot),
            RequestPath = "/app"
        });
    }

    string scopedCssBundleRoot = ResolveFromAnchors(
        pathAnchors,
        "WriterApp.Client",
        "obj",
        app.Environment.IsDevelopment() ? "Debug" : "Release",
        "net9.0",
        "scopedcss",
        "bundle");

    if (!string.IsNullOrWhiteSpace(scopedCssBundleRoot))
    {
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(scopedCssBundleRoot),
            RequestPath = "/app"
        });
    }

    wasmFrameworkRoots = new[]
    {
        ResolveFromAnchors(pathAnchors, "WriterApp.Client", "bin", "Debug", "net9.0", "wwwroot", "_framework"),
        ResolveFromAnchors(pathAnchors, "WriterApp.Client", "bin", "Release", "net9.0", "wwwroot", "_framework")
    }
    .Where(path => !string.IsNullOrWhiteSpace(path))
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToList();
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.Use(async (context, next) =>
{
    string incomingCorrelationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault() ?? string.Empty;
    string correlationId = string.IsNullOrWhiteSpace(incomingCorrelationId)
        ? context.TraceIdentifier
        : incomingCorrelationId.Trim();
    context.Response.Headers["X-Correlation-ID"] = correlationId;

    ILogger logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("ApiRequests");
    bool isApiRequest = context.Request.Path.StartsWithSegments("/api");
    Stopwatch stopwatch = Stopwatch.StartNew();

    using IDisposable? scope = logger.BeginScope(new Dictionary<string, object?>
    {
        ["CorrelationId"] = correlationId,
        ["TraceId"] = context.TraceIdentifier
    });

    try
    {
        await next();

        if (isApiRequest)
        {
            logger.LogInformation(
                "API request completed. Method={Method} Path={Path} StatusCode={StatusCode} DurationMs={DurationMs} CorrelationId={CorrelationId}",
                context.Request.Method,
                context.Request.Path.Value,
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds,
                correlationId);
        }
    }
    catch (Exception ex)
    {
        if (!isApiRequest || context.Response.HasStarted)
        {
            throw;
        }

        if (IsSqliteBusyException(ex))
        {
            logger.LogError(
                ex,
                "API request failed with SQLite busy/locked. Method={Method} Path={Path} DurationMs={DurationMs} CorrelationId={CorrelationId}",
                context.Request.Method,
                context.Request.Path.Value,
                stopwatch.ElapsedMilliseconds,
                correlationId);

            await WriteApiProblemDetailsAsync(
                context,
                StatusCodes.Status503ServiceUnavailable,
                "Database busy",
                "The database is temporarily busy. Please retry shortly.",
                "db.busy",
                correlationId);
            return;
        }

        if (ex is TimeoutException || ex is OperationCanceledException)
        {
            logger.LogError(
                ex,
                "API request timed out. Method={Method} Path={Path} DurationMs={DurationMs} CorrelationId={CorrelationId}",
                context.Request.Method,
                context.Request.Path.Value,
                stopwatch.ElapsedMilliseconds,
                correlationId);

            await WriteApiProblemDetailsAsync(
                context,
                StatusCodes.Status504GatewayTimeout,
                "Request timed out",
                "The request timed out. Please retry.",
                "request.timeout",
                correlationId);
            return;
        }

        logger.LogError(
            ex,
            "Unhandled API exception. Method={Method} Path={Path} DurationMs={DurationMs} CorrelationId={CorrelationId}",
            context.Request.Method,
            context.Request.Path.Value,
            stopwatch.ElapsedMilliseconds,
            correlationId);

        await WriteApiProblemDetailsAsync(
            context,
            StatusCodes.Status500InternalServerError,
            "Server error",
            "An unexpected server error occurred.",
            "server.error",
            correlationId);
    }
});

app.MapGet("/", () => Results.Redirect("/app/documents"));
app.MapGet("/projects/{*path}", (HttpContext context, string? path) =>
{
    string suffix = string.IsNullOrWhiteSpace(path)
        ? string.Empty
        : "/" + path.TrimStart('/');
    string query = context.Request.QueryString.HasValue
        ? context.Request.QueryString.Value ?? string.Empty
        : string.Empty;
    return Results.Redirect($"/app/projects{suffix}{query}");
});
app.MapGet("/login", (HttpContext context) =>
{
    string query = BuildRedirectQueryWithSafeReturnUrl(context, ReturnUrlSafety.DefaultProjectsPath);
    return Results.Redirect($"/app/login{query}", permanent: false);
});
app.MapGet("/start", (HttpContext context) =>
{
    string query = BuildRedirectQueryWithSafeReturnUrl(context, ReturnUrlSafety.DefaultProjectsPath);
    return Results.Redirect($"/app/start{query}", permanent: false);
});
app.MapGet("/billing/checkout", (HttpContext context) =>
{
    string query = BuildRedirectQueryWithSafeReturnUrl(context, ReturnUrlSafety.DefaultProjectsPath);
    return Results.Redirect($"/app/billing/checkout{query}", permanent: false);
});
app.MapGet("/logout", (HttpContext context) =>
{
    string query = BuildRedirectQueryWithSafeReturnUrl(context, ReturnUrlSafety.DefaultHomePath);
    return Results.Redirect($"/app/logout{query}", permanent: false);
});

app.MapGet("/__ping", () => Results.Ok("pong"));
app.MapGet("/healthz", async (AppDbContext dbContext, CancellationToken ct) =>
{
    try
    {
        await dbContext.Database.ExecuteSqlRawAsync("SELECT 1;", ct);
        return Results.Ok(new
        {
            status = "ok",
            provider = dbContext.Database.ProviderName,
            timestamp = DateTimeOffset.UtcNow
        });
    }
    catch (Exception ex)
    {
        Microsoft.AspNetCore.Mvc.ProblemDetails problem = new()
        {
            Title = "Health check failed",
            Detail = ex.Message,
            Status = StatusCodes.Status503ServiceUnavailable
        };
        problem.Extensions["traceId"] = Guid.NewGuid().ToString("n");
        return Results.Json(problem, statusCode: StatusCodes.Status503ServiceUnavailable, contentType: "application/problem+json");
    }
});
app.MapGet("/api/app/version", () =>
{
    string version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown";
    return Results.Ok(new
    {
        version,
        timestamp = DateTimeOffset.UtcNow
    });
});

app.UseAntiforgery();

// --------------------
// API endpoints (MUST come before MapStaticAssets)
// --------------------

app.MapGet("/api/ai/status", async (
        HttpContext context,
        IUserIdResolver userIdResolver,
        IAiUsageStatusService aiUsageStatusService,
        IOptions<WriterAiOptions> aiOptionsAccessor) =>
{
    try
    {
        string userId = userIdResolver.ResolveUserId(context.User);
        AiUsageStatus status = await aiUsageStatusService.GetStatusAsync(userId);
        WriterAiOptions aiOptions = aiOptionsAccessor.Value;

        return Results.Ok(new AiUsageStatusDto
        {
            Plan = status.PlanName,
            AiEnabled = aiOptions.Enabled && status.AiEnabled,
            UiEnabled = aiOptions.Enabled && aiOptions.UI.ShowAiMenu,
            QuotaTotal = status.QuotaTotal,
            QuotaRemaining = status.QuotaRemaining
        });
    }
    catch (SecurityException)
    {
        return Results.Unauthorized();
    }
})
.RequireAuthorization();

// Admin API: use the admin page to call this endpoint; manual calls require the X-MS-CLIENT-PRINCIPAL header.
app.MapPost("/api/admin/users/{userId}/plan/{planKey}", async (
        HttpContext context,
        string userId,
        string planKey,
        IUserEntitlementStore userEntitlementStore,
        IEntitlementService entitlementService,
        AppDbContext dbContext,
        IUserIdResolver userIdResolver,
        ILoggerFactory loggerFactory) =>
{
    if (string.IsNullOrWhiteSpace(userId))
    {
        return Results.BadRequest(new { message = "userId is required." });
    }

    if (string.IsNullOrWhiteSpace(planKey))
    {
        return Results.BadRequest(new { message = "planKey is required." });
    }

    if (!TryParseAdminPlanKey(planKey, out string normalizedPlanKey))
    {
        return Results.BadRequest(new
        {
            message = "planKey must be one of: free, standard, pro, professional.",
            code = "INVALID_PLAN_KEY",
            received = planKey,
            allowed = new[] { "free", "standard", "pro", "professional" }
        });
    }

    bool resetUsage = IsResetUsageRequested(context.Request.Query);

    ILogger logger = loggerFactory.CreateLogger("AdminPlanAssignments");
    string assignedBy = ResolveAssignedBy(context.User, userIdResolver, logger, out string? callerName);

    UserEntitlement entitlement = await userEntitlementStore.GetOrCreateAsync(userId, context.RequestAborted);
    DateTimeOffset now = DateTimeOffset.UtcNow;
    entitlement.PlanKey = normalizedPlanKey;
    entitlement.AiMonthlyTokenBudget = normalizedPlanKey switch
    {
        UserEntitlementDefaults.StandardPlanKey => UserEntitlementDefaults.STANDARD_MONTHLY_TOKEN_BUDGET,
        UserEntitlementDefaults.ProfessionalPlanKey => UserEntitlementDefaults.PROFESSIONAL_MONTHLY_TOKEN_BUDGET,
        _ => UserEntitlementDefaults.FREE_MONTHLY_TOKEN_BUDGET
    };

    if (resetUsage)
    {
        entitlement.AiTokensUsedThisPeriod = 0;
        entitlement.PeriodStartUtc = now;
    }

    entitlement.UpdatedUtc = now;
    await dbContext.SaveChangesAsync(context.RequestAborted);
    entitlementService.InvalidateForUser(userId);

    logger.LogInformation(
        "Admin entitlement updated: userId={UserId} planKey={PlanKey} budget={Budget} used={Used} resetUsage={ResetUsage} assignedBy={AssignedBy} callerName={CallerName}",
        userId,
        entitlement.PlanKey,
        entitlement.AiMonthlyTokenBudget,
        entitlement.AiTokensUsedThisPeriod,
        resetUsage,
        assignedBy,
        callerName ?? string.Empty);

    return Results.Ok(new
    {
        userId = entitlement.UserId,
        planKey = entitlement.PlanKey,
        aiMonthlyTokenBudget = entitlement.AiMonthlyTokenBudget,
        aiTokensUsedThisPeriod = entitlement.AiTokensUsedThisPeriod,
        periodStartUtc = entitlement.PeriodStartUtc
    });
})
.RequireAuthorization("AdminOnly");

app.MapPost("/api/admin/db/migrate", async (
        HttpContext context,
        AppDbContext dbContext,
        IConfiguration configuration,
        ILoggerFactory loggerFactory,
        CancellationToken ct) =>
{
    string? requiredKey = configuration["DB_MIGRATE_KEY"];
    if (!string.IsNullOrWhiteSpace(requiredKey))
    {
        string provided = context.Request.Headers["X-DB-MIGRATE-KEY"].FirstOrDefault() ?? string.Empty;
        byte[] expectedBytes = Encoding.UTF8.GetBytes(requiredKey);
        byte[] providedBytes = Encoding.UTF8.GetBytes(provided);
        if (!CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes))
        {
            return Results.Json(
                new { success = false, message = "Invalid migration key." },
                statusCode: StatusCodes.Status403Forbidden);
        }
    }

    ILogger logger = loggerFactory.CreateLogger("AdminDbMigrate");
    LogSqliteConnectionDetails(dbContext, logger);

    var migration = await ApplyDatabaseMigrationsAsync(dbContext, logger, ct);

    return Results.Ok(new
    {
        success = true,
        pendingBefore = migration.PendingBefore,
        appliedNow = migration.AppliedNow,
        provider = migration.Provider,
        database = migration.Database,
        timestamp = DateTimeOffset.UtcNow
    });
})
.RequireAuthorization("AdminOnly");

app.MapGet("/api/auth/debug", (HttpContext context, ILoggerFactory loggerFactory, IWebHostEnvironment environment) =>
{
    string slotName = Environment.GetEnvironmentVariable("WEBSITE_SLOT_NAME") ?? string.Empty;
    bool isStagingSlot = !string.IsNullOrWhiteSpace(slotName)
        && !string.Equals(slotName, "production", StringComparison.OrdinalIgnoreCase);
    bool authDebugEnabled = environment.IsDevelopment() || isStagingSlot;

    if (!authDebugEnabled)
    {
        return Results.NotFound();
    }

    ILogger logger = loggerFactory.CreateLogger("AuthDebug");
    ClaimsPrincipal user = context.User;

    logger.LogInformation(
        "AuthDebug: IsAuthenticated={Auth}, Scheme={Scheme}, Name={Name}",
        user.Identity?.IsAuthenticated,
        user.Identity?.AuthenticationType,
        user.Identity?.Name);

    foreach (Claim claim in user.Claims)
    {
        logger.LogInformation("Claim: {Type} = {Value}", claim.Type, claim.Value);
    }

    return Results.Ok(new
    {
        IsAuthenticated = user.Identity?.IsAuthenticated,
        user.Identity?.Name,
        Claims = user.Claims.Select(c => new { c.Type, c.Value })
    });
})
.RequireAuthorization();

app.MapGet("/api/auth/me", async (
    HttpContext context,
    AppDbContext dbContext,
    IUserIdResolver userIdResolver,
    IUserEntitlementStore userEntitlementStore,
    ILoggerFactory loggerFactory,
    CancellationToken ct) =>
{
    ILogger logger = loggerFactory.CreateLogger("AuthMe");
    ClaimsPrincipal user = context.User;

    if (user.Identity?.IsAuthenticated != true)
    {
        return Results.Unauthorized();
    }

    string? userId;
    try
    {
        userId = userIdResolver.ResolveUserId(user);
    }
    catch (SecurityException)
    {
        return Results.Unauthorized();
    }

    ExternalIdentityClaims.UserProfileIdentity profileIdentity =
        ExternalIdentityClaims.MapToUserProfileIdentity(user.Claims, userId);

    UserProfile? userProfile = await dbContext.UserProfiles
        .FirstOrDefaultAsync(item => item.UserId == userId, ct);
    bool createdProfile = false;
    if (userProfile is null)
    {
        DateTime now = DateTime.UtcNow;
        userProfile = new UserProfile
        {
            UserId = userId,
            DisplayName = profileIdentity.DisplayName,
            HasOnboarded = false,
            CreatedUtc = now,
            UpdatedUtc = now
        };

        dbContext.UserProfiles.Add(userProfile);
        try
        {
            await dbContext.SaveChangesAsync(ct);
            createdProfile = true;
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            dbContext.Entry(userProfile).State = EntityState.Detached;
            userProfile = await dbContext.UserProfiles
                .FirstOrDefaultAsync(item => item.UserId == userId, ct);
            createdProfile = false;
        }
    }

    bool hadEntitlement = await dbContext.UserEntitlements
        .AsNoTracking()
        .AnyAsync(item => item.UserId == userId, ct);

    List<string> roles = user.FindAll(ClaimTypes.Role)
        .Select(c => c.Value)
        .Concat(user.FindAll("roles").Select(c => c.Value))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    UserEntitlement entitlement = await userEntitlementStore.GetOrCreateAsync(userId, ct);

    if (createdProfile || !hadEntitlement)
    {
        logger.LogInformation(
            "Created new user records. UserId={UserId}, CreatedProfile={CreatedProfile}, CreatedEntitlement={CreatedEntitlement}, Email={Email}, DisplayName={DisplayName}, PlanKey={PlanKey}",
            userId,
            createdProfile,
            !hadEntitlement,
            profileIdentity.Email ?? string.Empty,
            profileIdentity.DisplayName,
            entitlement.PlanKey);
    }
    else
    {
        logger.LogInformation(
            "Existing user login. UserId={UserId}, Email={Email}, DisplayName={DisplayName}, PlanKey={PlanKey}",
            userId,
            profileIdentity.Email ?? string.Empty,
            profileIdentity.DisplayName,
            entitlement.PlanKey);
    }

    return Results.Ok(new WriterApp.Application.Security.AuthMeDto
    {
        IsAuthenticated = true,
        Name = profileIdentity.DisplayName,
        Email = profileIdentity.Email,
        UserId = userId,
        Roles = roles,
        PlanKey = entitlement.PlanKey,
        AiMonthlyTokenBudget = entitlement.AiMonthlyTokenBudget,
        AiTokensUsedThisPeriod = entitlement.AiTokensUsedThisPeriod,
        PeriodStartUtc = entitlement.PeriodStartUtc
    });
})
.RequireAuthorization();

app.MapControllers().RequireAuthorization();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

if (wasmEnabled)
{
    FileExtensionContentTypeProvider contentTypeProvider = new();
    app.MapGet("/app/_framework/{*file}", async context =>
    {
        string relativeFile = context.Request.RouteValues["file"]?.ToString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(relativeFile) || relativeFile.Contains("..", StringComparison.Ordinal))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        foreach (string root in wasmFrameworkRoots)
        {
            string candidate = Path.Combine(root, relativeFile.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(candidate))
            {
                continue;
            }

            if (!contentTypeProvider.TryGetContentType(candidate, out string? contentType))
            {
                contentType = "application/octet-stream";
            }

            context.Response.ContentType = contentType;
            await context.Response.SendFileAsync(candidate);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status404NotFound;
    });

    string runtimeAppIndex = Path.Combine(app.Environment.WebRootPath ?? string.Empty, "app", "index.html");
    string sourceAppIndex = Path.Combine(app.Environment.ContentRootPath, "WriterApp.Client", "wwwroot", "index.html");

    app.MapGet("/app", async context =>
    {
        string? indexPath = ResolveAppIndexPath(runtimeAppIndex, sourceAppIndex);
        if (indexPath is null)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.WriteAsync("Client app index not found.");
            return;
        }

        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.SendFileAsync(indexPath);
    });

    app.MapFallback("/app/{*path:nonfile}", async context =>
    {
        string? indexPath = ResolveAppIndexPath(runtimeAppIndex, sourceAppIndex);
        if (indexPath is null)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.WriteAsync("Client app index not found.");
            return;
        }

        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.SendFileAsync(indexPath);
    });
}

// --------------------
// LAST: static asset fallback
// --------------------

app.MapStaticAssets();

app.Run();

static string ResolveAssignedBy(
    ClaimsPrincipal user,
    IUserIdResolver userIdResolver,
    ILogger logger,
    out string? callerName)
{
    callerName = user.Identity?.Name;
    try
    {
        return userIdResolver.ResolveUserId(user);
    }
    catch (SecurityException ex)
    {
        logger.LogWarning(ex, "Admin assignment missing oid claim.");
        return callerName ?? "admin";
    }
}

static bool TryParseAdminPlanKey(string input, out string normalizedPlanKey)
{
    normalizedPlanKey = UserEntitlementDefaults.FreePlanKey;
    if (string.IsNullOrWhiteSpace(input))
    {
        return false;
    }

    string value = input.Trim();
    if (value.Equals("free", StringComparison.OrdinalIgnoreCase))
    {
        normalizedPlanKey = UserEntitlementDefaults.FreePlanKey;
        return true;
    }

    if (value.Equals("standard", StringComparison.OrdinalIgnoreCase))
    {
        normalizedPlanKey = UserEntitlementDefaults.StandardPlanKey;
        return true;
    }

    if (value.Equals("pro", StringComparison.OrdinalIgnoreCase)
        || value.Equals("professional", StringComparison.OrdinalIgnoreCase))
    {
        normalizedPlanKey = UserEntitlementDefaults.ProfessionalPlanKey;
        return true;
    }

    return false;
}

static bool IsResetUsageRequested(IQueryCollection query)
{
    if (!query.TryGetValue("resetUsage", out StringValues resetUsageValues))
    {
        return false;
    }

    string raw = resetUsageValues.ToString();
    if (bool.TryParse(raw, out bool parsed))
    {
        return parsed;
    }

    return string.Equals(raw, "1", StringComparison.OrdinalIgnoreCase);
}

static string BuildRedirectQueryWithSafeReturnUrl(HttpContext context, string fallback)
{
    QueryString queryString = context.Request.QueryString;
    if (!queryString.HasValue)
    {
        return string.Empty;
    }

    Dictionary<string, StringValues> parsed = QueryHelpers.ParseQuery(queryString.Value ?? string.Empty)
        .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);

    if (!parsed.TryGetValue(ReturnUrlSafety.ReturnUrlKey, out StringValues rawReturnUrl))
    {
        return queryString.Value ?? string.Empty;
    }

    string safeReturnUrl = ReturnUrlSafety.NormalizeOrFallback(rawReturnUrl.FirstOrDefault(), fallback);
    QueryBuilder builder = new();
    foreach ((string key, StringValues values) in parsed)
    {
        if (string.Equals(key, ReturnUrlSafety.ReturnUrlKey, StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        if (values.Count == 0)
        {
            builder.Add(key, string.Empty);
            continue;
        }

        foreach (string? value in values)
        {
            builder.Add(key, value ?? string.Empty);
        }
    }

    builder.Add(ReturnUrlSafety.ReturnUrlKey, safeReturnUrl);
    return builder.ToQueryString().Value ?? string.Empty;
}

static void LogRuntimeProbe(ILogger logger)
{
    try
    {
        logger.LogInformation(
            "Runtime probe: OS={OSDescription} Framework={Framework} ProcessArch={ProcessArch} OSArch={OSArch}",
            RuntimeInformation.OSDescription,
            RuntimeInformation.FrameworkDescription,
            RuntimeInformation.ProcessArchitecture,
            RuntimeInformation.OSArchitecture);

        string baseDir = AppContext.BaseDirectory;
        logger.LogInformation("Runtime probe: BaseDirectory={BaseDirectory}", baseDir);

        if (!Directory.Exists(baseDir))
        {
            logger.LogWarning("Runtime probe: BaseDirectory does not exist.");
            return;
        }

        string runtimesDir = Path.Combine(baseDir, "runtimes");
        if (!Directory.Exists(runtimesDir))
        {
            logger.LogWarning("Runtime probe: No runtimes directory found.");
            return;
        }

        foreach (string file in Directory.EnumerateFiles(runtimesDir, "*e_sqlite3*", SearchOption.AllDirectories))
        {
            LogFilePresence(logger, baseDir, file);
        }

        foreach (string file in Directory.EnumerateFiles(runtimesDir, "*libe_sqlite3*", SearchOption.AllDirectories))
        {
            LogFilePresence(logger, baseDir, file);
        }

        foreach (string file in Directory.EnumerateFiles(runtimesDir, "*", SearchOption.AllDirectories))
        {
            if (!file.Contains($"{Path.DirectorySeparatorChar}native{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (file.Contains($"{Path.DirectorySeparatorChar}runtimes{Path.DirectorySeparatorChar}win", StringComparison.OrdinalIgnoreCase)
                || file.Contains($"{Path.DirectorySeparatorChar}runtimes{Path.DirectorySeparatorChar}linux", StringComparison.OrdinalIgnoreCase))
            {
                LogFilePresence(logger, baseDir, file);
            }
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Runtime probe failed.");
    }
}

static void LogFilePresence(ILogger logger, string baseDir, string filePath)
{
    try
    {
        long size = new FileInfo(filePath).Length;
        string relative = Path.GetRelativePath(baseDir, filePath);
        logger.LogInformation("Runtime probe: native file {File} ({Size} bytes)", relative, size);
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Runtime probe: failed to read file info for {File}", filePath);
    }
}

static void ProbeSqlite(ILogger logger)
{
    try
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "select 1;";
        _ = command.ExecuteScalar();
        logger.LogInformation("SQLite probe succeeded.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "SQLite probe failed.");
        LogExceptionChain(logger, ex);
    }
}

static void ApplySqlitePragmas(AppDbContext dbContext, ILogger logger)
{
    try
    {
        dbContext.Database.OpenConnection();
        dbContext.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");
        dbContext.Database.ExecuteSqlRaw("PRAGMA synchronous=NORMAL;");
        dbContext.Database.ExecuteSqlRaw("PRAGMA foreign_keys=ON;");
        dbContext.Database.ExecuteSqlRaw("PRAGMA busy_timeout=5000;");
        logger.LogInformation("SQLite pragmas applied.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to apply SQLite pragmas.");
    }
    finally
    {
        dbContext.Database.CloseConnection();
    }
}

static async Task<(string Provider, string Database, string[] PendingBefore, string[] AppliedNow, string[] AppliedAfter)> ApplyDatabaseMigrationsAsync(
    AppDbContext dbContext,
    ILogger logger,
    CancellationToken ct)
{
    string provider = dbContext.Database.ProviderName ?? "unknown";
    DbConnection connection = dbContext.Database.GetDbConnection();
    string database = connection.Database ?? string.Empty;
    string redactedConnectionString = RedactConnectionString(connection.ConnectionString ?? string.Empty);

    logger.LogInformation(
        "Starting EF migrations. Provider={Provider}, Database={Database}, Connection={ConnectionString}",
        provider,
        database,
        redactedConnectionString);

    string[] pendingBefore = (await dbContext.Database.GetPendingMigrationsAsync(ct)).ToArray();
    string[] appliedBefore = (await dbContext.Database.GetAppliedMigrationsAsync(ct)).ToArray();

    logger.LogInformation(
        "EF migrations pending before apply. Count={Count}. Pending=[{Pending}]",
        pendingBefore.Length,
        string.Join(", ", pendingBefore));

    try
    {
        await dbContext.Database.MigrateAsync(ct);
    }
    catch (SqliteException ex) when (
        ex.SqliteErrorCode == 5
        || ex.SqliteErrorCode == 6
        || ex.Message.Contains("database is locked", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("database is busy", StringComparison.OrdinalIgnoreCase))
    {
        logger.LogCritical(ex, "Database migration failed because SQLite database is locked/busy.");
        throw;
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Database migration failed unexpectedly.");
        throw;
    }

    string[] appliedAfter = (await dbContext.Database.GetAppliedMigrationsAsync(ct)).ToArray();
    string[] appliedNow = appliedAfter.Except(appliedBefore, StringComparer.Ordinal).ToArray();

    logger.LogInformation(
        "EF migrations completed. AppliedNowCount={Count}. AppliedNow=[{Applied}]",
        appliedNow.Length,
        string.Join(", ", appliedNow));

    return (
        provider,
        database,
        pendingBefore,
        appliedNow,
        appliedAfter);
}

static string RedactConnectionString(string connectionString)
{
    try
    {
        DbConnectionStringBuilder builder = new()
        {
            ConnectionString = connectionString
        };

        string[] sensitiveKeys = { "Password", "Pwd", "User ID", "UID" };
        foreach (string key in sensitiveKeys)
        {
            if (builder.ContainsKey(key))
            {
                builder[key] = "***";
            }
        }

        return builder.ConnectionString;
    }
    catch
    {
        return connectionString;
    }
}

static void LogTablePresence(AppDbContext dbContext, ILogger logger, string tableName)
{
    try
    {
        dbContext.Database.OpenConnection();
        using var command = dbContext.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name = $tableName;";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "$tableName";
        parameter.Value = tableName;
        command.Parameters.Add(parameter);
        object? result = command.ExecuteScalar();
        if (result is null)
        {
            logger.LogWarning("SQLite table check: {Table} not found.", tableName);
        }
        else
        {
            logger.LogInformation("SQLite table check: {Table} exists.", tableName);
        }
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "SQLite table check failed for {Table}.", tableName);
    }
    finally
    {
        dbContext.Database.CloseConnection();
    }
}

static void LogExceptionChain(ILogger logger, Exception ex)
{
    Exception? current = ex.InnerException;
    int depth = 0;
    while (current is not null && depth < 8)
    {
        logger.LogError(current, "SQLite probe inner exception depth {Depth}.", depth + 1);
        current = current.InnerException;
        depth++;
    }
}

static void LogSqliteConnectionDetails(AppDbContext dbContext, ILogger logger)
{
    try
    {
        if (!dbContext.Database.IsSqlite())
        {
            return;
        }

        DbConnection connection = dbContext.Database.GetDbConnection();
        string connectionString = connection.ConnectionString ?? string.Empty;
        SqliteConnectionStringBuilder builder = new(connectionString);
        string dataSource = builder.DataSource ?? string.Empty;
        string resolvedPath = string.IsNullOrWhiteSpace(dataSource)
            ? string.Empty
            : (Path.IsPathRooted(dataSource)
                ? dataSource
                : Path.GetFullPath(dataSource));

        logger.LogInformation(
            "SQLite target. DataSource={DataSource}; ResolvedPath={ResolvedPath}; ConnectionString={ConnectionString}",
            dataSource,
            resolvedPath,
            connectionString);
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Failed to log SQLite connection details.");
    }
}

static void LogSqliteTables(AppDbContext dbContext, ILogger logger, string phase)
{
    if (!dbContext.Database.IsSqlite())
    {
        return;
    }

    try
    {
        dbContext.Database.OpenConnection();
        using var command = dbContext.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' ORDER BY name;";
        using DbDataReader reader = command.ExecuteReader();
        List<string> tables = new();
        while (reader.Read())
        {
            if (!reader.IsDBNull(0))
            {
                tables.Add(reader.GetString(0));
            }
        }

        logger.LogInformation("SQLite tables ({Phase}): [{Tables}]", phase, string.Join(", ", tables));
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Failed to list SQLite tables during {Phase}.", phase);
    }
    finally
    {
        dbContext.Database.CloseConnection();
    }
}

static void LogSchemaHistoryMismatchWarning(AppDbContext dbContext, ILogger logger)
{
    if (!dbContext.Database.IsSqlite())
    {
        return;
    }

    try
    {
        dbContext.Database.OpenConnection();
        using DbConnection connection = dbContext.Database.GetDbConnection();

        using DbCommand historyCommand = connection.CreateCommand();
        historyCommand.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = '__EFMigrationsHistory';";
        int hasHistoryTable = Convert.ToInt32(historyCommand.ExecuteScalar(), CultureInfo.InvariantCulture);

        using DbCommand projectsCommand = connection.CreateCommand();
        projectsCommand.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'Projects';";
        int hasProjectsTable = Convert.ToInt32(projectsCommand.ExecuteScalar(), CultureInfo.InvariantCulture);

        if (hasHistoryTable > 0 && hasProjectsTable == 0)
        {
            logger.LogWarning("Schema mismatch detected: migrations history present but Projects table missing. Attempting self-heal.");
        }
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Failed to evaluate schema/history mismatch warning.");
    }
    finally
    {
        dbContext.Database.CloseConnection();
    }
}

static string ResolveSqliteConnectionString(IConfiguration configuration, IHostEnvironment environment)
{
    const string defaultProdPath = "/home/site/data/writerapp.db";
    string? configured = configuration.GetConnectionString("DefaultConnection");
    string fallback = environment.IsDevelopment()
        ? "Data Source=writerapp.db"
        : $"Data Source={defaultProdPath}";

    string baseConnection = string.IsNullOrWhiteSpace(configured) ? fallback : configured;
    SqliteConnectionStringBuilder sqliteBuilder = new(baseConnection);

    string dataSource = sqliteBuilder.DataSource ?? string.Empty;
    if (environment.IsDevelopment())
    {
        if (!string.IsNullOrWhiteSpace(dataSource) && !Path.IsPathRooted(dataSource))
        {
            sqliteBuilder.DataSource = Path.Combine(environment.ContentRootPath, dataSource);
        }
    }
    else
    {
        string normalized = dataSource.Replace('\\', '/');
        bool pointsAtWwwroot = normalized.StartsWith("/home/site/wwwroot", StringComparison.OrdinalIgnoreCase);
        bool rootedInHome = normalized.StartsWith("/home/", StringComparison.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(dataSource) || pointsAtWwwroot || !rootedInHome)
        {
            sqliteBuilder.DataSource = defaultProdPath;
        }
        else if (!Path.IsPathRooted(dataSource))
        {
            sqliteBuilder.DataSource = defaultProdPath;
        }
    }

    string resolvedDataSource = sqliteBuilder.DataSource ?? string.Empty;
    if (!string.IsNullOrWhiteSpace(resolvedDataSource))
    {
        string? directory = Path.GetDirectoryName(resolvedDataSource);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    if (sqliteBuilder.DefaultTimeout <= 0)
    {
        sqliteBuilder.DefaultTimeout = 30;
    }

    return sqliteBuilder.ToString();
}

static string? ResolveAppIndexPath(string runtimeAppIndex, string sourceAppIndex)
{
    if (File.Exists(runtimeAppIndex))
    {
        return runtimeAppIndex;
    }

    if (File.Exists(sourceAppIndex))
    {
        return sourceAppIndex;
    }

    return null;
}

static bool IsSqliteBusyException(Exception ex)
{
    if (ex is SqliteException sqliteEx)
    {
        return sqliteEx.SqliteErrorCode == 5
               || sqliteEx.SqliteErrorCode == 6
               || sqliteEx.Message.Contains("database is locked", StringComparison.OrdinalIgnoreCase)
               || sqliteEx.Message.Contains("database is busy", StringComparison.OrdinalIgnoreCase);
    }

    Exception? inner = ex.InnerException;
    while (inner is not null)
    {
        if (inner is SqliteException innerSqlite
            && (innerSqlite.SqliteErrorCode == 5
                || innerSqlite.SqliteErrorCode == 6
                || innerSqlite.Message.Contains("database is locked", StringComparison.OrdinalIgnoreCase)
                || innerSqlite.Message.Contains("database is busy", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        inner = inner.InnerException;
    }

    return false;
}

static bool IsUniqueConstraintViolation(DbUpdateException ex)
{
    if (ex.InnerException is SqliteException sqliteEx)
    {
        return sqliteEx.SqliteErrorCode == 19
               || sqliteEx.Message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase);
    }

    return ex.InnerException?.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase) == true
           || ex.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase);
}

static async Task WriteApiProblemDetailsAsync(
    HttpContext context,
    int statusCode,
    string title,
    string detail,
    string code,
    string correlationId)
{
    context.Response.StatusCode = statusCode;
    context.Response.ContentType = "application/problem+json";
    var problem = new Microsoft.AspNetCore.Mvc.ProblemDetails
    {
        Status = statusCode,
        Title = title,
        Detail = detail
    };
    problem.Extensions["code"] = code;
    problem.Extensions["traceId"] = context.TraceIdentifier;
    problem.Extensions["correlationId"] = correlationId;
    await context.Response.WriteAsJsonAsync(problem);
}
