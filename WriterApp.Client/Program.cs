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
// Image generation has a longer budget than ordinary API calls. Keep the same
// account/error and manuscript handlers without reusing an attached handler chain.
builder.Services.AddKeyedScoped<HttpClient>("cover-studio",(sp,_)=>{
    var unauthorized=ActivatorUtilities.CreateInstance<ApiUnauthorizedRedirectHandler>(sp);
    var manuscript=ActivatorUtilities.CreateInstance<ManuscriptRequestHandler>(sp);
    manuscript.InnerHandler=new HttpClientHandler();unauthorized.InnerHandler=manuscript;
    return new HttpClient(unauthorized){BaseAddress=new Uri($"{origin}/"),Timeout=TimeSpan.FromMinutes(3)};
});
builder.Services.AddScoped(sp=>new CoverApiClient(sp.GetRequiredKeyedService<HttpClient>("cover-studio")));
builder.Services.AddScoped<WriterApp.Client.Services.OnboardingService>();
builder.Services.AddScoped<WriterApp.Client.State.OnboardingStateStore>();
builder.Services.AddScoped<WriterApp.Client.State.OnboardingOverlayStateService>();
builder.Services.AddScoped<AuthStateService>();
builder.Services.AddScoped<WebCheckedAi>();
builder.Services.AddScoped<WebBrainstormingCapabilities>();
builder.Services.AddScoped<WriterApp.Shared.Brainstorming.IBrainstormingMicrophone,WebBrainstormingMicrophone>();
builder.Services.AddScoped<WriterApp.Shared.Brainstorming.IBrainstormingSpeechOutput,WebBrainstormingSpeechOutput>();
builder.Services.AddScoped<WriterApp.Shared.Brainstorming.BrainstormingVoiceCoordinator>();
builder.Services.AddScoped<WebBrainstormingStore>();
builder.Services.AddScoped<WebBrainstormingSessions>();
builder.Services.AddScoped<WebBrainstormingTextProjector>();
builder.Services.AddScoped<WebBrainstormingContext>();
builder.Services.AddScoped<WriterApp.Shared.Brainstorming.IBrainstormingCacheStore>(sp => sp.GetRequiredService<WebBrainstormingSessions>());
builder.Services.AddScoped<WriterApp.Shared.Brainstorming.IBrainstormingRemote>(sp => sp.GetRequiredService<WebBrainstormingSessions>().Remote);
builder.Services.AddScoped<WriterApp.Shared.Brainstorming.BrainstormingSessionService>();
builder.Services.AddScoped<WriterApp.Shared.Brainstorming.IBrainstormingCapabilityService>(sp => sp.GetRequiredService<WebBrainstormingCapabilities>());
builder.Services.AddScoped<WebAiHistoryOutbox>();
builder.Services.AddScoped<EasyAuthMeClient>();
builder.Services.AddScoped<RecoveryDraftService>();
builder.Services.AddTransient<EditorSaveCoordinator>();
builder.Services.AddSingleton<WriterApp.Client.State.LastOpenedDocumentStateService>();
builder.Services.AddScoped<WriterApp.Client.Services.CoachRecommendationService>();
builder.Services.AddScoped<AiCommandStatusService>();
builder.Services.AddScoped<WriterApp.UI.Shared.Projects.IStoryboardData, HttpStoryboardData>();

await builder.Build().RunAsync();
