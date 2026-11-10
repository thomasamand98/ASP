using System.Globalization;
using Horizon.Web;
using Horizon.Web.Components.UnsavedChanges;
using Horizon.Web.Services;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#root");

// nombres envoyés à l'API avec un point, l'affichage fr-BE passe par Format.cs
CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

var config = new AppConfig
{
    ApiUrl = builder.Configuration["ApiUrl"] ?? "",
    LoginScreen = bool.TryParse(builder.Configuration["LoginScreen"], out var loginScreen) && loginScreen,
};
var apiUrl = AppConfig.ResolveApiUrl(config.ApiUrl, new Uri(builder.HostEnvironment.BaseAddress));
builder.Services.AddSingleton(config);
builder.Services.AddSingleton(new HttpClient { BaseAddress = new Uri(apiUrl + "/") });
builder.Services.AddSingleton<AuthSession>();
builder.Services.AddSingleton<ApiClient>();
builder.Services.AddSingleton<Browser>();
builder.Services.AddSingleton<ThemeService>();
builder.Services.AddSingleton<UnsavedChangesService>();
builder.Services.AddSingleton<EnumerationLabels>();
builder.Services.AddSingleton<Horizon.Web.Features.Documents.DocEditorJs>();

await builder.Build().RunAsync();
