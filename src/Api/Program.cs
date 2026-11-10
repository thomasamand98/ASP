using Api.Auth;
using Api.Common;
using Api.Configuration;
using Api.Data;
using Api.Features;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// anciennes variables d'env + .env en dev
builder.Configuration.AddLegacyEnvironment(builder.Environment.ContentRootPath);
builder.Services.Configure<AppOptions>(builder.Configuration.GetSection(AppOptions.Section));
builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection(AuthOptions.Section));
var appOptions = builder.Configuration.GetSection(AppOptions.Section).Get<AppOptions>() ?? new AppOptions();

builder.WebHost.ConfigureKestrel(kestrel =>
{
    kestrel.ListenAnyIP(builder.Configuration.GetValue("Port", 3000));
    // limites gérées dans ZodBodyBinder et Uploads
    kestrel.Limits.MaxRequestBodySize = null;
    kestrel.AddServerHeader = false;
});
builder.Services.Configure<FormOptions>(form => form.MultipartBodyLengthLimit = Uploads.MaxFileSize + 1024 * 1024);

builder.Services.AddDbContext<AppDbContext>(db => db.UseMySQL(LegacyEnvironment.ToConnectionString(appOptions.DatabaseUrl)));

builder.Services
    .AddControllers()
    .AddJsonOptions(json => JsonConfig.Configure(json.JsonSerializerOptions));
builder.Services.Configure<ApiBehaviorOptions>(api =>
{
    // validation faite par Zod, pas le ModelState
    api.SuppressModelStateInvalidFilter = true;
    api.SuppressMapClientErrors = true;
});

// jeton exigé partout sauf [AllowAnonymous]
builder.Services.AddSingleton<TokenService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services
    .AddAuthentication(BearerAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, BearerAuthenticationHandler>(BearerAuthenticationHandler.SchemeName, null);
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

// credentials pour le cookie de refresh
builder.Services.AddCors(cors => cors.AddDefaultPolicy(policy => policy
    .SetIsOriginAllowed(appOptions.IsOriginAllowed)
    .AllowCredentials()
    .WithMethods("GET", "POST", "PATCH", "DELETE")
    .WithHeaders("Content-Type", "Authorization")
    .SetPreflightMaxAge(TimeSpan.FromSeconds(600))));

// sinon tout le monde a l'IP du proxy (et la même limite)
builder.Services.Configure<ForwardedHeadersOptions>(forwarded =>
{
    forwarded.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    forwarded.ForwardLimit = appOptions.TrustProxyHops;
    forwarded.KnownIPNetworks.Clear();
    forwarded.KnownProxies.Clear();
});

builder.Services.AddFeatureServices();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (appOptions.TrustProxyHops > 0) app.UseForwardedHeaders();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseCors();
app.UseMiddleware<ExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();
// avant l'auth : freine aussi les requêtes sans jeton
app.UseMiddleware<ThrottleMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapFallback(context => throw new NotFoundException($"Cannot {context.Request.Method} {context.Request.Path}{context.Request.QueryString}"))
    .WithMetadata(new AllowAnonymousAttribute(), new SkipThrottleAttribute());

await DatabaseStartupCheck.LogAsync(app);
await app.RunAsync();

// pour WebApplicationFactory
public partial class Program;
