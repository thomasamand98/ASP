using System.Net.Http.Json;
using Horizon.Shared;
using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace Horizon.Web.Services;

// Session de l'utilisateur. Le jeton d'accès (court, 15 min) n'est gardé
// qu'en mémoire — jamais dans localStorage, lisible par un script injecté.
// Recharger la page le fait disparaître : il est alors redemandé à l'API
// grâce au cookie de renouvellement httpOnly (voir RefreshAsync).
public sealed class AuthSession(HttpClient http, AppConfig config)
{
    // Session automatique, utilisée tant que l'écran de connexion est
    // désactivé (voir AppConfig.LoginScreen). L'API accepte pour l'instant
    // n'importe quel identifiant : ces valeurs sont un simple espace réservé,
    // et non de vrais identifiants. À supprimer avec la vraie authentification.
    private const string AutoLogin = "utilisateur";
    private const string AutoPassword = "session-automatique";

    private Task<string?>? _refreshInFlight;
    private Task<string>? _autoLoginInFlight;

    public string? AccessToken { get; private set; }

    public bool LoginScreenEnabled => config.LoginScreen;

    // Session perdue en cours d'utilisation (renouvellement refusé, voir
    // ApiClient) : AuthGate y branche le retour à l'écran de connexion.
    public event Action? SessionLost;

    public void NotifySessionLost()
    {
        AccessToken = null;
        SessionLost?.Invoke();
    }

    private sealed record SessionResponse(string accessToken, string login);

    // Les requêtes /auth incluent les cookies (`credentials: 'include'`) :
    // nécessaire pour que le navigateur envoie et enregistre le cookie de
    // renouvellement, l'API étant sur une autre origine.
    private Task<HttpResponseMessage> AuthRequestAsync(string path, object? body = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"auth/{path}");
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
        if (body is not null) request.Content = JsonContent.Create(body, options: JsonConfig.Options);
        return http.SendAsync(request);
    }

    // Renvoie l'identifiant de l'utilisateur connecté.
    public async Task<string> LoginAsync(string login, string password)
    {
        HttpResponseMessage response;
        try
        {
            response = await AuthRequestAsync("login", new { login, password });
        }
        catch (HttpRequestException)
        {
            throw new ApiException("Impossible de joindre le serveur", 0);
        }
        if (!response.IsSuccessStatusCode) throw new ApiException(await ApiException.ReadMessageAsync(response), (int)response.StatusCode);
        var session = (await response.Content.ReadFromJsonAsync<SessionResponse>(JsonConfig.Options))!;
        AccessToken = session.accessToken;
        return session.login;
    }

    // Plusieurs appels simultanés partagent une seule connexion, pour ne pas
    // consommer inutilement la limite de tentatives.
    public Task<string> LoginAutomaticallyAsync()
    {
        return _autoLoginInFlight ??= Run();

        async Task<string> Run()
        {
            try
            {
                return await LoginAsync(AutoLogin, AutoPassword);
            }
            finally
            {
                _autoLoginInFlight = null;
            }
        }
    }

    // Échange le cookie de renouvellement contre un nouveau jeton d'accès ;
    // renvoie l'identifiant, ou null sans session valide. Plusieurs appels
    // simultanés (chargement de page, plusieurs requêtes en 401 en même
    // temps) partagent une seule requête.
    public Task<string?> RefreshAsync()
    {
        return _refreshInFlight ??= Run();

        async Task<string?> Run()
        {
            try
            {
                var response = await AuthRequestAsync("refresh");
                if (!response.IsSuccessStatusCode)
                {
                    AccessToken = null;
                    return null;
                }
                var session = (await response.Content.ReadFromJsonAsync<SessionResponse>(JsonConfig.Options))!;
                AccessToken = session.accessToken;
                return session.login;
            }
            catch (HttpRequestException)
            {
                AccessToken = null;
                return null;
            }
            finally
            {
                _refreshInFlight = null;
            }
        }
    }

    public async Task LogoutAsync()
    {
        AccessToken = null;
        try
        {
            await AuthRequestAsync("logout");
        }
        catch (HttpRequestException)
        {
            // Serveur injoignable : la session locale est de toute façon terminée.
        }
    }
}
