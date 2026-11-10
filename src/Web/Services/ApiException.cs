using System.Text.Json;

namespace Horizon.Web.Services;

// message du serveur, déjà en français, affichable tel quel
public sealed class ApiException(string message, int status) : Exception(message)
{
    public int Status { get; } = status;

    // erreurs de l'API toujours au format { statusCode, message }
    public static async Task<string> ReadMessageAsync(HttpResponseMessage response)
    {
        try
        {
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            if (document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("message", out var message)
                && message.ValueKind == JsonValueKind.String
                && message.GetString() is { Length: > 0 } text)
            {
                return text;
            }
        }
        catch (JsonException)
        {
            // pas du JSON (proxy...), message générique
        }
        return $"Erreur {(int)response.StatusCode}";
    }
}
