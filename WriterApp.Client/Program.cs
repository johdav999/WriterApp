using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using WriterApp.Client;
using WriterApp.Application.Documents;



var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

Uri serverBase = new(builder.HostEnvironment.BaseAddress, UriKind.Absolute);
string origin = serverBase.GetLeftPart(UriPartial.Authority);
builder.Services.AddScoped(sp =>
{
    return new HttpClient { BaseAddress = new Uri($"{origin}/") };
});
builder.Services.AddScoped<OutlineTemplatesClient>();
builder.Services.AddScoped<WriterApp.Client.State.LayoutStateService>();
builder.Services.AddScoped<WriterApp.Client.State.CurrentDocumentStateService>();
builder.Services.AddScoped<WriterApp.Client.State.CurrentSceneStateService>();
builder.Services.AddScoped<WriterApp.Client.State.CurrentProjectStateService>();
builder.Services.AddSingleton<WriterApp.Client.State.LastOpenedDocumentStateService>();
builder.Services.AddScoped<WriterApp.Client.Services.CoachRecommendationService>();

await builder.Build().RunAsync();
