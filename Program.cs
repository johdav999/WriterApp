
using BlazorApp.Components;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using System.IO;
using System.Security;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
using WriterApp.Application.Billing;
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
using WriterApp.Application.Users;
using WriterApp.Data;
using WriterApp.Data.Subscriptions;
using WriterApp.Shared;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddFilter("Microsoft.AspNetCore.Components.Server.Circuits", LogLevel.Information);
builder.Logging.AddFilter(
    "Microsoft.AspNetCore.SignalR",
    builder.Environment.IsDevelopment() ? LogLevel.Debug : LogLevel.Information);

StripeConfigurationResult stripeConfiguration = StripeOptions.Load(
    builder.Configuration,
    builder.Environment.IsDevelopment());

if (stripeConfiguration.Errors.Count > 0)
{
    throw new InvalidOperationException(
        "Stripe startup configuration failed:" + Environment.NewLine + string.Join(Environment.NewLine, stripeConfiguration.Errors));
}

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

string databaseProvider = builder.Configuration["DatabaseProvider"]?.Trim() ?? "Sqlite";
bool useSqlServerProvider = string.Equals(databaseProvider, "SqlServer", StringComparison.OrdinalIgnoreCase);
bool useSqliteProvider = !useSqlServerProvider;

if (useSqlServerProvider)
{
    string connectionString = ResolveSqlServerConnectionString(builder.Configuration)
        ?? throw new InvalidOperationException(
            "DatabaseProvider is set to SqlServer, but no SQL Server connection string was found. Set ConnectionStrings:SqlServer or DefaultConnection.");

    builder.Services.AddDbContext<SqlServerMigrationsDbContext>(options =>
    {
        options.UseSqlServer(connectionString, sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorNumbersToAdd: null);
        });
    });

    builder.Services.AddScoped<AppDbContext>(sp => sp.GetRequiredService<SqlServerMigrationsDbContext>());
}
else
{
    builder.Services.AddDbContext<AppDbContext>(options =>
    {
        string connectionString = ResolveSqliteConnectionString(builder.Configuration, builder.Environment);
        options.UseSqlite(connectionString);
    });
}

if (builder.Environment.IsDevelopment())
{
    // Local development auth for onboarding/auth-gated flows without EasyAuth.
    builder.Services.AddAuthentication(LocalDevAuthenticationHandler.SchemeName)
        .AddScheme<AuthenticationSchemeOptions, LocalDevAuthenticationHandler>(
            LocalDevAuthenticationHandler.SchemeName,
            _ => { });
}
else
{
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
}
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
var mvcBuilder = builder.Services.AddControllers();
mvcBuilder.ConfigureApplicationPartManager(manager =>
{
    manager.ApplicationParts.Clear();
    manager.ApplicationParts.Add(new AssemblyPart(typeof(Program).Assembly));
});
builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped(sp =>
{
    NavigationManager navigation = sp.GetRequiredService<NavigationManager>();
    return new HttpClient { BaseAddress = new Uri(navigation.BaseUri) };
});
builder.Services.AddScoped<OutlineTemplatesClient>();
builder.Services.AddSingleton(stripeConfiguration.Options);
builder.Services.AddSingleton<StripeApiClient>();
builder.Services.AddScoped<StripeEntitlementSyncService>();
builder.Services.AddScoped<IStripeClientFacade, StripeClientFacade>();
builder.Services.Configure<StripeBillingOptions>(builder.Configuration.GetSection("Stripe:Billing"));
builder.Services.AddScoped<IStripePriceResolver, StripePriceResolver>();

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
builder.Services.AddScoped<AdminPlanOverrideService>();
builder.Services.AddScoped<AdminAuditService>();
builder.Services.AddScoped<UserEventService>();
builder.Services.AddScoped<IUserLookupService, UserLookupService>();
builder.Services.AddScoped<AdminUsersService>();
builder.Services.AddSingleton<AdminEndpointRateLimiter>();
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
builder.Services.AddScoped<WriterApp.Application.Exporting.IOutlineOrderResolver, WriterApp.Application.Exporting.OutlineOrderResolver>();
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

if (!app.Environment.IsDevelopment())
{
    AppDomain.CurrentDomain.FirstChanceException += (_, eventArgs) =>
    {
        if (eventArgs.Exception is BadImageFormatException badImageFormatException)
        {
            app.Logger.LogError(
                badImageFormatException,
                "First-chance BadImageFormatException observed during production startup/request pipeline. Message={Message}",
                badImageFormatException.Message);
        }
    };
}

QualityRewriteOutputValidator.Configure(app.Services.GetRequiredService<IOptions<QualityRewriteValidationOptions>>().Value);

AdminPolicyDiagnostics.Configure(app.Services.GetRequiredService<ILoggerFactory>());

foreach (string warning in stripeConfiguration.Warnings)
{
    app.Logger.LogWarning("{Warning}", warning);
}

app.Logger.LogInformation(
    "Stripe configuration loaded. Enabled={Enabled}, Mode={Mode}, WebhookConfigured={WebhookConfigured}, StandardPriceConfigured={StandardPriceConfigured}, ProPriceConfigured={ProPriceConfigured}.",
    stripeConfiguration.Options.Enabled,
    stripeConfiguration.Options.Mode,
    !string.IsNullOrWhiteSpace(stripeConfiguration.Options.WebhookSecret),
    !string.IsNullOrWhiteSpace(stripeConfiguration.Options.PriceStandard),
    !string.IsNullOrWhiteSpace(stripeConfiguration.Options.PricePro));

StripeBillingOptions stripeBillingOptions = app.Services.GetRequiredService<IOptions<StripeBillingOptions>>().Value;
app.Logger.LogInformation(
    "Stripe billing mode configured. Mode={Mode}, ApiKeyPresent={ApiKeyPresent}, StandardPriceConfigured={StandardConfigured}, ProPriceConfigured={ProConfigured}.",
    stripeBillingOptions.Mode,
    !string.IsNullOrWhiteSpace(stripeBillingOptions.ApiKey),
    !string.IsNullOrWhiteSpace(stripeBillingOptions.Prices.Standard.LivePriceId) || !string.IsNullOrWhiteSpace(stripeBillingOptions.Prices.Standard.TestPriceId),
    !string.IsNullOrWhiteSpace(stripeBillingOptions.Prices.Pro.LivePriceId) || !string.IsNullOrWhiteSpace(stripeBillingOptions.Prices.Pro.TestPriceId));
if (string.IsNullOrWhiteSpace(stripeBillingOptions.ApiKey))
{
    app.Logger.LogWarning("Stripe checkout endpoints are disabled because Stripe:Billing:ApiKey is not configured.");
}

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

    AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (dbContext.Database.IsSqlServer())
    {
        await WarmUpSqlServerConnectionAsync(dbContext, logger, CancellationToken.None);
    }
    if (dbContext.Database.IsSqlite())
    {
        ProbeSqlite(logger);
    }
    LogSqliteConnectionDetails(dbContext, logger);

    bool diagnosticsDb =
        app.Environment.IsDevelopment()
        || app.Configuration.GetValue<bool?>("DIAGNOSTICS_DB") == true;
    if (diagnosticsDb && dbContext.Database.IsSqlite())
    {
        LogSqliteTables(dbContext, logger, "pre-migrate");
    }

    LogSchemaHistoryMismatchWarning(dbContext, logger);

    bool autoMigrateOnStartup =
        app.Configuration.GetValue<bool?>("WriterApp:Database:AutoMigrateOnStartup")
        ?? app.Configuration.GetValue<bool?>("AUTO_MIGRATE")
        ?? false;
    if (autoMigrateOnStartup)
    {
        await ApplyDatabaseMigrationsAsync(dbContext, logger, CancellationToken.None);
        logger.LogInformation("Migrations ok; schema up to date.");
    }
    else
    {
        logger.LogInformation("Database auto-migrate on startup is disabled. Skipping EF migrations.");
    }

    bool adminApiEnabled = app.Configuration.GetValue<bool?>("Admin:EnableAdminApi") ?? false;
    if (adminApiEnabled)
    {
        string[] appliedMigrations = (await dbContext.Database.GetAppliedMigrationsAsync(CancellationToken.None)).ToArray();
        string[] pendingMigrations = (await dbContext.Database.GetPendingMigrationsAsync(CancellationToken.None)).ToArray();
        string currentMigration = appliedMigrations.LastOrDefault() ?? "(none)";
        logger.LogInformation(
            "Admin API migration check. CurrentMigration={CurrentMigration}, PendingCount={PendingCount}, Pending=[{Pending}]",
            currentMigration,
            pendingMigrations.Length,
            string.Join(", ", pendingMigrations));
    }

    if (diagnosticsDb && dbContext.Database.IsSqlite())
    {
        LogTablePresence(dbContext, logger, "PageVersions");
        LogTablePresence(dbContext, logger, "OutlineTemplates");
        LogRequiredSqliteColumns(dbContext, logger, "SectionSceneCards", new[]
        {
            "PlaceId",
            "PovCharacterId",
            "TimelineEventId",
            "TimeRef",
            "TagsJson",
            "ReferencesJson"
        });
        LogRequiredSqliteColumns(dbContext, logger, "DocumentOutlineNodes", new[] { "MetadataJson" });
        LogSqliteTables(dbContext, logger, "post-migrate");
    }

    bool goalsEnabled =
        app.Configuration.GetValue<bool?>("Workflow:GoalsEnabled")
        ?? app.Configuration.GetValue<bool?>("WriterApp:Workflow:GoalsEnabled")
        ?? false;
    if (goalsEnabled && dbContext.Database.IsSqlite())
    {
        bool hasProjectGoals = SqliteTableExists(dbContext, "ProjectGoals");
        bool hasProjectProgressDaily = SqliteTableExists(dbContext, "ProjectProgressDaily");
        if (!hasProjectGoals || !hasProjectProgressDaily)
        {
            logger.LogWarning(
                "Goals feature is enabled, but required tables are missing. ProjectGoalsExists={ProjectGoalsExists}, ProjectProgressDailyExists={ProjectProgressDailyExists}. Run database migrations.",
                hasProjectGoals,
                hasProjectProgressDaily);
        }
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

    if (dbContext.Database.IsSqlite())
    {
        ApplySqlitePragmas(dbContext, logger);
    }

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
    bool isAuthMeRequest = context.Request.Path.Equals("/api/auth/me", StringComparison.OrdinalIgnoreCase);
    bool enableAuthMeDiagnostics = app.Environment.IsProduction() && isAuthMeRequest;
    Stopwatch stopwatch = Stopwatch.StartNew();

    using IDisposable? scope = logger.BeginScope(new Dictionary<string, object?>
    {
        ["CorrelationId"] = correlationId,
        ["TraceId"] = context.TraceIdentifier
    });

    try
    {
        if (enableAuthMeDiagnostics)
        {
            string[] claimTypesBefore = context.User?.Claims
                .Select(claim => claim.Type)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(type => type, StringComparer.Ordinal)
                .Take(24)
                .ToArray() ?? Array.Empty<string>();
            string? authorizationHeader = context.Request.Headers.Authorization.FirstOrDefault();
            string authorizationScheme = string.IsNullOrWhiteSpace(authorizationHeader)
                ? string.Empty
                : authorizationHeader.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;

            logger.LogInformation(
                "AuthMe diagnostics begin. Method={Method} Path={Path} Query={Query} Authenticated={Authenticated} AuthType={AuthType} Claims={ClaimTypes} XMsClientPrincipalLength={XMsClientPrincipalLength} XMsAadIdTokenLength={XMsAadIdTokenLength} AuthorizationScheme={AuthorizationScheme} AuthorizationLength={AuthorizationLength} CookieLength={CookieLength}",
                context.Request.Method,
                context.Request.Path.Value,
                context.Request.QueryString.Value ?? string.Empty,
                context.User?.Identity?.IsAuthenticated ?? false,
                context.User?.Identity?.AuthenticationType ?? string.Empty,
                claimTypesBefore,
                context.Request.Headers["X-MS-CLIENT-PRINCIPAL"].ToString().Length,
                context.Request.Headers["X-MS-TOKEN-AAD-ID-TOKEN"].ToString().Length,
                authorizationScheme,
                authorizationHeader?.Length ?? 0,
                context.Request.Headers.Cookie.ToString().Length);
        }

        await next();

        if (enableAuthMeDiagnostics)
        {
            bool handlerEntered = context.Items.TryGetValue("AuthMeHandlerEntered", out object? marker)
                && marker is true;
            logger.LogInformation(
                "AuthMe diagnostics end. StatusCode={StatusCode} DurationMs={DurationMs} HandlerEntered={HandlerEntered}",
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds,
                handlerEntered);
        }

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

        if (ex is EntitlementDeniedException entitlementDenied)
        {
            Microsoft.AspNetCore.Mvc.ProblemDetails payload = EntitlementDeniedApiError.ToProblemDetails(entitlementDenied);
            payload.Extensions["code"] = "entitlement_denied";
            payload.Extensions["traceId"] = context.TraceIdentifier;
            payload.Extensions["correlationId"] = correlationId;
            logger.LogInformation(
                "API request denied by entitlement. Method={Method} Path={Path} DurationMs={DurationMs} CorrelationId={CorrelationId} FeatureKey={FeatureKey} PlanKey={PlanKey}",
                context.Request.Method,
                context.Request.Path.Value,
                stopwatch.ElapsedMilliseconds,
                correlationId,
                entitlementDenied.FeatureKey,
                entitlementDenied.PlanKey ?? string.Empty);

            context.Response.StatusCode = StatusCodes.Status402PaymentRequired;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(payload);
            return;
        }

        if (useSqliteProvider && IsSqliteBusyException(ex))
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
app.MapGet("/upgrade", (HttpContext context) =>
{
    string query = context.Request.QueryString.HasValue
        ? context.Request.QueryString.Value ?? string.Empty
        : string.Empty;
    return Results.Redirect($"/app/upgrade{query}", permanent: false);
});
app.MapGet("/upgrade/{*rest}", (HttpContext context, string? rest) =>
{
    string suffix = string.IsNullOrWhiteSpace(rest)
        ? string.Empty
        : "/" + rest.TrimStart('/');
    string query = context.Request.QueryString.HasValue
        ? context.Request.QueryString.Value ?? string.Empty
        : string.Empty;
    return Results.Redirect($"/app/upgrade{suffix}{query}", permanent: false);
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

app.MapGet("/api/admin/users", async (
        HttpContext context,
        IConfiguration configuration,
        IUserIdResolver userIdResolver,
        AdminEndpointRateLimiter adminRateLimiter,
        AdminUsersService adminUsersService,
        ILoggerFactory loggerFactory,
        int page = 1,
        int pageSize = 20,
        string? q = null,
        string? planKey = null,
        bool overrideOnly = false,
        string? subscriptionStatus = null,
        int? tokensLeftLt = null,
        int? tokensLeftGt = null,
        string? sort = null) =>
{
    if (!AdminPlanOverrideAccess.IsAdminApiEnabled(configuration)
        || !AdminPlanOverrideAccess.IsAuthorized(context.User, configuration))
    {
        return Results.NotFound();
    }

    ILogger logger = loggerFactory.CreateLogger("AdminUsers");
    string adminUserId = ResolveAssignedBy(context.User, userIdResolver, logger, out _);
    if (!adminRateLimiter.TryAcquire($"{adminUserId}:list-users", 120))
    {
        return Results.StatusCode(StatusCodes.Status429TooManyRequests);
    }

    AdminUserListResponseDto response = await adminUsersService.QueryUsersAsync(
        page,
        pageSize,
        q,
        planKey,
        overrideOnly,
        subscriptionStatus,
        tokensLeftLt,
        tokensLeftGt,
        sort,
        context.RequestAborted);
    return Results.Ok(response);
});

app.MapGet("/api/admin/users/{userId}", async (
        HttpContext context,
        string userId,
        IConfiguration configuration,
        IUserIdResolver userIdResolver,
        AdminEndpointRateLimiter adminRateLimiter,
        AdminUsersService adminUsersService,
        ILoggerFactory loggerFactory) =>
{
    if (!AdminPlanOverrideAccess.IsAdminApiEnabled(configuration)
        || !AdminPlanOverrideAccess.IsAuthorized(context.User, configuration))
    {
        return Results.NotFound();
    }

    ILogger logger = loggerFactory.CreateLogger("AdminUsers");
    string adminUserId = ResolveAssignedBy(context.User, userIdResolver, logger, out _);
    if (!adminRateLimiter.TryAcquire($"{adminUserId}:get-user", 240))
    {
        return Results.StatusCode(StatusCodes.Status429TooManyRequests);
    }

    AdminUserDetailDto? user = await adminUsersService.GetUserAsync(userId, context.RequestAborted);
    return user is null ? Results.NotFound() : Results.Ok(user);
});

app.MapPost("/api/admin/users", async (
        HttpContext context,
        AdminCreateUserRequest request,
        IConfiguration configuration,
        IUserIdResolver userIdResolver,
        AdminEndpointRateLimiter adminRateLimiter,
        AdminUsersService adminUsersService,
        ILoggerFactory loggerFactory) =>
{
    if (!AdminPlanOverrideAccess.IsAdminApiEnabled(configuration)
        || !AdminPlanOverrideAccess.IsAuthorized(context.User, configuration))
    {
        return Results.NotFound();
    }

    ILogger logger = loggerFactory.CreateLogger("AdminUsers");
    string adminUserId = ResolveAssignedBy(context.User, userIdResolver, logger, out _);
    string? adminEmail = context.User.FindFirstValue(ClaimTypes.Email)
        ?? context.User.FindFirstValue("emails")
        ?? context.User.FindFirstValue("preferred_username")
        ?? context.User.Identity?.Name;
    if (!adminRateLimiter.TryAcquire($"{adminUserId}:create-user", 60))
    {
        return Results.StatusCode(StatusCodes.Status429TooManyRequests);
    }

    try
    {
        AdminUserDetailDto created = await adminUsersService.CreateUserAsync(
            request,
            adminUserId,
            adminEmail,
            context.RequestAborted);
        return Results.Ok(created);
    }
    catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
});

app.MapPut("/api/admin/users/{userId}", async (
        HttpContext context,
        string userId,
        AdminUpdateUserRequest request,
        IConfiguration configuration,
        IUserIdResolver userIdResolver,
        AdminEndpointRateLimiter adminRateLimiter,
        AdminUsersService adminUsersService,
        ILoggerFactory loggerFactory) =>
{
    if (!AdminPlanOverrideAccess.IsAdminApiEnabled(configuration)
        || !AdminPlanOverrideAccess.IsAuthorized(context.User, configuration))
    {
        return Results.NotFound();
    }

    ILogger logger = loggerFactory.CreateLogger("AdminUsers");
    string adminUserId = ResolveAssignedBy(context.User, userIdResolver, logger, out _);
    string? adminEmail = context.User.FindFirstValue(ClaimTypes.Email)
        ?? context.User.FindFirstValue("emails")
        ?? context.User.FindFirstValue("preferred_username")
        ?? context.User.Identity?.Name;
    if (!adminRateLimiter.TryAcquire($"{adminUserId}:update-user", 120))
    {
        return Results.StatusCode(StatusCodes.Status429TooManyRequests);
    }

    try
    {
        AdminUserDetailDto? updated = await adminUsersService.UpdateUserAsync(
            userId,
            request,
            adminUserId,
            adminEmail,
            context.RequestAborted);
        return updated is null ? Results.NotFound() : Results.Ok(updated);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
});

app.MapDelete("/api/admin/users/{userId}", async (
        HttpContext context,
        string userId,
        IConfiguration configuration,
        IUserIdResolver userIdResolver,
        AdminEndpointRateLimiter adminRateLimiter,
        AdminUsersService adminUsersService,
        ILoggerFactory loggerFactory) =>
{
    if (!AdminPlanOverrideAccess.IsAdminApiEnabled(configuration)
        || !AdminPlanOverrideAccess.IsAuthorized(context.User, configuration))
    {
        return Results.NotFound();
    }

    ILogger logger = loggerFactory.CreateLogger("AdminUsers");
    string adminUserId = ResolveAssignedBy(context.User, userIdResolver, logger, out _);
    string? adminEmail = context.User.FindFirstValue(ClaimTypes.Email)
        ?? context.User.FindFirstValue("emails")
        ?? context.User.FindFirstValue("preferred_username")
        ?? context.User.Identity?.Name;
    if (!adminRateLimiter.TryAcquire($"{adminUserId}:delete-user", 30))
    {
        return Results.StatusCode(StatusCodes.Status429TooManyRequests);
    }

    bool allowDeleteWithActiveSubscription = configuration.GetValue<bool?>("Admin:AllowDeleteWithActiveSubscription") ?? false;
    try
    {
        bool deleted = await adminUsersService.DeleteUserAsync(
            userId,
            allowDeleteWithActiveSubscription,
            adminUserId,
            adminEmail,
            context.RequestAborted);
        return deleted ? Results.NoContent() : Results.NotFound();
    }
    catch (InvalidOperationException ex)
    {
        return Results.Conflict(new { message = ex.Message });
    }
});

app.MapPost("/api/admin/users/{userId}/plan-override", async (
        HttpContext context,
        string userId,
        AdminSetPlanOverrideRequest request,
        IConfiguration configuration,
        AdminEndpointRateLimiter adminRateLimiter,
        AdminUsersService adminUsersService,
        IUserIdResolver userIdResolver,
        ILoggerFactory loggerFactory) =>
{
    if (!AdminPlanOverrideAccess.IsAdminApiEnabled(configuration)
        || !AdminPlanOverrideAccess.IsAuthorized(context.User, configuration))
    {
        return Results.NotFound();
    }

    if (string.IsNullOrWhiteSpace(userId))
    {
        return Results.BadRequest(new { message = "userId is required." });
    }

    ILogger logger = loggerFactory.CreateLogger("AdminPlanAssignments");
    string assignedBy = ResolveAssignedBy(context.User, userIdResolver, logger, out _);
    if (!adminRateLimiter.TryAcquire($"{assignedBy}:plan-override", 120))
    {
        return Results.StatusCode(StatusCodes.Status429TooManyRequests);
    }
    string? adminCallerEmail = context.User.FindFirstValue(ClaimTypes.Email)
        ?? context.User.FindFirstValue("emails")
        ?? context.User.FindFirstValue("preferred_username")
        ?? context.User.Identity?.Name;

    try
    {
        AdminPlanOverrideResponse response = await adminUsersService.SetPlanOverrideAsync(
            userId,
            request,
            assignedBy,
            adminCallerEmail,
            context.RequestAborted);
        return Results.Ok(response);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new
        {
            message = ex.Message,
            code = "INVALID_PLAN_KEY"
        });
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new
        {
            message = ex.Message,
            code = "PLAN_NOT_FOUND"
        });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Admin plan override failed. userId={UserId}", userId);
        return Results.Problem(
            title: "Plan override failed.",
            detail: "An unexpected error occurred while persisting the override.",
            statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.MapPost("/api/admin/users/{userId}/stripe/sync", async (
        HttpContext context,
        string userId,
        IConfiguration configuration,
        AdminEndpointRateLimiter adminRateLimiter,
        AdminUsersService adminUsersService,
        IUserIdResolver userIdResolver,
        ILoggerFactory loggerFactory) =>
{
    if (!AdminPlanOverrideAccess.IsAdminApiEnabled(configuration)
        || !AdminPlanOverrideAccess.IsAuthorized(context.User, configuration))
    {
        return Results.NotFound();
    }

    ILogger logger = loggerFactory.CreateLogger("AdminStripeSyncUser");
    string adminUserId = ResolveAssignedBy(context.User, userIdResolver, logger, out _);
    string? adminEmail = context.User.FindFirstValue(ClaimTypes.Email)
        ?? context.User.FindFirstValue("emails")
        ?? context.User.FindFirstValue("preferred_username")
        ?? context.User.Identity?.Name;
    if (!adminRateLimiter.TryAcquire($"{adminUserId}:stripe-sync-user", 30))
    {
        return Results.StatusCode(StatusCodes.Status429TooManyRequests);
    }

    try
    {
        AdminUserDetailDto updated = await adminUsersService.SyncStripeForUserAsync(
            userId,
            adminUserId,
            adminEmail,
            context.RequestAborted);
        return Results.Ok(updated);
    }
    catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or StripeApiException)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
});

app.MapPost("/api/admin/users/{userId}/tokens/reset-period", async (
        HttpContext context,
        string userId,
        IConfiguration configuration,
        AdminEndpointRateLimiter adminRateLimiter,
        AdminUsersService adminUsersService,
        IUserIdResolver userIdResolver,
        ILoggerFactory loggerFactory) =>
{
    if (!AdminPlanOverrideAccess.IsAdminApiEnabled(configuration)
        || !AdminPlanOverrideAccess.IsAuthorized(context.User, configuration))
    {
        return Results.NotFound();
    }

    ILogger logger = loggerFactory.CreateLogger("AdminTokens");
    string adminUserId = ResolveAssignedBy(context.User, userIdResolver, logger, out _);
    string? adminEmail = context.User.FindFirstValue(ClaimTypes.Email)
        ?? context.User.FindFirstValue("emails")
        ?? context.User.FindFirstValue("preferred_username")
        ?? context.User.Identity?.Name;
    if (!adminRateLimiter.TryAcquire($"{adminUserId}:tokens-reset", 60))
    {
        return Results.StatusCode(StatusCodes.Status429TooManyRequests);
    }

    AdminTokenOperationResponse response = await adminUsersService.ResetTokensPeriodAsync(
        userId,
        adminUserId,
        adminEmail,
        context.RequestAborted);
    return Results.Ok(response);
});

app.MapPost("/api/admin/users/{userId}/tokens/adjust", async (
        HttpContext context,
        string userId,
        AdminAdjustTokensRequest request,
        IConfiguration configuration,
        AdminEndpointRateLimiter adminRateLimiter,
        AdminUsersService adminUsersService,
        IUserIdResolver userIdResolver,
        ILoggerFactory loggerFactory) =>
{
    if (!AdminPlanOverrideAccess.IsAdminApiEnabled(configuration)
        || !AdminPlanOverrideAccess.IsAuthorized(context.User, configuration))
    {
        return Results.NotFound();
    }

    bool tokenAdjustEnabled = configuration.GetValue<bool?>("Admin:EnableTokenAdjust") ?? false;
    if (!tokenAdjustEnabled)
    {
        return Results.NotFound();
    }

    ILogger logger = loggerFactory.CreateLogger("AdminTokens");
    string adminUserId = ResolveAssignedBy(context.User, userIdResolver, logger, out _);
    string? adminEmail = context.User.FindFirstValue(ClaimTypes.Email)
        ?? context.User.FindFirstValue("emails")
        ?? context.User.FindFirstValue("preferred_username")
        ?? context.User.Identity?.Name;
    if (!adminRateLimiter.TryAcquire($"{adminUserId}:tokens-adjust", 60))
    {
        return Results.StatusCode(StatusCodes.Status429TooManyRequests);
    }

    try
    {
        AdminTokenOperationResponse response = await adminUsersService.AdjustTokensAsync(
            userId,
            request.DeltaTokens,
            request.Reason ?? string.Empty,
            adminUserId,
            adminEmail,
            context.RequestAborted);
        return Results.Ok(response);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
});

app.MapGet("/api/admin/audit", async (
        HttpContext context,
        IConfiguration configuration,
        IUserIdResolver userIdResolver,
        AdminEndpointRateLimiter adminRateLimiter,
        AdminAuditService adminAuditService,
        ILoggerFactory loggerFactory,
        int page = 1,
        int pageSize = 50,
        string? adminUserId = null,
        string? targetUserId = null,
        string? action = null,
        DateTime? fromUtc = null,
        DateTime? toUtc = null) =>
{
    if (!AdminPlanOverrideAccess.IsAdminApiEnabled(configuration)
        || !AdminPlanOverrideAccess.IsAuthorized(context.User, configuration))
    {
        return Results.NotFound();
    }

    ILogger logger = loggerFactory.CreateLogger("AdminAudit");
    string caller = ResolveAssignedBy(context.User, userIdResolver, logger, out _);
    if (!adminRateLimiter.TryAcquire($"{caller}:audit-list", 120))
    {
        return Results.StatusCode(StatusCodes.Status429TooManyRequests);
    }

    AdminAuditListResponseDto response = await adminAuditService.QueryAsync(
        new AdminAuditQueryDto(page, pageSize, adminUserId, targetUserId, action, fromUtc, toUtc),
        context.RequestAborted);
    return Results.Ok(response);
});

app.MapGet("/api/admin/users/export.csv", async (
        HttpContext context,
        IConfiguration configuration,
        IUserIdResolver userIdResolver,
        AdminEndpointRateLimiter adminRateLimiter,
        AdminUsersService adminUsersService,
        ILoggerFactory loggerFactory,
        int page = 1,
        int pageSize = 500,
        string? q = null,
        string? planKey = null,
        bool overrideOnly = false,
        string? subscriptionStatus = null,
        int? tokensLeftLt = null,
        int? tokensLeftGt = null,
        string? sort = null) =>
{
    if (!AdminPlanOverrideAccess.IsAdminApiEnabled(configuration)
        || !AdminPlanOverrideAccess.IsAuthorized(context.User, configuration))
    {
        return Results.NotFound();
    }

    ILogger logger = loggerFactory.CreateLogger("AdminUsersExport");
    string adminUserId = ResolveAssignedBy(context.User, userIdResolver, logger, out _);
    string? adminEmail = context.User.FindFirstValue(ClaimTypes.Email)
        ?? context.User.FindFirstValue("emails")
        ?? context.User.FindFirstValue("preferred_username")
        ?? context.User.Identity?.Name;
    if (!adminRateLimiter.TryAcquire($"{adminUserId}:users-export", 20))
    {
        return Results.StatusCode(StatusCodes.Status429TooManyRequests);
    }

    string csv = await adminUsersService.ExportCsvAsync(
        page,
        pageSize,
        q,
        planKey,
        overrideOnly,
        subscriptionStatus,
        tokensLeftLt,
        tokensLeftGt,
        sort,
        adminUserId,
        adminEmail,
        context.RequestAborted);

    context.Response.Headers.ContentDisposition = "attachment; filename=admin-users-export.csv";
    return Results.Text(csv, "text/csv");
});

app.MapPost("/api/dev/users/{userId}/reset-onboarding", async (
        HttpContext context,
        string userId,
        IUserIdResolver userIdResolver,
        AdminUsersService adminUsersService,
        ILoggerFactory loggerFactory) =>
{
    if (!app.Environment.IsDevelopment())
    {
        return Results.NotFound();
    }

    ILogger logger = loggerFactory.CreateLogger("DevAdmin");
    string adminUserId = ResolveAssignedBy(context.User, userIdResolver, logger, out _);
    string? adminEmail = context.User.FindFirstValue(ClaimTypes.Email)
        ?? context.User.FindFirstValue("emails")
        ?? context.User.FindFirstValue("preferred_username")
        ?? context.User.Identity?.Name;

    try
    {
        AdminUserDetailDto updated = await adminUsersService.ResetOnboardingAsync(
            userId,
            adminUserId,
            adminEmail,
            context.RequestAborted);
        return Results.Ok(updated);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
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

app.MapPost("/api/admin/stripe/resync", async (
        string? customerId,
        string? subscriptionId,
        StripeApiClient stripeApiClient,
        StripeEntitlementSyncService stripeEntitlementSyncService,
        StripeOptions stripeOptions,
        AppDbContext dbContext,
        ILoggerFactory loggerFactory,
        CancellationToken ct) =>
{
    if (!stripeOptions.Enabled || string.IsNullOrWhiteSpace(stripeOptions.SecretKey))
    {
        return Results.Json(
            new { success = false, message = "Stripe integration is not configured." },
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    string? normalizedCustomerId = string.IsNullOrWhiteSpace(customerId) ? null : customerId.Trim();
    string? normalizedSubscriptionId = string.IsNullOrWhiteSpace(subscriptionId) ? null : subscriptionId.Trim();
    if (string.IsNullOrWhiteSpace(normalizedCustomerId) && string.IsNullOrWhiteSpace(normalizedSubscriptionId))
    {
        return Results.BadRequest(new { message = "Either customerId or subscriptionId is required." });
    }

    ILogger logger = loggerFactory.CreateLogger("AdminStripeResync");

    JsonDocument subscriptionDoc;
    try
    {
        if (!string.IsNullOrWhiteSpace(normalizedSubscriptionId))
        {
            subscriptionDoc = await stripeApiClient.GetSubscriptionAsync(
                stripeOptions.SecretKey,
                normalizedSubscriptionId,
                ct);
        }
        else
        {
            using JsonDocument subscriptionsDoc = await stripeApiClient.ListSubscriptionsByCustomerAsync(
                stripeOptions.SecretKey,
                normalizedCustomerId!,
                ct);
            if (!subscriptionsDoc.RootElement.TryGetProperty("data", out JsonElement data)
                || data.ValueKind != JsonValueKind.Array
                || data.GetArrayLength() == 0)
            {
                return Results.NotFound(new
                {
                    message = "No Stripe subscription found for customer.",
                    customerId = normalizedCustomerId
                });
            }

            subscriptionDoc = JsonDocument.Parse(data[0].GetRawText());
        }
    }
    catch (StripeApiException ex)
    {
        logger.LogError(
            ex,
            "Admin Stripe resync fetch failed. CustomerId={CustomerId} SubscriptionId={SubscriptionId}",
            normalizedCustomerId ?? string.Empty,
            normalizedSubscriptionId ?? string.Empty);
        return Results.Json(
            new
            {
                message = "Failed to fetch Stripe subscription.",
                stripeStatusCode = (int)ex.StatusCode,
                stripeErrorParam = ex.ErrorParam ?? string.Empty
            },
            statusCode: StatusCodes.Status502BadGateway);
    }

    using (subscriptionDoc)
    {
        JsonElement subscription = subscriptionDoc.RootElement;
        string? resolvedSubscriptionId = ReadJsonString(subscription, "id");
        if (string.IsNullOrWhiteSpace(normalizedCustomerId))
        {
            normalizedCustomerId = ReadJsonString(subscription, "customer");
        }

        string? resolvedUserId = ReadJsonString(subscription, "metadata", "userId");
        if (string.IsNullOrWhiteSpace(resolvedUserId) && !string.IsNullOrWhiteSpace(resolvedSubscriptionId))
        {
            resolvedUserId = await dbContext.UserEntitlements
                .AsNoTracking()
                .Where(item => item.StripeSubscriptionId == resolvedSubscriptionId)
                .Select(item => item.UserId)
                .FirstOrDefaultAsync(ct);
        }

        if (string.IsNullOrWhiteSpace(resolvedUserId) && !string.IsNullOrWhiteSpace(normalizedCustomerId))
        {
            resolvedUserId = await dbContext.UserEntitlements
                .AsNoTracking()
                .Where(item => item.StripeCustomerId == normalizedCustomerId)
                .Select(item => item.UserId)
                .FirstOrDefaultAsync(ct);
        }

        if (string.IsNullOrWhiteSpace(resolvedUserId))
        {
            return Results.NotFound(new
            {
                message = "No user entitlement record found for the provided Stripe identifiers.",
                customerId = normalizedCustomerId ?? string.Empty,
                subscriptionId = resolvedSubscriptionId ?? normalizedSubscriptionId ?? string.Empty
            });
        }

        UserEntitlement entitlement = await stripeEntitlementSyncService.SyncFromSubscriptionAsync(
            resolvedUserId,
            normalizedCustomerId,
            subscription,
            ct);

        logger.LogInformation(
            "Admin Stripe resync completed. UserId={UserId} PlanKey={PlanKey} CustomerId={CustomerId} SubscriptionId={SubscriptionId} PriceId={PriceId}",
            entitlement.UserId,
            entitlement.PlanKey,
            entitlement.StripeCustomerId ?? string.Empty,
            entitlement.StripeSubscriptionId ?? string.Empty,
            entitlement.StripePriceId ?? string.Empty);

        return Results.Ok(new
        {
            userId = entitlement.UserId,
            planKey = entitlement.PlanKey,
            subscriptionStatus = entitlement.SubscriptionStatus,
            stripeCustomerId = entitlement.StripeCustomerId,
            stripeSubscriptionId = entitlement.StripeSubscriptionId,
            stripePriceId = entitlement.StripePriceId,
            entitlementUpdatedUtc = entitlement.UpdatedUtc
        });
    }
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
    context.Items["AuthMeHandlerEntered"] = true;
    string? forceRaw = context.Request.Query["force"].FirstOrDefault();
    bool force = ParseTruthy(forceRaw);
    if (force)
    {
        context.Response.Headers.CacheControl = "no-store, no-cache, max-age=0";
        context.Response.Headers.Pragma = "no-cache";
    }

    ILogger logger = loggerFactory.CreateLogger("AuthMe");
    ClaimsPrincipal user = context.User;
    if (app.Environment.IsProduction())
    {
        string[] claimTypes = user.Claims
            .Select(claim => claim.Type)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(type => type, StringComparer.Ordinal)
            .Take(24)
            .ToArray();
        logger.LogInformation(
            "AuthMe handler entered. Authenticated={Authenticated} AuthType={AuthType} ClaimTypes={ClaimTypes}",
            user.Identity?.IsAuthenticated ?? false,
            user.Identity?.AuthenticationType ?? string.Empty,
            claimTypes);
    }

    if (user.Identity?.IsAuthenticated != true)
    {
        logger.LogInformation("AuthMe probe resolved anonymous user.");
        return Results.Ok(new WriterApp.Application.Security.AuthMeDto
        {
            IsAuthenticated = false,
            Roles = Array.Empty<string>(),
            PlanKey = UserEntitlementDefaults.FreePlanKey,
            SubscriptionStatus = null,
            StripeCustomerId = null,
            AiMonthlyTokenBudget = 0,
            AiTokensUsedThisPeriod = 0,
            PeriodStartUtc = DateTimeOffset.MinValue,
            EntitlementUpdatedUtc = DateTimeOffset.MinValue
        });
    }

    string? userId;
    try
    {
        userId = userIdResolver.ResolveUserId(user);
    }
    catch (SecurityException)
    {
        logger.LogWarning("AuthMe probe could not resolve user id from authenticated principal.");
        return Results.Unauthorized();
    }

    ExternalIdentityClaims.UserProfileIdentity profileIdentity =
        ExternalIdentityClaims.MapToUserProfileIdentity(user.Claims, userId);

    List<string> roles = user.FindAll(ClaimTypes.Role)
        .Select(c => c.Value)
        .Concat(user.FindAll("roles").Select(c => c.Value))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    WriterApp.Application.Security.AuthMeDto minimalResponse = new()
    {
        IsAuthenticated = true,
        Name = profileIdentity.DisplayName,
        Email = profileIdentity.Email,
        UserId = userId,
        Roles = roles,
        PlanKey = UserEntitlementDefaults.FreePlanKey,
        SubscriptionStatus = "Unknown",
        StripeCustomerId = null,
        AiMonthlyTokenBudget = 0,
        AiTokensUsedThisPeriod = 0,
        PeriodStartUtc = DateTimeOffset.MinValue,
        EntitlementUpdatedUtc = DateTimeOffset.MinValue
    };

    try
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();
        WriterApp.Application.Security.AuthMeDto? enrichedResponse = null;

        await strategy.ExecuteAsync(async () =>
        {
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
            else
            {
                DateTime now = DateTime.UtcNow;
                string? nextDisplayName = string.IsNullOrWhiteSpace(profileIdentity.DisplayName)
                    ? userProfile.DisplayName
                    : profileIdentity.DisplayName;
                bool changed =
                    !string.Equals(userProfile.DisplayName, nextDisplayName, StringComparison.Ordinal)
                    || userProfile.UpdatedUtc != now;
                if (changed)
                {
                    userProfile.DisplayName = nextDisplayName;
                    userProfile.UpdatedUtc = now;
                    await dbContext.SaveChangesAsync(ct);
                }
            }

            bool hadEntitlement = await dbContext.UserEntitlements
                .AsNoTracking()
                .AnyAsync(item => item.UserId == userId, ct);

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

            enrichedResponse = new WriterApp.Application.Security.AuthMeDto
            {
                IsAuthenticated = true,
                Name = profileIdentity.DisplayName,
                Email = profileIdentity.Email,
                UserId = userId,
                Roles = roles,
                PlanKey = entitlement.PlanKey,
                SubscriptionStatus = entitlement.SubscriptionStatus,
                StripeCustomerId = entitlement.StripeCustomerId,
                AiMonthlyTokenBudget = entitlement.AiMonthlyTokenBudget,
                AiTokensUsedThisPeriod = entitlement.AiTokensUsedThisPeriod,
                PeriodStartUtc = entitlement.PeriodStartUtc,
                EntitlementUpdatedUtc = entitlement.UpdatedUtc
            };
        });

        return Results.Ok(enrichedResponse ?? minimalResponse);
    }
    catch (Exception ex)
    {
        logger.LogWarning(
            ex,
            "Azure SQL connection failed during AuthMe. Likely cold start or transient failure. Returning minimal identity.");
        return Results.Ok(minimalResponse);
    }
});

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

static string? ReadJsonString(JsonElement element, params string[] path)
{
    JsonElement cursor = element;
    foreach (string segment in path)
    {
        if (cursor.ValueKind != JsonValueKind.Object
            || !cursor.TryGetProperty(segment, out JsonElement child))
        {
            return null;
        }

        cursor = child;
    }

    return cursor.ValueKind switch
    {
        JsonValueKind.String => cursor.GetString(),
        JsonValueKind.Number => cursor.ToString(),
        _ => null
    };
}

static bool ParseTruthy(string? rawValue)
{
    if (string.IsNullOrWhiteSpace(rawValue))
    {
        return false;
    }

    string candidate = rawValue.Trim();
    return candidate.Equals("1", StringComparison.OrdinalIgnoreCase)
        || candidate.Equals("true", StringComparison.OrdinalIgnoreCase)
        || candidate.Equals("yes", StringComparison.OrdinalIgnoreCase)
        || candidate.Equals("on", StringComparison.OrdinalIgnoreCase);
}

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

        string ridPrefix = RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "linux-" : "win-";
        foreach (string ridDir in Directory.EnumerateDirectories(runtimesDir, $"{ridPrefix}*", SearchOption.TopDirectoryOnly))
        {
            string nativeDir = Path.Combine(ridDir, "native");
            if (!Directory.Exists(nativeDir))
            {
                continue;
            }

            foreach (string file in Directory.EnumerateFiles(nativeDir, "*", SearchOption.TopDirectoryOnly))
            {
                string fileName = Path.GetFileName(file);
                if (fileName.Contains("sqlite3", StringComparison.OrdinalIgnoreCase))
                {
                    LogFilePresence(logger, baseDir, file);
                }
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

    bool projectsExists = false;
    int migrationsHistoryCount = 0;
    if (dbContext.Database.IsSqlite())
    {
        try
        {
            dbContext.Database.OpenConnection();
            using DbCommand projectsCommand = dbContext.Database.GetDbConnection().CreateCommand();
            projectsCommand.CommandText = "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = $tableName LIMIT 1;";
            DbParameter projectsParameter = projectsCommand.CreateParameter();
            projectsParameter.ParameterName = "$tableName";
            projectsParameter.Value = "Projects";
            projectsCommand.Parameters.Add(projectsParameter);
            object? projectsResult = projectsCommand.ExecuteScalar();
            projectsExists = projectsResult is not null;

            using DbCommand historyCommand = dbContext.Database.GetDbConnection().CreateCommand();
            historyCommand.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = '__EFMigrationsHistory';";
            object? historyTableExistsResult = historyCommand.ExecuteScalar();
            bool historyTableExists = historyTableExistsResult is long countLong && countLong > 0
                || historyTableExistsResult is int countInt && countInt > 0;
            if (historyTableExists)
            {
                using DbCommand countCommand = dbContext.Database.GetDbConnection().CreateCommand();
                countCommand.CommandText = "SELECT COUNT(*) FROM __EFMigrationsHistory;";
                object? historyCountResult = countCommand.ExecuteScalar();
                migrationsHistoryCount = historyCountResult switch
                {
                    long value => (int)value,
                    int value => value,
                    _ => 0
                };
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Pre-migration SQLite schema diagnostics failed.");
        }
        finally
        {
            dbContext.Database.CloseConnection();
        }
    }

    string[] pendingBefore = (await dbContext.Database.GetPendingMigrationsAsync(ct)).ToArray();
    string[] appliedBefore = (await dbContext.Database.GetAppliedMigrationsAsync(ct)).ToArray();

    logger.LogInformation(
        "EF pre-migration diagnostics. ProjectsExists={ProjectsExists}, EFMigrationsHistoryCount={HistoryCount}, PendingCount={PendingCount}, Pending=[{Pending}]",
        projectsExists,
        migrationsHistoryCount,
        pendingBefore.Length,
        string.Join(", ", pendingBefore));

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
    if (!dbContext.Database.IsSqlite())
    {
        return;
    }

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

static void LogRequiredSqliteColumns(AppDbContext dbContext, ILogger logger, string tableName, IReadOnlyList<string> requiredColumns)
{
    if (!dbContext.Database.IsSqlite())
    {
        return;
    }

    try
    {
        dbContext.Database.OpenConnection();
        using var command = dbContext.Database.GetDbConnection().CreateCommand();
        string escapedTableName = tableName.Replace("'", "''", StringComparison.Ordinal);
        command.CommandText = $"SELECT name FROM pragma_table_info('{escapedTableName}');";

        HashSet<string> existing = new(StringComparer.OrdinalIgnoreCase);
        using DbDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            if (!reader.IsDBNull(0))
            {
                existing.Add(reader.GetString(0));
            }
        }

        List<string> missing = requiredColumns
            .Where(column => !existing.Contains(column))
            .ToList();

        if (missing.Count == 0)
        {
            logger.LogInformation("SQLite column check: {Table} includes required columns [{Columns}].", tableName, string.Join(", ", requiredColumns));
            return;
        }

        logger.LogWarning("SQLite column check: {Table} is missing required columns [{Columns}].", tableName, string.Join(", ", missing));
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "SQLite column check failed for {Table}.", tableName);
    }
    finally
    {
        dbContext.Database.CloseConnection();
    }
}

static bool SqliteTableExists(AppDbContext dbContext, string tableName)
{
    try
    {
        dbContext.Database.OpenConnection();
        using var command = dbContext.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = $tableName LIMIT 1;";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "$tableName";
        parameter.Value = tableName;
        command.Parameters.Add(parameter);
        object? result = command.ExecuteScalar();
        return result is not null;
    }
    catch
    {
        return false;
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
        else if (hasProjectsTable > 0 && hasHistoryTable == 0)
        {
            logger.LogWarning("Schema mismatch detected: Projects table exists but __EFMigrationsHistory is missing. Database was likely initialized outside EF migrations.");
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

static string? ResolveSqlServerConnectionString(IConfiguration configuration)
{
    // Preferred key is ConnectionStrings:SqlServer, but allow DefaultConnection for Azure App Service compatibility.
    string? rawConnectionString = configuration.GetConnectionString("SqlServer")
        ?? configuration.GetConnectionString("DefaultConnection")
        ?? configuration["DefaultConnection"]
        ?? Environment.GetEnvironmentVariable("DefaultConnection");
    if (string.IsNullOrWhiteSpace(rawConnectionString))
    {
        return rawConnectionString;
    }

    var sqlBuilder = new SqlConnectionStringBuilder(rawConnectionString)
    {
        ConnectTimeout = 60
    };

    return sqlBuilder.ToString();
}


static async Task WarmUpSqlServerConnectionAsync(AppDbContext dbContext, ILogger logger, CancellationToken ct)
{
    try
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await dbContext.Database.ExecuteSqlRawAsync("SELECT 1;", ct);
        });

        logger.LogInformation("SQL warmup probe succeeded.");
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "SQL warmup probe failed at startup. App will continue; retries happen per request.");
    }
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








