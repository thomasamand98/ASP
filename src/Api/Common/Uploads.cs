using System.Text.Json;

namespace Api.Common;

// un seul fichier "file", 50 Mo max
public static class Uploads
{
    public const long MaxFileSize = 50 * 1024 * 1024;

    public record ReceivedFile(byte[] Buffer, string OriginalName, string MimeType);

    public record Form(ReceivedFile? File, IReadOnlyDictionary<string, string> Fields)
    {
        public string? Field(string name) => Fields.GetValueOrDefault(name);
    }

    private static readonly IReadOnlyDictionary<string, string> NoFields = new Dictionary<string, string>();

    public static async Task<Form> ReadAsync(HttpRequest request, CancellationToken ct = default)
    {
        if (!request.HasFormContentType)
        {
            // JSON accepté aussi, lu comme des champs
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
            // vider le corps, sinon coupure au lieu du 413
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
