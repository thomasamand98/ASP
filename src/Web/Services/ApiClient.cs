using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Horizon.Shared;

namespace Horizon.Web.Services;

// Point d'appel unique vers l'API : préfixe l'URL de base, joint le jeton
// d'accès et lit les messages d'erreur du serveur. `path` est relatif à
// l'API, sans « / » initial (ex. "societes?page=1"). Tous les écrans passent
// par ici — aucun HttpClient utilisé directement.
//
// Sur un 401 (jeton d'accès expiré), la session est renouvelée une fois puis
// la requête rejouée ; si le renouvellement échoue, l'utilisateur est
// renvoyé à l'écran de connexion. Toute réponse non réussie lève une
// ApiException dont le message est celui du serveur.
public sealed class ApiClient(HttpClient http, AuthSession session)
{
    // `build` crée la requête (rappelé pour la rejouer après renouvellement).
    public async Task<HttpResponseMessage> SendAsync(Func<HttpRequestMessage> build)
    {
        async Task<HttpResponseMessage> Send()
        {
            var request = build();
            if (session.AccessToken is { } token) request.Headers.Authorization = new("Bearer", token);
            try
            {
                return await http.SendAsync(request);
            }
            catch (HttpRequestException)
            {
                // Même message que fetch() dans le navigateur.
                throw new ApiException("Failed to fetch", 0);
            }
        }

        var response = await Send();
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            if (await session.RefreshAsync() is not null) response = await Send();
            else session.NotifySessionLost();
        }
        if (!response.IsSuccessStatusCode) throw new ApiException(await ApiException.ReadMessageAsync(response), (int)response.StatusCode);
        return response;
    }

    public Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? body = null) =>
        SendAsync(() => new HttpRequestMessage(method, path)
        {
            Content = body is null ? null : JsonContent.Create(body, body.GetType(), options: JsonConfig.Options),
        });

    public async Task<T> GetAsync<T>(string path)
    {
        var response = await SendAsync(HttpMethod.Get, path);
        return (await response.Content.ReadFromJsonAsync<T>(JsonConfig.Options))!;
    }

    // Écriture (POST/PATCH/DELETE) : la réponse JSON est lue si elle en
    // contient une (DELETE renvoie un corps vide).
    public async Task<T?> SendJsonAsync<T>(HttpMethod method, string path, object? body = null)
    {
        var response = await SendAsync(method, path, body);
        var text = await response.Content.ReadAsStringAsync();
        return text.Length == 0 ? default : JsonSerializer.Deserialize<T>(text, JsonConfig.Options);
    }
}
