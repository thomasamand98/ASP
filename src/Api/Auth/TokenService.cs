using System.Security.Cryptography;
using System.Text;
using Api.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Api.Auth;

// Identité portée par un jeton : volontairement réduite à l'identifiant
// (revendication `sub`) — rien d'autre ne circule dans le jeton.
public record AuthUser(string Sub);

// Vérification des identifiants et émission/validation des jetons JWT
// (équivalent de AuthService). Appelé par AuthController et par
// BearerAuthenticationHandler.
public class TokenService
{
    private readonly JsonWebTokenHandler _handler = new() { SetDefaultTimesOnTokenCreation = false, MapInboundClaims = false };
    private readonly SigningCredentials _credentials;
    private readonly SymmetricSecurityKey _key;
    private readonly AuthOptions _options;

    public TokenService(IOptions<AuthOptions> options, ILogger<TokenService> logger)
    {
        _options = options.Value;
        // JWT_SECRET : au moins 32 caractères. Absent, on génère un secret
        // aléatoire propre à ce processus — jamais un secret connu par défaut —
        // et on prévient : les sessions ne survivent alors pas à un redémarrage.
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
        // Algorithme imposé à la signature comme à la vérification.
        _credentials = new SigningCredentials(_key, SecurityAlgorithms.HmacSha256);
    }

    // ⚠️ AUTHENTIFICATION FACTICE (temporaire), reprise telle quelle du backend
    // NestJS : accepte n'importe quel couple identifiant / mot de passe, sans
    // consulter la base. Elle ne protège donc rien : quiconque atteint
    // /auth/login obtient un jeton valide. Seul point à remplacer pour une
    // vraie authentification — vérifier le hachage de
    // `utilisateurs.Password_hash` (et `Actif`), puis renvoyer null si les
    // identifiants sont incorrects. Le reste (jetons, front) n'a pas à changer.
    public Task<AuthUser?> AuthenticateAsync(string login, string password) => Task.FromResult<AuthUser?>(new AuthUser(login));

    public string IssueAccessToken(AuthUser user) => Issue(user, AuthOptions.AccessAudience, _options.AccessTtlSeconds);

    public string IssueRefreshToken(AuthUser user) => Issue(user, AuthOptions.RefreshAudience, AuthOptions.RefreshTtlSeconds);

    // Renvoie l'identité si le jeton est valide (signature, expiration,
    // émetteur, audience), sinon null.
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
