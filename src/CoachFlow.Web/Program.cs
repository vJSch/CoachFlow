using CoachFlow.Client;
using CoachFlow.Web;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Gleicher Ursprung wie die API: Das Anmelde-Cookie (SameSite=Strict) wird automatisch mitgesendet.
builder.Services.AddScoped(_ => new HttpClient
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress),
    Timeout = TimeSpan.FromSeconds(30)
});
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(SuggestionTiming.Default);
builder.Services.AddSingleton<AppState>();
builder.Services.AddScoped<IClientStore, BrowserStore>();
builder.Services.AddScoped<CoachFlowApi>();
builder.Services.AddScoped<PlanCache>();
builder.Services.AddScoped<PlanLibrary>();
builder.Services.AddScoped<FocusAreaCatalog>();
builder.Services.AddScoped<SuggestionRequester>();

await builder.Build().RunAsync();
