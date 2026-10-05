// Point d'entrée du backend ASP.NET Core (migration du backend NestJS : même
// contrat d'API, le frontend n'a pas à changer). Écoute sur le port défini
// par PORT (3000 par défaut).
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

// ---------- Configuration ----------
// appsettings.json, puis les variables d'environnement historiques du
// backend NestJS (DATABASE_URL, JWT_SECRET... et fichier .env en dev).
builder.Configuration.AddLegacyEnvironment(builder.Environment.ContentRootPath);
builder.Services.Configure<AppOptions>(builder.Configuration.GetSection(AppOptions.Section));
builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection(AuthOptions.Section));
var appOptions = builder.Configuration.GetSection(AppOptions.Section).Get<AppOptions>() ?? new AppOptions();

builder.WebHost.ConfigureKestrel(kestrel =>
{
    kestrel.ListenAnyIP(builder.Configuration.GetValue("Port", 3000));
    // Pas de limite globale : elles sont appliquées par type de contenu,
    // 10 Mo pour le JSON (ZodBodyBinder) et 50 Mo par fichier envoyé
    // (Uploads), avec les mêmes réponses que le backend d'origine.
    kestrel.Limits.MaxRequestBodySize = null;
    kestrel.AddServerHeader = false;
});
builder.Services.Configure<FormOptions>(form => form.MultipartBodyLengthLimit = Uploads.MaxFileSize + 1024 * 1024);

// ---------- Base de données ----------
builder.Services.AddDbContext<AppDbContext>(db => db.UseMySQL(LegacyEnvironment.ToConnectionString(appOptions.DatabaseUrl)));

// ---------- Contrôleurs et JSON ----------
builder.Services
    .AddControllers()
    .AddJsonOptions(json => JsonConfig.Configure(json.JsonSerializerOptions));
builder.Services.Configure<ApiBehaviorOptions>(api =>
{
    // Les erreurs de validation viennent de Zod (messages du backend
    // d'origine), pas du ModelState d'ASP.NET.
    api.SuppressModelStateInvalidFilter = true;
    api.SuppressMapClientErrors = true;
});

// ---------- Authentification ----------
// Toute route exige un jeton d'accès, sauf [AllowAnonymous].
builder.Services.AddSingleton<TokenService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services
    .AddAuthentication(BearerAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, BearerAuthenticationHandler>(BearerAuthenticationHandler.SchemeName, null);
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

// ---------- CORS ----------
// Le front, servi depuis une autre origine, envoie le cookie de
// renouvellement de session : `credentials` est nécessaire.
builder.Services.AddCors(cors => cors.AddDefaultPolicy(policy => policy
    .SetIsOriginAllowed(appOptions.IsOriginAllowed)
    .AllowCredentials()
    .WithMethods("GET", "POST", "PATCH", "DELETE")
    .WithHeaders("Content-Type", "Authorization")
    .SetPreflightMaxAge(TimeSpan.FromSeconds(600))));

// Nombre de proxys placés devant l'API : sans cela, tous les visiteurs
// auraient l'adresse IP du proxy et partageraient la même limite de requêtes.
builder.Services.Configure<ForwardedHeadersOptions>(forwarded =>
{
    forwarded.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    forwarded.ForwardLimit = appOptions.TrustProxyHops;
    forwarded.KnownIPNetworks.Clear();
    forwarded.KnownProxies.Clear();
});

// ---------- Services métier ----------
builder.Services.AddFeatureServices();

// ---------- Swagger (développement uniquement) ----------
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
// La limitation s'applique d'abord (elle freine aussi les requêtes sans
// jeton), puis le jeton est exigé.
app.UseMiddleware<ThrottleMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
// Route inconnue : même réponse que NestJS/Express.
app.MapFallback(context => throw new NotFoundException($"Cannot {context.Request.Method} {context.Request.Path}{context.Request.QueryString}"))
    .WithMetadata(new AllowAnonymousAttribute(), new SkipThrottleAttribute());

await DatabaseStartupCheck.LogAsync(app);
await app.RunAsync();

// Pour les tests d'intégration (WebApplicationFactory).
public partial class Program;
