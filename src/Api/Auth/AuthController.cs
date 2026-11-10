using System.Text.Json;
using Api.Common;
using Api.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Api.Auth;

// jeton d'accès dans le corps, refresh token seulement en cookie httpOnly
[ApiController]
[Route("auth")]
public class AuthController(TokenService tokens, IOptions<AuthOptions> options) : ControllerBase
{
    private const int MaxLoginLength = 100;
    private const int MaxPasswordLength = 200;

    private readonly AuthOptions _options = options.Value;

    // limite plus basse contre le brute force
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
        // même message que le login existe ou non
        if (user is null) throw new UnauthorizedException("Identifiant ou mot de passe incorrect");
        return StartSession(user);
    }

    // au chargement de la page et à l'expiration du jeton, prolonge le cookie
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

    // domaines différents : COOKIE_SAMESITE=none (donc HTTPS)
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
