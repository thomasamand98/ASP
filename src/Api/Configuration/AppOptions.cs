using System.Text.RegularExpressions;
using Api.Common;

namespace Api.Configuration;

// Réglages de l'application (section « App » de appsettings.json). Chaque
// valeur peut aussi venir de la variable d'environnement historique du
// backend NestJS (DATABASE_URL, CORS_ORIGIN...) — voir LegacyEnvironment.
public class AppOptions
{
    public const string Section = "App";

    // Format Prisma : mysql://utilisateur:motdepasse@hote:3306/base
    public string? DatabaseUrl { get; set; }

    // Origines autorisées en CORS, séparées par des virgules. Vide (dev
    // local) : seules les pages ouvertes sur localhost/127.0.0.1 (n'importe
    // quel port) sont acceptées.
    public string? CorsOrigin { get; set; }

    // Nombre de proxys placés devant l'API. Vide : 1 sur Railway, sinon 0.
    public string? TrustProxy { get; set; }
    public string? RailwayEnvironment { get; set; }

    // Cible du lien « ICI » imprimé en pied de page des contrats PDF.
    public string? CgvUrl { get; set; }

    // Dossier des fichiers du serveur (PDF générés, documents joints).
    // Vide : « storage », relatif au dossier de lancement.
    public string? PdfStorageDir { get; set; }

    public string? MistralApiKey { get; set; }

    public string StorageRoot => Path.GetFullPath(string.IsNullOrWhiteSpace(PdfStorageDir) ? "storage" : PdfStorageDir.Trim());

    public int TrustProxyHops
    {
        get
        {
            if (TrustProxy is not null)
            {
                var configured = JsNumber.Parse(TrustProxy);
                if (JsNumber.IsInteger(configured) && configured >= 0) return (int)configured;
            }
            return string.IsNullOrEmpty(RailwayEnvironment) ? 0 : 1;
        }
    }

    public string[]? CorsOrigins =>
        string.IsNullOrEmpty(CorsOrigin) ? null : CorsOrigin.Split(',').Select(origin => origin.Trim()).ToArray();

    public static readonly Regex LocalOrigin = new(@"^http://(localhost|127\.0\.0\.1):\d+$", RegexOptions.Compiled);

    public bool IsOriginAllowed(string origin) => CorsOrigins is { } list ? list.Contains(origin) : LocalOrigin.IsMatch(origin);
}

// Réglages de l'authentification (section « Auth »).
public class AuthOptions
{
    public const string Section = "Auth";

    public const string Issuer = "mvc-template-api";
    // Deux audiences distinctes : un jeton de renouvellement ne peut pas servir
    // de jeton d'accès (et inversement), même signé avec le même secret.
    public const string AccessAudience = "mvc-template-app";
    public const string RefreshAudience = "mvc-template-refresh";
    public const int RefreshTtlSeconds = 8 * 60 * 60;
    public const string RefreshCookie = "refresh_token";
    // Le cookie n'est envoyé qu'aux routes /auth/*, jamais au reste de l'API.
    public const string RefreshCookiePath = "/auth";

    // Secret de signature des jetons JWT (32 caractères minimum). Absent : un
    // secret temporaire est généré et les sessions sont perdues à chaque
    // redémarrage.
    public string? JwtSecret { get; set; }

    // lax (défaut), strict ou none (impose HTTPS).
    public string? CookieSameSite { get; set; }
    public string? CookieSecure { get; set; }

    // Limites par IP, par route et par minute.
    public string? ThrottleLimit { get; set; }
    public string? LoginThrottleLimit { get; set; }

    // Durée de vie du jeton d'accès, en secondes (900 = 15 minutes par défaut).
    public string? AccessTokenTtlSeconds { get; set; }

    public int ThrottleLimitValue => NumberOr(ThrottleLimit, 300);
    public int LoginThrottleLimitValue => NumberOr(LoginThrottleLimit, 5);
    public double AccessTtlSeconds => NumberOrDouble(AccessTokenTtlSeconds, 15 * 60);

    // `Number(valeur) || défaut` : vide, non numérique ou 0 → défaut.
    private static double NumberOrDouble(string? raw, double fallback)
    {
        var value = raw is null ? double.NaN : JsNumber.Parse(raw);
        return double.IsNaN(value) || value == 0 ? fallback : value;
    }

    private static int NumberOr(string? raw, int fallback) => (int)Math.Min(NumberOrDouble(raw, fallback), int.MaxValue);

    public SameSiteMode SameSite => CookieSameSite?.Trim().ToLowerInvariant() switch
    {
        "none" => SameSiteMode.None,
        "strict" => SameSiteMode.Strict,
        _ => SameSiteMode.Lax,
    };

    public bool Secure => SameSite == SameSiteMode.None || CookieSecure == "true";
}
