using BlazorApp.Components;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WriterApp.AI.Abstractions;
using WriterApp.AI.Actions;
using WriterApp.AI.Core;
using WriterApp.AI.Providers.Mock;
using WriterApp.AI.Providers.OpenAI;
using WriterApp.Application.Security;
using WriterApp.Application.Subscriptions;
using WriterApp.Application.Usage;
using WriterApp.Application.Commands;
using WriterApp.Application.Exporting;
using WriterApp.Application.State;
using WriterApp.Application.AI.StoryCoach;
using WriterApp.Application.Synopsis;
using WriterApp.Data;
using WriterApp.Data.Subscriptions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddDbContext<AppDbContext>(options =>
{
    string? connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseSqlite(string.IsNullOrWhiteSpace(connectionString) ? "Data Source=writerapp.db" : connectionString);
});
builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.User.RequireUniqueEmail = true;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();
builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddIdentityCookies();
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<IPlanRepository, PlanRepository>();
builder.Services.AddScoped<IEntitlementService, EntitlementService>();
builder.Services.AddScoped<IUserIdResolver, UserIdResolver>();
builder.Services.AddScoped<IUsageMeter, UsageMeter>();
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddScoped<IAiUsageStatusService, AiUsageStatusService>();
builder.Services.AddScoped<IAiUsagePolicy, AiUsagePolicy>();
builder.Services.AddSingleton<StoryCoachContextBuilder>();
builder.Services.Configure<WriterAuthOptions>(builder.Configuration.GetSection("WriterApp:Auth"));
builder.Services.Configure<WriterAiOptions>(builder.Configuration.GetSection("WriterApp:AI"));
builder.Services.AddSingleton<IAiTextService, MockAiTextService>();
builder.Services.AddSingleton<IArtifactStore, InMemoryArtifactStore>();
builder.Services.AddSingleton<IAiAttachmentStore, InMemoryAiAttachmentStore>();
builder.Services.AddSingleton<IAiProvider, MockTextProvider>();
builder.Services.AddSingleton<IAiProvider, MockImageProvider>();

WriterAiOpenAiOptions openAiOptions = builder.Configuration
    .GetSection("WriterApp:AI:Providers:OpenAI")
    .Get<WriterAiOpenAiOptions>() ?? new WriterAiOpenAiOptions();
OpenAiKeyProvider openAiKeyProvider = OpenAiKeyProvider.FromEnvironment();
builder.Services.AddSingleton(openAiKeyProvider);

if (openAiOptions.Enabled && openAiKeyProvider.HasKey)
{
    builder.Services.AddHttpClient(nameof(OpenAiProvider), client =>
    {
        int timeoutSeconds = Math.Max(1, openAiOptions.TimeoutSeconds);
        client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
    });
    builder.Services.AddSingleton<IAiProvider, OpenAiProvider>();
}

builder.Services.AddSingleton<IAiProviderRegistry, DefaultAiProviderRegistry>();
builder.Services.AddSingleton<IAiRouter, DefaultAiRouter>();
builder.Services.AddSingleton<IAiAction, RewriteSelectionAction>();
builder.Services.AddSingleton<IAiAction, GenerateCoverImageAction>();
builder.Services.AddSingleton<IAiAction, StoryCoachAction>();
builder.Services.AddSingleton<IAiActionExecutor, AiActionExecutor>();
builder.Services.AddSingleton<IAiProposalApplier, DefaultProposalApplier>();
builder.Services.AddScoped<IAiOrchestrator, AiOrchestrator>();
builder.Services.AddScoped<DocumentStorageService>();
builder.Services.AddScoped<AppHeaderState>();
builder.Services.AddSingleton<IExportRenderer, MarkdownExportRenderer>();
builder.Services.AddSingleton<IExportRenderer, HtmlExportRenderer>();
builder.Services.AddSingleton<ExportService>();
builder.Services.AddServerSideBlazor()
    .AddCircuitOptions(options =>
    {
        options.DetailedErrors = true;
    });
var app = builder.Build();

using (IServiceScope scope = app.Services.CreateScope())
{
    AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    dbContext.Database.EnsureCreated();

    WriterAuthOptions authOptions = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<WriterAuthOptions>>().Value;
    RoleManager<IdentityRole> roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

    if (!await roleManager.RoleExistsAsync("Admin"))
    {
        await roleManager.CreateAsync(new IdentityRole("Admin"));
    }

    string? adminEmail = string.IsNullOrWhiteSpace(authOptions.AdminEmail)
        ? authOptions.DevAutoLoginEmail
        : authOptions.AdminEmail;

    if (!string.IsNullOrWhiteSpace(adminEmail))
    {
        ApplicationUser? adminUser = await userManager.FindByEmailAsync(adminEmail);
        if (adminUser is null && app.Environment.IsDevelopment())
        {
            adminUser = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true
            };

            IdentityResult createResult = await userManager.CreateAsync(adminUser, authOptions.DevAutoLoginPassword);
            if (!createResult.Succeeded)
            {
                adminUser = null;
            }
        }

        if (adminUser is not null && !await userManager.IsInRoleAsync(adminUser, "Admin"))
        {
            await userManager.AddToRoleAsync(adminUser, "Admin");
        }
    }
}

if (openAiOptions.Enabled && !openAiKeyProvider.HasKey)
{
    var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
    logger.LogWarning("OPENAI_API_KEY is not set. OpenAI provider is disabled.");
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseAuthentication();
if (app.Environment.IsDevelopment())
{
    app.Use(async (context, next) =>
    {
        WriterAuthOptions authOptions = context.RequestServices.GetRequiredService<Microsoft.Extensions.Options.IOptions<WriterAuthOptions>>().Value;
        if (!authOptions.DevAutoLogin || (context.User.Identity?.IsAuthenticated ?? false))
        {
            await next();
            return;
        }

        UserManager<ApplicationUser> userManager = context.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
        SignInManager<ApplicationUser> signInManager = context.RequestServices.GetRequiredService<SignInManager<ApplicationUser>>();

        ApplicationUser? user = await userManager.FindByEmailAsync(authOptions.DevAutoLoginEmail);
        if (user is null && !await userManager.Users.AnyAsync())
        {
            user = new ApplicationUser
            {
                UserName = authOptions.DevAutoLoginEmail,
                Email = authOptions.DevAutoLoginEmail,
                EmailConfirmed = true
            };

            IdentityResult result = await userManager.CreateAsync(user, authOptions.DevAutoLoginPassword);
            if (!result.Succeeded)
            {
                await next();
                return;
            }
        }

        if (user is not null)
        {
            await signInManager.SignInAsync(user, isPersistent: false);
            context.User = await signInManager.CreateUserPrincipalAsync(user);
        }

        await next();
    });
}
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
