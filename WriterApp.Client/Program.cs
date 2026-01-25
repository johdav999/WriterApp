using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using WriterApp.Client;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

Uri serverBase = new(builder.HostEnvironment.BaseAddress, UriKind.Absolute);
string origin = serverBase.GetLeftPart(UriPartial.Authority);
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri($"{origin}/") });

await builder.Build().RunAsync();
