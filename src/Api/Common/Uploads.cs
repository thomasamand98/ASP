using System.Text.Json;

namespace Api.Common;

// Fichiers envoyés en multipart/form-data (équivalent de
// FileInterceptor('file', { limits: { fileSize: 50 Mo, files: 1 } })).
public static class Uploads
{
    public const long MaxFileSize = 50 * 1024 * 1024;

    public record ReceivedFile(byte[] Buffer, string OriginalName, string MimeType);

    public record Form(ReceivedFile? File, IReadOnlyDictionary<string, string> Fields)
    {
        public string? Field(string name) => Fields.GetValueOrDefault(name);
    }

    private static readonly IReadOnlyDictionary<string, string> NoFields = new Dictionary<string, string>();

    // Lit le formulaire et son champ « file » (null si absent).
    public static async Task<Form> ReadAsync(HttpRequest request, CancellationToken ct = default)
    {
        if (!request.HasFormContentType)
        {
            // Corps JSON (sans fichier) : ses champs sont lus comme ceux d'un
            // formulaire, comme le fait Express.
            var json = await ZodBodyBinder.ReadJsonBodyAsync(request);
            if (json is not { ValueKind: JsonValueKind.Object } body) return new Form(null, NoFields);
            return new Form(null, body.EnumerateObject()
                .Where(property => property.Value.ValueKind is JsonValueKind.String or JsonValueKind.Number)
                .ToDictionary(property => property.Name, property => property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString()! : JsNumber.ToString(property.Value.GetDouble())));
        }
        IFormCollection form;
        try
        {
            form = await request.ReadFormAsync(ct);
        }
        catch (InvalidDataException)
        {
            // Envoi trop volumineux : le reste de la requête est lu et ignoré
            // (comme le fait Express), pour que le client reçoive bien la
            // réponse 413 au lieu d'une coupure de connexion.
            await request.Body.CopyToAsync(Stream.Null, ct);
            throw new PayloadTooLargeException("File too large");
        }
        var fields = form.ToDictionary(field => field.Key, field => field.Value.ToString());
        var file = form.Files.GetFile("file");
        if (file is null) return new Form(null, fields);
        if (file.Length > MaxFileSize) throw new PayloadTooLargeException("File too large");

        using var buffer = new MemoryStream((int)file.Length);
        await file.CopyToAsync(buffer, ct);
        return new Form(new ReceivedFile(buffer.ToArray(), file.FileName, file.ContentType), fields);
    }
}
