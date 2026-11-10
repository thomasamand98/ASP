using System.Text.RegularExpressions;
using Api.Common;

namespace Api.Configuration;

// anciennes variables d'env aussi, cf. LegacyEnvironment
public class AppOptions
{
    public const string Section = "App";

    // mysql://user:mdp@hote:3306/base
    public string? DatabaseUrl { get; set; }

    // séparées par des virgules, vide = localhost seulement
    public string? CorsOrigin { get; set; }

    // nb de proxys devant l'API, vide = 1 sur Railway sinon 0
    public string? TrustProxy { get; set; }
    public string? RailwayEnvironment { get; set; }

    // lien "ICI" en pied de page des contrats PDF
    public string? CgvUrl { get; set; }

    // vide = ./storage
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

public class AuthOptions
{
    public const string Section = "Auth";

    public const string Issuer = "mvc-template-api";
    // refresh token inutilisable comme jeton d'accès
    public const string AccessAudience = "mvc-template-app";
    public const string RefreshAudience = "mvc-template-refresh";
    public const int RefreshTtlSeconds = 8 * 60 * 60;
    public const string RefreshCookie = "refresh_token";
    // cookie envoyé seulement sur /auth
    public const string RefreshCookiePath = "/auth";

    // 32 caractères min
    public string? JwtSecret { get; set; }

    // lax par défaut, none impose HTTPS
    public string? CookieSameSite { get; set; }
    public string? CookieSecure { get; set; }

    // par IP, par route, par minute
    public string? ThrottleLimit { get; set; }
    public string? LoginThrottleLimit { get; set; }

    public string? AccessTokenTtlSeconds { get; set; }

    public int ThrottleLimitValue => NumberOr(ThrottleLimit, 300);
    public int LoginThrottleLimitValue => NumberOr(LoginThrottleLimit, 5);
    public double AccessTtlSeconds => NumberOrDouble(AccessTokenTtlSeconds, 15 * 60);

    // vide, non numérique ou 0 = défaut
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
