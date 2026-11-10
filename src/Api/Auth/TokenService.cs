using System.Security.Cryptography;
using System.Text;
using Api.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Api.Auth;

// rien d'autre que le login (sub) dans le jeton
public record AuthUser(string Sub);

public class TokenService
{
    private readonly JsonWebTokenHandler _handler = new() { SetDefaultTimesOnTokenCreation = false, MapInboundClaims = false };
    private readonly SigningCredentials _credentials;
    private readonly SymmetricSecurityKey _key;
    private readonly AuthOptions _options;

    public TokenService(IOptions<AuthOptions> options, ILogger<TokenService> logger)
    {
        _options = options.Value;
        // pas de JWT_SECRET : secret aléatoire, sessions perdues au redémarrage
        var secret = _options.JwtSecret?.Trim();
        if (!string.IsNullOrEmpty(secret))
        {
            if (secret.Length < 32) throw new InvalidOperationException("JWT_SECRET est trop court (32 caractères minimum).");
        }
        else
        {
            secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
            logger.LogWarning("JWT_SECRET absent : secret temporaire généré, les sessions seront perdues au prochain redémarrage.");
        }
        _key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        _credentials = new SigningCredentials(_key, SecurityAlgorithms.HmacSha256);
    }

    // TODO auth factice, tout passe : vérifier utilisateurs.Password_hash (et Actif)
    public Task<AuthUser?> AuthenticateAsync(string login, string password) => Task.FromResult<AuthUser?>(new AuthUser(login));

    public string IssueAccessToken(AuthUser user) => Issue(user, AuthOptions.AccessAudience, _options.AccessTtlSeconds);

    public string IssueRefreshToken(AuthUser user) => Issue(user, AuthOptions.RefreshAudience, AuthOptions.RefreshTtlSeconds);

    // null si jeton invalide
    public Task<AuthUser?> VerifyAccessTokenAsync(string token) => VerifyAsync(token, AuthOptions.AccessAudience);

    public Task<AuthUser?> VerifyRefreshTokenAsync(string token) => VerifyAsync(token, AuthOptions.RefreshAudience);

    private string Issue(AuthUser user, string audience, double ttlSeconds)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return _handler.CreateToken(new SecurityTokenDescriptor
        {
            Claims = new Dictionary<string, object>
            {
                ["iat"] = now,
                ["exp"] = now + (long)Math.Floor(ttlSeconds),
                ["aud"] = audience,
                ["iss"] = AuthOptions.Issuer,
                ["sub"] = user.Sub,
            },
            SigningCredentials = _credentials,
        });
    }

    private async Task<AuthUser?> VerifyAsync(string token, string audience)
    {
        var result = await _handler.ValidateTokenAsync(token, new TokenValidationParameters
        {
            IssuerSigningKey = _key,
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ValidIssuer = AuthOptions.Issuer,
            ValidAudience = audience,
            RequireExpirationTime = false,
            ClockSkew = TimeSpan.Zero,
        });
        if (!result.IsValid) return null;
        return result.Claims.TryGetValue("sub", out var sub) && sub is string { Length: > 0 } login ? new AuthUser(login) : null;
    }
}
