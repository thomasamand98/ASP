using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Horizon.Shared;

namespace Horizon.Web.Services;

// sur 401 : un renouvellement de session puis on rejoue la requête
public sealed class ApiClient(HttpClient http, AuthSession session)
{
    // build rappelé pour rejouer la requête
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
                // même message que fetch()
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

    // corps vide possible (DELETE)
    public async Task<T?> SendJsonAsync<T>(HttpMethod method, string path, object? body = null)
    {
        var response = await SendAsync(method, path, body);
        var text = await response.Content.ReadAsStringAsync();
        return text.Length == 0 ? default : JsonSerializer.Deserialize<T>(text, JsonConfig.Options);
    }
}
