using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Options;
using WriterApp.Application.Security;
using WriterApp.Client;
using WriterApp.Application.Documents;
using WriterApp.Client.Services;



var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

Uri serverBase = new(builder.HostEnvironment.BaseAddress, UriKind.Absolute);
string origin = serverBase.GetLeftPart(UriPartial.Authority);
builder.Services.AddScoped<ApiUnauthorizedRedirectHandler>();
builder.Services.AddScoped<WriterApp.Client.State.ManuscriptSelectionState>();
builder.Services.AddScoped<ManuscriptRequestHandler>();
builder.Services.AddScoped(sp =>
{
    ApiUnauthorizedRedirectHandler unauthorizedHandler = sp.GetRequiredService<ApiUnauthorizedRedirectHandler>();
    var manuscriptHandler = sp.GetRequiredService<ManuscriptRequestHandler>();
    manuscriptHandler.InnerHandler = new HttpClientHandler();
    unauthorizedHandler.InnerHandler = manuscriptHandler;

    return new HttpClient(unauthorizedHandler)
    {
        BaseAddress = new Uri($"{origin}/")
    };
});
WriterAuthOptions authOptions =
    builder.Configuration.GetSection("WriterApp:Auth").Get<WriterAuthOptions>()
    ?? new WriterAuthOptions();
builder.Services.AddSingleton<IOptions<WriterAuthOptions>>(Options.Create(authOptions));
builder.Services.AddScoped<OutlineTemplatesClient>();
builder.Services.AddScoped<WriterApp.Client.State.LayoutStateService>();
builder.Services.AddScoped<WriterApp.Client.State.CurrentDocumentStateService>();
builder.Services.AddScoped<WriterApp.Client.State.CurrentSceneStateService>();
builder.Services.AddScoped<WriterApp.Client.State.CurrentProjectStateService>();
builder.Services.AddScoped<WriterApp.Client.State.GlobalSearchNavigationService>();
builder.Services.AddScoped<WriterApp.Client.State.ProjectStructureCacheService>();
builder.Services.AddScoped<WriterApp.Client.State.ProjectProgressCacheService>();
builder.Services.AddScoped<WriterApp.Client.State.DeletedAccountStateService>();
builder.Services.AddScoped<WriterApp.Client.State.DuplicateAccountStateService>();
builder.Services.AddScoped<WriterApp.Client.State.AuthMeStateService>();
builder.Services.AddScoped<FeatureAccessService>();
builder.Services.AddScoped<ClientLogoutService>();
builder.Services.AddScoped<CoverApiClient>();
builder.Services.AddScoped<WriterApp.Client.Services.OnboardingService>();
builder.Services.AddScoped<WriterApp.Client.State.OnboardingStateStore>();
builder.Services.AddScoped<WriterApp.Client.State.OnboardingOverlayStateService>();
builder.Services.AddScoped<AuthStateService>();
builder.Services.AddScoped<EasyAuthMeClient>();
builder.Services.AddScoped<RecoveryDraftService>();
builder.Services.AddTransient<EditorSaveCoordinator>();
builder.Services.AddSingleton<WriterApp.Client.State.LastOpenedDocumentStateService>();
builder.Services.AddScoped<WriterApp.Client.Services.CoachRecommendationService>();
builder.Services.AddScoped<AiCommandStatusService>();
builder.Services.AddScoped<WriterApp.UI.Shared.Projects.IStoryboardData, HttpStoryboardData>();

await builder.Build().RunAsync();
