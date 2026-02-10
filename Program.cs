
using BlazorApp.Components;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Data.Sqlite;
using System.IO;
using System.Security;
using System.Security.Claims;
using System.Runtime.InteropServices;
using System.Threading;
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

var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddFilter("Microsoft.AspNetCore.Components.Server.Circuits", LogLevel.Information);
builder.Logging.AddFilter(
    "Microsoft.AspNetCore.SignalR",
    builder.Environment.IsDevelopment() ? LogLevel.Debug : LogLevel.Information);

// --------------------
// Services
// --------------------

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddDbContext<AppDbContext>(options =>
{
    string? connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        connectionString = builder.Environment.IsDevelopment()
            ? "Data Source=writerapp.db"
            : "Data Source=/home/site/data/writerapp.db";
    }

    var sqliteBuilder = new SqliteConnectionStringBuilder(connectionString);
    if (!string.IsNullOrWhiteSpace(sqliteBuilder.DataSource) && !Path.IsPathRooted(sqliteBuilder.DataSource))
    {
        sqliteBuilder.DataSource = Path.Combine(builder.Environment.ContentRootPath, sqliteBuilder.DataSource);
        connectionString = sqliteBuilder.ToString();
    }

    options.UseSqlite(connectionString);
});

builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = builder.Environment.IsDevelopment()
            ? FakeAuthAuthenticationHandler.SchemeName
            : EasyAuthAuthenticationHandler.SchemeName;
        options.DefaultChallengeScheme = builder.Environment.IsDevelopment()
            ? FakeAuthAuthenticationHandler.SchemeName
            : EasyAuthAuthenticationHandler.SchemeName;
    })
    .AddScheme<AuthenticationSchemeOptions, EasyAuthAuthenticationHandler>(
        EasyAuthAuthenticationHandler.SchemeName,
        _ => { });

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddAuthentication()
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

builder.Services.AddHttpsRedirection(options => options.HttpsPort = 443);

// Domain services
builder.Services.AddScoped<IPlanRepository, PlanRepository>();
builder.Services.AddScoped<IEntitlementService, EntitlementService>();
builder.Services.AddScoped<IUserIdResolver, UserIdResolver>();
builder.Services.AddScoped<IPlanAssignmentService, PlanAssignmentService>();
builder.Services.AddScoped<IUsageMeter, UsageMeter>();
builder.Services.AddSingleton<IClock, WriterApp.Application.Usage.SystemClock>();
builder.Services.AddScoped<IAiUsageStatusService, AiUsageStatusService>();
builder.Services.AddScoped<IAiUsagePolicy, AiUsagePolicy>();
builder.Services.AddSingleton<ISectionImportService, SectionImportService>();
builder.Services.AddScoped<IBibleStore, EfCoreBibleStore>();
builder.Services.AddSingleton<BiblePatchApplier>();
builder.Services.AddScoped<BibleRefreshService>();
builder.Services.AddScoped<WriterApp.Application.Documents.IDocumentRepository, WriterApp.Data.Documents.DocumentRepository>();
builder.Services.AddScoped<WriterApp.Application.Documents.ISectionRepository, WriterApp.Data.Documents.SectionRepository>();
builder.Services.AddScoped<WriterApp.Application.Documents.IPageRepository, WriterApp.Data.Documents.PageRepository>();
builder.Services.AddScoped<WriterApp.Application.Documents.IPageVersionService, WriterApp.Application.Documents.PageVersionService>();
builder.Services.AddScoped<WriterApp.Application.Documents.IPageVersionDiffService, WriterApp.Application.Documents.PageVersionDiffService>();
builder.Services.AddScoped<WriterApp.Application.Documents.IQualityCheckService, WriterApp.Application.Documents.QualityCheckService>();
builder.Services.AddScoped<WriterApp.Application.Documents.IProjectWordCountService, WriterApp.Application.Documents.ProjectWordCountService>();
builder.Services.AddScoped<WriterApp.Application.Documents.IProjectGoalsService, WriterApp.Application.Documents.ProjectGoalsService>();
builder.Services.AddScoped<WriterApp.Application.Documents.IProjectSceneLinkingService, WriterApp.Application.Documents.ProjectSceneLinkingService>();
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

AdminPolicyDiagnostics.Configure(app.Services.GetRequiredService<ILoggerFactory>());

// --------------------
// Startup probes
// --------------------

using (IServiceScope scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");

    LogRuntimeProbe(logger);
    ProbeSqlite(logger);

    AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (app.Environment.IsDevelopment())
    {
        await dbContext.Database.MigrateAsync();
        logger.LogInformation("Database migrations applied (development).");
    }
    else
    {
        await dbContext.Database.MigrateAsync();
        logger.LogInformation("Database migrations applied.");
    }

    EnsureOutlineTemplatesTable(dbContext, logger);

    if (app.Environment.IsDevelopment())
    {
        LogTablePresence(dbContext, logger, "PageVersions");
        LogTablePresence(dbContext, logger, "OutlineTemplates");
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
        logger.LogWarning(ex, "Startup trash cleanup failed.");
    }

    ApplySqlitePragmas(dbContext, logger);
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
    logger.LogInformation(
        "Authentication schemes registered: {Schemes}",
        string.Join(", ", authOptions.Schemes.Select(s => s.Name)));
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
app.UseHttpsRedirection();

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

app.MapGet("/", () => Results.Redirect("/app/projects"));

app.MapGet("/__ping", () => Results.Ok("pong"));

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
        IPlanAssignmentService planAssignmentService,
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

    ILogger logger = loggerFactory.CreateLogger("AdminPlanAssignments");
    string assignedBy = ResolveAssignedBy(context.User, userIdResolver, logger, out string? callerName);

    try
    {
        PlanAssignmentResult result = await planAssignmentService.AssignPlanAsync(
            userId,
            planKey,
            assignedBy,
            callerName,
            context.RequestAborted);

        return Results.Ok(new
        {
            userId = result.UserId,
            planKey = result.PlanKey,
            planName = result.PlanName,
            assignedUtc = result.AssignedUtc
        });
    }
    catch (PlanAssignmentException ex)
    {
        return ex.Code switch
        {
            PlanAssignmentErrorCode.PlanNotFound => Results.NotFound(new { message = ex.Message }),
            PlanAssignmentErrorCode.PlanInactive => Results.BadRequest(new { message = ex.Message }),
            PlanAssignmentErrorCode.InvalidUserId => Results.BadRequest(new { message = ex.Message }),
            PlanAssignmentErrorCode.InvalidPlanKey => Results.BadRequest(new { message = ex.Message }),
            PlanAssignmentErrorCode.AssignmentExists => Results.Conflict(new { message = ex.Message }),
            _ => Results.BadRequest(new { message = ex.Message })
        };
    }
})
.RequireAuthorization("AdminOnly");

app.MapGet("/api/debug/auth", (HttpContext context, ILoggerFactory loggerFactory) =>
{
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

app.MapGet("/api/auth/me", (HttpContext context, IUserIdResolver userIdResolver) =>
{
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

    List<string> roles = user.FindAll(ClaimTypes.Role)
        .Select(c => c.Value)
        .Concat(user.FindAll("roles").Select(c => c.Value))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    return Results.Ok(new WriterApp.Application.Security.AuthMeDto
    {
        IsAuthenticated = true,
        Name = user.Identity?.Name,
        UserId = userId,
        Roles = roles
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

static void EnsureOutlineTemplatesTable(AppDbContext dbContext, ILogger logger)
{
    if (!dbContext.Database.IsSqlite())
    {
        return;
    }

    const string createTableSql = """
        CREATE TABLE IF NOT EXISTS "OutlineTemplates" (
            "Id" TEXT NOT NULL CONSTRAINT "PK_OutlineTemplates" PRIMARY KEY,
            "OwnerUserId" TEXT NOT NULL,
            "Name" TEXT NOT NULL,
            "TemplateJson" TEXT NOT NULL,
            "CreatedUtc" TEXT NOT NULL,
            "UpdatedUtc" TEXT NOT NULL
        );
        """;

    const string createOwnerIndexSql = """
        CREATE INDEX IF NOT EXISTS "IX_OutlineTemplates_OwnerUserId"
        ON "OutlineTemplates" ("OwnerUserId");
        """;

    const string createUpdatedIndexSql = """
        CREATE INDEX IF NOT EXISTS "IX_OutlineTemplates_UpdatedUtc"
        ON "OutlineTemplates" ("UpdatedUtc");
        """;

    try
    {
        dbContext.Database.ExecuteSqlRaw(createTableSql);
        dbContext.Database.ExecuteSqlRaw(createOwnerIndexSql);
        dbContext.Database.ExecuteSqlRaw(createUpdatedIndexSql);
        logger.LogInformation("SQLite outline templates schema ensured.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to ensure SQLite OutlineTemplates schema.");
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
