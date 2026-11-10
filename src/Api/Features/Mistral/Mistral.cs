using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using Api.Common;
using Api.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Api.Features.Mistral;

// Schema : JSON Schema rempli par Mistral sur tout le document, Prompt optionnel
public record OcrAnnotation(JsonObject Schema, string? Prompt);

public class MistralException(int statusCode, string body) : Exception($"Mistral {statusCode} : {body}")
{
    public int StatusCode { get; } = statusCode;
    public string Body { get; } = body;
}

// appel REST direct, pas de SDK .NET officiel
public class MistralService(HttpClient http, IOptions<AppOptions> options, ILogger<MistralService> logger)
{
    public const string OcrUrl = "https://api.mistral.ai/v1/ocr";

    // 401/403 Mistral (clé invalide) en 502, sinon le front croit à une session expirée
    private HttpException ToHttpException(MistralException error)
    {
        logger.LogWarning("Mistral {Status} : {Body}", error.StatusCode, error.Body);
        if (error.StatusCode == 429)
        {
            return new TooManyRequestsException("Limite de requêtes Mistral atteinte, réessayez dans quelques instants");
        }
        if (error.StatusCode is 401 or 403)
        {
            return new BadGatewayException("Clé API Mistral invalide ou non autorisée (MISTRAL_API_KEY)");
        }
        return new BadGatewayException($"Erreur du service Mistral (statut {error.StatusCode})");
    }

    // envoi en data URI base64, pas besoin de passer par l'API Files
    public async Task<object> RunOcrAsync(byte[] file, string mimeType, OcrAnnotation? annotation, CancellationToken ct)
    {
        var dataUri = $"data:{mimeType};base64,{Convert.ToBase64String(file)}";
        var request = new JsonObject
        {
            ["model"] = "mistral-ocr-latest",
            ["document"] = mimeType.StartsWith("image/", StringComparison.Ordinal)
                ? new JsonObject { ["type"] = "image_url", ["image_url"] = dataUri }
                : new JsonObject { ["type"] = "document_url", ["document_url"] = dataUri },
            // tous les paramètres listés avec leur valeur par défaut
            ["pages"] = null,
            ["include_image_base64"] = false,
            ["image_limit"] = null,
            ["image_min_size"] = null,
            // 'markdown' | 'html', null = tableaux dans le markdown de la page
            ["table_format"] = null,
            ["extract_header"] = false,
            ["extract_footer"] = false,
            ["include_blocks"] = false,
            // 'page' | 'word' | 'block'
            ["confidence_scores_granularity"] = null,
            ["bbox_annotation_format"] = null,
            ["document_annotation_format"] = annotation is null
                ? null
                : new JsonObject
                {
                    ["type"] = "json_schema",
                    ["json_schema"] = new JsonObject { ["name"] = "extraction", ["schema"] = annotation.Schema.DeepClone(), ["strict"] = true },
                },
            ["document_annotation_prompt"] = string.IsNullOrEmpty(annotation?.Prompt) ? null : annotation.Prompt,
        };

        JsonElement response;
        try
        {
            response = await PostAsync(request, ct);
        }
        catch (MistralException error)
        {
            throw ToHttpException(error);
        }

        var pages = response.GetProperty("pages").EnumerateArray().Select(page => page.GetProperty("markdown").GetString());
        var documentAnnotation = response.TryGetProperty("document_annotation", out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        return new
        {
            text = string.Join("\n\n", pages),
            annotation = string.IsNullOrEmpty(documentAnnotation) ? (JsonNode?)null : JsonNode.Parse(documentAnnotation),
        };
    }

    private async Task<JsonElement> PostAsync(JsonObject body, CancellationToken ct)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, OcrUrl);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.Value.MistralApiKey ?? "");
        message.Content = new StringContent(body.ToJsonString(), new MediaTypeHeaderValue("application/json"));
        using var response = await http.SendAsync(message, ct);
        var text = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode) throw new MistralException((int)response.StatusCode, text);
        using var document = JsonDocument.Parse(text);
        return document.RootElement.Clone();
    }
}

[Route("mistral")]
public class MistralController(MistralService service) : ApiControllerBase
{
    // multipart pour échapper à la limite JSON de 10 Mo, 50 Mo max côté Mistral
    [HttpPost("ocr")]
    public async Task<IActionResult> Ocr(CancellationToken ct)
    {
        var form = await Uploads.ReadAsync(Request, ct);
        if (form.File is null) throw new BadRequestException("Fichier requis (champ \"file\")");
        if (form.File.MimeType != "application/pdf" && !form.File.MimeType.StartsWith("image/", StringComparison.Ordinal))
        {
            throw new UnsupportedMediaTypeException("Seuls les PDF et les images sont acceptés");
        }
        var annotation = ParseAnnotation(form.Field("schema"), form.Field("prompt"));
        return StatusCode(StatusCodes.Status201Created, await service.RunOcrAsync(form.File.Buffer, form.File.MimeType, annotation, ct));
    }

    private static OcrAnnotation? ParseAnnotation(string? schema, string? prompt)
    {
        if (string.IsNullOrWhiteSpace(schema))
        {
            // refusé par Mistral sans schéma
            if (!string.IsNullOrWhiteSpace(prompt)) throw new BadRequestException("Un prompt exige un schéma JSON (champ \"schema\")");
            return null;
        }
        JsonNode? parsed;
        try
        {
            parsed = JsonNode.Parse(schema);
        }
        catch (JsonException)
        {
            throw new BadRequestException("Le schéma JSON n'est pas un JSON valide");
        }
        if (parsed is not JsonObject root) throw new BadRequestException("Le schéma JSON doit être un objet");
        return new OcrAnnotation(root, string.IsNullOrWhiteSpace(prompt) ? null : prompt.Trim());
    }
}
