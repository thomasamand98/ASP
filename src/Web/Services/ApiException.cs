using System.Text.Json;

namespace Horizon.Web.Services;

// Erreur renvoyée par l'API : `Message` est celui du serveur (déjà en
// français, ex. « Vous devez renseigner le nom »), affichable tel quel.
public sealed class ApiException(string message, int status) : Exception(message)
{
    public int Status { get; } = status;

    // L'API répond toujours { statusCode, message } en cas d'erreur (voir
    // Api/Common/ExceptionMiddleware.cs).
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
            // Corps absent ou non JSON (ex. réponse d'un proxy) : message générique.
        }
        return $"Erreur {(int)response.StatusCode}";
    }
}
