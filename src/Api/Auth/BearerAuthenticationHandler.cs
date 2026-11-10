using System.Security.Claims;
using System.Text.Encodings.Web;
using Api.Common;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Api.Auth;

public class BearerAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    TokenService tokens) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Bearer";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var parts = Request.Headers.Authorization.ToString().Split(' ');
        if (parts.Length < 2 || parts[0] != "Bearer" || parts[1].Length == 0) return AuthenticateResult.NoResult();

        var user = await tokens.VerifyAccessTokenAsync(parts[1]);
        if (user is null) return AuthenticateResult.Fail("Jeton invalide");

        var identity = new ClaimsIdentity([new Claim("sub", user.Sub)], SchemeName, "sub", null);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties) =>
        ExceptionMiddleware.WriteAsync(Context, StatusCodes.Status401Unauthorized, "Session expirée ou invalide, veuillez vous reconnecter");
}
