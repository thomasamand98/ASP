using Ganss.Xss;

namespace Horizon.Web.Features.Documents;

// Le HTML d'un modèle vient de la base (donc potentiellement d'un autre
// utilisateur) ou du mode code de l'éditeur : il est nettoyé avant d'être
// posé dans le canvas, pour qu'aucun <script>, attribut on* ou lien
// javascript: ne s'exécute dans l'application. class/contenteditable/
// draggable/data-* (attributs propres à l'éditeur, voir interop.js
// docEditor) sont conservés.
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

    // HtmlSanitizer n'est pas garanti thread-safe pour des appels concurrents
    // (voir sa documentation) ; l'éditeur ne sanitize jamais deux zones à la
    // fois, un verrou simple suffit.
    private static readonly Lock Gate = new();

    public static string Sanitize(string html)
    {
        lock (Gate) return Sanitizer.Sanitize(html);
    }
}
