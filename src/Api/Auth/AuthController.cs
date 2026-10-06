using System.Text.Json;
using Api.Common;
using Api.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Api.Auth;

// Connexion, renouvellement, déconnexion. Le jeton d'accès est renvoyé dans
// le corps de la réponse (le front le garde en mémoire) ; le jeton de
// renouvellement, lui, n'existe que dans un cookie httpOnly, illisible par
// le JavaScript de la page.
[ApiController]
[Route("auth")]
public class AuthController(TokenService tokens, IOptions<AuthOptions> options) : ControllerBase
{
    private const int MaxLoginLength = 100;
    private const int MaxPasswordLength = 200;

    private readonly AuthOptions _options = options.Value;

    // NestJS : POST auth/login (@Public, @Throttle LOGIN_THROTTLE_LIMIT, 200)
    // Tentatives de connexion : bien plus strictes que la limite globale, pour
    // freiner le brute force.
    [AllowAnonymous]
    [LoginThrottle]
    [HttpPost("login")]
    public async Task<object> Login()
    {
        var body = await ZodBodyBinder.ReadJsonBodyAsync(Request);
        var login = ReadString(body, "login")?.Trim() ?? "";
        var password = ReadString(body, "password") ?? "";
        if (login.Length == 0 || password.Length == 0) throw new BadRequestException("Vous devez renseigner l'identifiant et le mot de passe");
        if (login.Length > MaxLoginLength || password.Length > MaxPasswordLength)
        {
            throw new BadRequestException("Identifiant ou mot de passe trop long");
        }

        var user = await tokens.AuthenticateAsync(login, password);
        // Même message que l'identifiant existe ou non : on ne révèle rien.
        if (user is null) throw new UnauthorizedException("Identifiant ou mot de passe incorrect");
        return StartSession(user);
    }

    // NestJS : POST auth/refresh (@Public, 200)
    // Appelé par le front au chargement de la page et quand le jeton d'accès
    // expire : échange le cookie de renouvellement contre un nouveau jeton
    // d'accès, et prolonge le cookie.
    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<object> Refresh()
    {
        var token = Request.Cookies[AuthOptions.RefreshCookie];
        var user = string.IsNullOrEmpty(token) ? null : await tokens.VerifyRefreshTokenAsync(token);
        if (user is null)
        {
            Response.Cookies.Delete(AuthOptions.RefreshCookie, RefreshCookieOptions());
            throw new UnauthorizedException("Session expirée, veuillez vous reconnecter");
        }
        return StartSession(user);
    }

    // NestJS : POST auth/logout (@Public, 200)
    [AllowAnonymous]
    [HttpPost("logout")]
    public object Logout()
    {
        Response.Cookies.Delete(AuthOptions.RefreshCookie, RefreshCookieOptions());
        return new { };
    }

    private object StartSession(AuthUser user)
    {
        var accessToken = tokens.IssueAccessToken(user);
        var refreshToken = tokens.IssueRefreshToken(user);
        var cookie = RefreshCookieOptions();
        cookie.MaxAge = TimeSpan.FromSeconds(AuthOptions.RefreshTtlSeconds);
        cookie.Expires = DateTimeOffset.UtcNow.AddSeconds(AuthOptions.RefreshTtlSeconds);
        Response.Cookies.Append(AuthOptions.RefreshCookie, refreshToken, cookie);
        return new { accessToken, login = user.Sub };
    }

    // COOKIE_SAMESITE (lax par défaut) / COOKIE_SECURE : le front et l'API sur
    // le même site (ex. localhost, ou app.x.be + api.x.be) fonctionnent avec les
    // valeurs par défaut. Front et API sur des domaines différents demandent
    // COOKIE_SAMESITE=none, qui impose HTTPS.
    private CookieOptions RefreshCookieOptions() => new()
    {
        HttpOnly = true,
        SameSite = _options.SameSite,
        Secure = _options.Secure,
        Path = AuthOptions.RefreshCookiePath,
    };

    private static string? ReadString(JsonElement? body, string name) =>
        body is { ValueKind: JsonValueKind.Object } json && json.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
