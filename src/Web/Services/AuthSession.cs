using System.Net.Http.Json;
using Horizon.Shared;
using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace Horizon.Web.Services;

// jeton d'accès en mémoire seulement, pas dans localStorage (XSS)
public sealed class AuthSession(HttpClient http, AppConfig config)
{
    // TODO à virer avec la vraie auth, l'API accepte n'importe quel login pour l'instant
    private const string AutoLogin = "utilisateur";
    private const string AutoPassword = "session-automatique";

    private Task<string?>? _refreshInFlight;
    private Task<string>? _autoLoginInFlight;

    public string? AccessToken { get; private set; }

    public bool LoginScreenEnabled => config.LoginScreen;

    // renouvellement refusé en cours de route
    public event Action? SessionLost;

    public void NotifySessionLost()
    {
        AccessToken = null;
        SessionLost?.Invoke();
    }

    private sealed record SessionResponse(string accessToken, string login);

    // cookies inclus pour le refresh, API sur une autre origine
    private Task<HttpResponseMessage> AuthRequestAsync(string path, object? body = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"auth/{path}");
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
        if (body is not null) request.Content = JsonContent.Create(body, options: JsonConfig.Options);
        return http.SendAsync(request);
    }

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

    // appels simultanés mutualisés, pour ménager la limite de tentatives
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

    // null sans session valide, une seule requête pour plusieurs 401 simultanés
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
            // serveur injoignable, tant pis
        }
    }
}
