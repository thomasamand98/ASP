using Ganss.Xss;

namespace Horizon.Web.Features.Documents;

// HTML venant de la base ou du mode code, on garde les attributs de l'éditeur
public static class TemplateHtmlSanitizer
{
    private static readonly HtmlSanitizer Sanitizer = Create();

    private static HtmlSanitizer Create()
    {
        var sanitizer = new HtmlSanitizer { AllowDataAttributes = true };
        sanitizer.AllowedAttributes.Add("class");
        sanitizer.AllowedAttributes.Add("contenteditable");
        sanitizer.AllowedAttributes.Add("draggable");
        sanitizer.AllowedAttributes.Add("aria-label");
        sanitizer.AllowedAttributes.Add("aria-hidden");
        sanitizer.AllowedAttributes.Add("role");
        return sanitizer;
    }

    // HtmlSanitizer pas thread-safe
    private static readonly Lock Gate = new();

    public static string Sanitize(string html)
    {
        lock (Gate) return Sanitizer.Sanitize(html);
    }
}
