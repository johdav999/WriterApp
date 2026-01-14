using BlazorApp.Components;
using WriterApp.AI.Abstractions;
using WriterApp.AI.Actions;
using WriterApp.AI.Core;
using WriterApp.AI.Providers.Mock;
using WriterApp.Application.Commands;

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
builder.Services.AddSingleton<IAiProviderRegistry, DefaultAiProviderRegistry>();
builder.Services.AddSingleton<IAiRouter, DefaultAiRouter>();
builder.Services.AddSingleton<IAiAction, RewriteSelectionAction>();
builder.Services.AddSingleton<IAiAction, GenerateCoverImageAction>();
builder.Services.AddSingleton<IAiActionExecutor, AiActionExecutor>();
builder.Services.AddSingleton<IAiProposalApplier, DefaultProposalApplier>();
builder.Services.AddSingleton<IAiOrchestrator, AiOrchestrator>();

var app = builder.Build();

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
