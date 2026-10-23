using System.Diagnostics;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using WriterApp.Application.Security;
using WriterApp.Application.Subscriptions;
using WriterApp.Data.Subscriptions;

namespace WriterApp.Application.Collaboration;

// Explicit, loopback-only staging profile. Normal Development/Production defaults stay closed.
public static class LocalCollaborationTest
{
    public const string CookieScheme = "LocalCollaborationAccount";
    public const string Origin = "http://localhost:5398";
    public static int AppPort(IConfiguration config) => Port(config,"Port",5398);
    public static int WorkerPort(IConfiguration config) => Port(config,"CollaborationPort",1234);
    public static string LocalOrigin(IConfiguration config) => $"http://localhost:{AppPort(config)}";
    private static int Port(IConfiguration config,string name,int fallback)
    {
        int port=config.GetValue<int?>($"Collaboration:LocalTest:{name}")??fallback;
        return port is >=1024 and <=65535 ? port : throw new InvalidOperationException($"Local collaboration {name} must be between 1024 and 65535.");
    }
    public static bool AcceptsRequest(HttpContext context,IConfiguration config)
    {
        return context.Connection.RemoteIpAddress is { } address && System.Net.IPAddress.IsLoopback(address)
            && context.Request.Scheme=="http" && context.Request.Host.Value==$"localhost:{AppPort(config)}"
            && (context.Request.Headers.Origin.Count==0 || context.Request.Headers.Origin.ToString()==LocalOrigin(config));
    }
    public static bool Active(IHostEnvironment env, IConfiguration config) => env.IsStaging()
        && config.GetValue<bool>("Collaboration:LocalTest:Enabled");

    public static bool Configure(WebApplicationBuilder builder)
    {
        if (!builder.Configuration.GetValue<bool>("Collaboration:LocalTest:Enabled")) return false;
        if (!builder.Environment.IsStaging()) throw new InvalidOperationException("Local collaboration requires the explicit Staging profile.");
        var origin=LocalOrigin(builder.Configuration);
        int workerPort=WorkerPort(builder.Configuration);
        if(AppPort(builder.Configuration)==workerPort)throw new InvalidOperationException("Local app and collaboration worker require different ports.");
        // Staging does not load referenced-project static assets automatically. This local
        // source build needs the client runtime and shared UI assets from its build manifest.
        builder.WebHost.UseStaticWebAssets();
        string folder = Path.Combine(builder.Environment.ContentRootPath, "artifacts", "local-collaboration");
        Directory.CreateDirectory(folder);
        // Set the isolated path here, before DB/billing configuration is read. Never reuse a supplied production connection.
        builder.Configuration.AddInMemoryCollection(new Dictionary<string,string?> {
            ["DatabaseProvider"]="Sqlite", ["ConnectionStrings:DefaultConnection"]=new SqliteConnectionStringBuilder{DataSource=Path.Combine(folder,"writerapp.db")}.ToString(),
            ["UseExternalIdAuth"]="false", ["Stripe:Enabled"]="false", ["WriterApp:Database:AutoMigrateOnStartup"]="true",
            ["AppUrls:PublicBaseUrl"]=origin,
            ["Logging:LogLevel:Microsoft.AspNetCore"]="Warning",
            ["Logging:LogLevel:Microsoft.EntityFrameworkCore.Database.Command"]="Warning", ["Logging:LogLevel:ApiRequests"]="Warning",
            ["Collaboration:SessionsEnabled"]="true", ["Collaboration:CommitsEnabled"]="true", ["Collaboration:Sharing:Enabled"]="true",
            ["Collaboration:Conversion:Enabled"]="true", ["Collaboration:Interactions:Enabled"]="true",
            ["Collaboration:Environment"]="local-two-account-test", ["Collaboration:WebSocketUrl"]=$"ws://127.0.0.1:{workerPort}/",
            ["Collaboration:ServiceKey"]=Convert.ToHexString(RandomNumberGenerator.GetBytes(32))
        });
        builder.Services.AddDataProtection().SetApplicationName("Prosa.LocalCollaborationTest")
            .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(folder,"keys")));
        builder.Services.AddAuthentication().AddCookie(CookieScheme, options => {
            options.Cookie.Name=AppPort(builder.Configuration)==5398?"Prosa.LocalCollaboration":$"Prosa.LocalCollaboration.{AppPort(builder.Configuration)}"; options.Cookie.HttpOnly=true;
            options.Cookie.SameSite=SameSiteMode.Lax; options.Cookie.SecurePolicy=CookieSecurePolicy.SameAsRequest;
            options.LoginPath="/local-collaboration/sign-in"; options.SlidingExpiration=false;
            options.Events.OnRedirectToLogin=context => { if(context.Request.Path.StartsWithSegments("/api"))context.Response.StatusCode=401; else context.Response.Redirect(context.RedirectUri); return Task.CompletedTask; };
            options.Events.OnRedirectToAccessDenied=context => { context.Response.StatusCode=403; return Task.CompletedTask; };
        });
        builder.Services.AddHostedService<LocalCollaborationNode>();
        builder.Services.AddScoped<UserEntitlementStore>();
        return true;
    }
}

public sealed class CollaborationStagedTestGate(IHostEnvironment env, IConfiguration config)
{
    public bool Enabled => LocalCollaborationTest.Active(env,config)
        && config.GetValue<bool>("Collaboration:CommitsEnabled") && config.GetValue<bool>("Collaboration:Sharing:Enabled")
        && config.GetValue<bool>("Collaboration:Conversion:Enabled")
        && config.GetSection("Collaboration").Get<CollaborationSessionOptions>()?.IsConfigured==true;
}

// A local-only license view; the underlying free record and all real subscriptions are unchanged.
public sealed class LocalCollaborationEntitlements(UserEntitlementStore inner) : IUserEntitlementStore
{
    public async Task<UserEntitlement> GetOrCreateAsync(string userId,CancellationToken ct=default)
    {
        var original=await inner.GetOrCreateAsync(userId,ct);
        return new UserEntitlement{UserId=original.UserId,PlanKey="standard",HasManualPlanOverride=true,
            SubscriptionStatus=original.SubscriptionStatus,CreatedAt=original.CreatedAt,PeriodStartUtc=original.PeriodStartUtc,
            AiMonthlyTokenBudget=UserEntitlementDefaults.StandardMonthlyTokenBudget,
            AiTokensUsedThisPeriod=original.AiTokensUsedThisPeriod,UpdatedUtc=original.UpdatedUtc};
    }
}

public sealed class LocalCollaborationNode(IOptionsMonitor<CollaborationSessionOptions> options,IHostEnvironment env,
    ILogger<LocalCollaborationNode> logger,IConfiguration config) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var start=new ProcessStartInfo("node"){WorkingDirectory=env.ContentRootPath,UseShellExecute=false,CreateNoWindow=true,
            RedirectStandardOutput=true,RedirectStandardError=true};
        start.ArgumentList.Add("WriterApp.Collaboration/src/main.mjs");
        start.Environment["COLLABORATION_BACKEND_URL"]=LocalCollaborationTest.LocalOrigin(config)+"/";
        start.Environment["COLLABORATION_SERVICE_KEY"]=options.CurrentValue.ServiceKey;
        start.Environment["COLLABORATION_ENVIRONMENT"]=options.CurrentValue.Environment;
        start.Environment["COLLABORATION_ALLOWED_ORIGINS"]=LocalCollaborationTest.LocalOrigin(config);
        start.Environment["COLLABORATION_COMMITS_ENABLED"]="true";start.Environment["COLLABORATION_INTERACTIONS_ENABLED"]="true";
        start.Environment["COLLABORATION_ADDRESS"]="127.0.0.1";start.Environment["PORT"]=LocalCollaborationTest.WorkerPort(config).ToString(System.Globalization.CultureInfo.InvariantCulture);
        using var child=Process.Start(start)??throw new InvalidOperationException("Local collaboration service did not start.");
        // Consume output without logging potential protocol payloads or service credentials.
        var output=child.StandardOutput.ReadToEndAsync();var error=child.StandardError.ReadToEndAsync();
        try { await child.WaitForExitAsync(ct); if(!ct.IsCancellationRequested)throw new InvalidOperationException($"Local collaboration service exited; check that port {LocalCollaborationTest.WorkerPort(config)} is free and Node 24 dependencies are installed."); }
        finally { if(!child.HasExited){child.Kill(entireProcessTree:true);await child.WaitForExitAsync();} await output;await error;logger.LogInformation("Owned local collaboration process stopped."); }
    }
}
