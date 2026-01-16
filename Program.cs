using BlazorApp.Components;
using WriterApp.AI.Abstractions;
using WriterApp.AI.Actions;
using WriterApp.AI.Core;
using WriterApp.AI.Providers.Mock;
using WriterApp.AI.Providers.OpenAI;
using WriterApp.Application.Commands;
using WriterApp.Application.Exporting;
using WriterApp.Application.State;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.Configure<WriterAiOptions>(builder.Configuration.GetSection("WriterApp:AI"));
builder.Services.AddSingleton<IAiTextService, MockAiTextService>();
builder.Services.AddSingleton<IArtifactStore, InMemoryArtifactStore>();
builder.Services.AddSingleton<IAiAttachmentStore, InMemoryAiAttachmentStore>();
builder.Services.AddSingleton<IAiProvider, MockTextProvider>();
builder.Services.AddSingleton<IAiProvider, MockImageProvider>();

WriterAiOpenAiOptions openAiOptions = builder.Configuration
    .GetSection("WriterApp:AI:Providers:OpenAI")
    .Get<WriterAiOpenAiOptions>() ?? new WriterAiOpenAiOptions();
<<<<<<< HEAD
OpenAiKeyProvider openAiKeyProvider = OpenAiKeyProvider.FromEnvironment();
builder.Services.AddSingleton(openAiKeyProvider);

if (openAiOptions.Enabled && openAiKeyProvider.HasKey)
=======

if (openAiOptions.Enabled)
>>>>>>> ebb7526 (Implemented export of md and html)
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
builder.Services.AddSingleton<IAiActionExecutor, AiActionExecutor>();
builder.Services.AddSingleton<IAiProposalApplier, DefaultProposalApplier>();
builder.Services.AddSingleton<IAiOrchestrator, AiOrchestrator>();
builder.Services.AddScoped<DocumentStorageService>();
builder.Services.AddScoped<AppHeaderState>();
builder.Services.AddSingleton<IExportRenderer, MarkdownExportRenderer>();
builder.Services.AddSingleton<IExportRenderer, HtmlExportRenderer>();
builder.Services.AddSingleton<ExportService>();

var app = builder.Build();

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


app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
