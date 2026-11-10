using System.Net.Http.Headers;
using System.Text;

namespace Horizon.Web.Common;

// à la main, MultipartFormDataContent encode les accents en RFC 2047
public static class Uploads
{
    public static HttpContent Form(IEnumerable<(string Name, string Value)> fields, string field, string fileName, string? contentType, byte[] bytes)
    {
        var boundary = "----HorizonFormBoundary" + Guid.NewGuid().ToString("N");
        using var body = new MemoryStream();
        void Write(string text) => body.Write(Encoding.UTF8.GetBytes(text));
        foreach (var (name, value) in fields)
        {
            Write($"--{boundary}\r\nContent-Disposition: form-data; name=\"{Escape(name)}\"\r\n\r\n{value}\r\n");
        }
        Write($"--{boundary}\r\nContent-Disposition: form-data; name=\"{Escape(field)}\"; filename=\"{Escape(fileName)}\"\r\n");
        Write($"Content-Type: {(string.IsNullOrEmpty(contentType) ? "application/octet-stream" : contentType)}\r\n\r\n");
        body.Write(bytes);
        Write($"\r\n--{boundary}--\r\n");
        var content = new ByteArrayContent(body.ToArray());
        content.Headers.ContentType = MediaTypeHeaderValue.Parse($"multipart/form-data; boundary={boundary}");
        return content;
    }

    // onglet Documents : type, referentId puis le fichier
    public static HttpContent Form((string, string) first, (string, string) second, string fileName, string? contentType, byte[] bytes) =>
        Form([first, second], "file", fileName, contentType, bytes);

    private static string Escape(string text) => text.Replace("\"", "%22").Replace("\r", "%0D").Replace("\n", "%0A");
}
