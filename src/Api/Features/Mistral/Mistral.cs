using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using Api.Common;
using Api.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Api.Features.Mistral;

// Extraction structurée demandée en plus de l'OCR : `Schema` est un JSON
// Schema (objet racine) que Mistral remplit à partir du document entier,
// `Prompt` une consigne optionnelle pour guider ce remplissage.
public record OcrAnnotation(JsonObject Schema, string? Prompt);

// Erreur renvoyée par l'API Mistral (statut HTTP non réussi).
public class MistralException(int statusCode, string body) : Exception($"Mistral {statusCode} : {body}")
{
    public int StatusCode { get; } = statusCode;
    public string Body { get; } = body;
}

// ===== SERVICE =====
// Appelle l'API REST de Mistral (il n'existe pas de SDK .NET officiel ; le
// backend NestJS utilisait @mistralai/mistralai, qui poste la même requête).
public class MistralService(HttpClient http, IOptions<AppOptions> options, ILogger<MistralService> logger)
{
    public const string OcrUrl = "https://api.mistral.ai/v1/ocr";

    // Traduit une erreur de l'API Mistral en HttpException lisible par le
    // front — sinon elle serait masquée en « Erreur interne du serveur ». Un
    // 401/403 de Mistral (clé invalide) devient un 502 : renvoyé tel quel, le
    // front le prendrait pour une session expirée (voir frontend/src/lib/api.ts).
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

    // Extrait le texte d'un fichier (PDF ou image) par OCR. Le fichier reçu
    // en binaire est transmis à Mistral en data URI base64, que l'API OCR
    // accepte directement, sans étape d'upload préalable via son API Files.
    // Les images passent par un chunk "image_url", les PDF par "document_url".
    // Renvoie le texte en Markdown (une page par élément, séparées par une
    // ligne vide) et, si `annotation` est fourni, l'extraction structurée du
    // document entier guidée par `annotation.Prompt` (JSON conforme à
    // `annotation.Schema`, déjà parsé).
    public async Task<object> RunOcrAsync(byte[] file, string mimeType, OcrAnnotation? annotation, CancellationToken ct)
    {
        var dataUri = $"data:{mimeType};base64,{Convert.ToBase64String(file)}";
        var request = new JsonObject
        {
            ["model"] = "mistral-ocr-latest",
            ["document"] = mimeType.StartsWith("image/", StringComparison.Ordinal)
                ? new JsonObject { ["type"] = "image_url", ["image_url"] = dataUri }
                : new JsonObject { ["type"] = "document_url", ["document_url"] = dataUri },
            // Tous les paramètres de l'API OCR, avec leur valeur par défaut
            // (null/false = comportement de l'API quand le champ est absent).
            // Pages à traiter (index à partir de 0). null = toutes les pages.
            ["pages"] = null,
            // Renvoie les images extraites en base64 dans page.images.
            ["include_image_base64"] = false,
            // Nombre max d'images extraites. null = pas de limite.
            ["image_limit"] = null,
            // Hauteur/largeur min (px) d'une image pour être extraite.
            ["image_min_size"] = null,
            // Format des tableaux : 'markdown' | 'html'. null = tableaux laissés
            // dans le markdown de la page.
            ["table_format"] = null,
            // Sort l'en-tête / le pied de page dans page.header / page.footer.
            ["extract_header"] = false,
            ["extract_footer"] = false,
            // Boîtes englobantes par paragraphe dans la réponse.
            ["include_blocks"] = false,
            // Scores de confiance : 'page' | 'word' | 'block'. null = aucun.
            ["confidence_scores_granularity"] = null,
            // Extraction structurée (json_schema uniquement) : sur chaque image /
            // boîte englobante, et sur le document entier (avec un prompt
            // optionnel, qui exige document_annotation_format).
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

// ===== CONTROLLER =====
[Route("mistral")]
public class MistralController(MistralService service) : ApiControllerBase
{
    // NestJS : POST mistral/ocr (multipart, champ « file »)
    // Upload multipart plutôt qu'une data URI base64 dans le JSON : le fichier
    // voyage en binaire et n'est pas soumis à la limite de 10 Mo du JSON.
    // 50 Mo = taille max acceptée par l'API OCR de Mistral ; au-delà, 413.
    // Champs texte optionnels : « schema » (JSON Schema sous forme de chaîne)
    // pour l'extraction structurée, et « prompt », la consigne qui la guide.
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
            // Mistral refuse un prompt d'annotation sans schéma de sortie.
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
