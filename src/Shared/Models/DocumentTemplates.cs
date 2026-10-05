namespace Horizon.Shared.Models;

// Réponses de l'API — modèles de document (GET /document-templates et
// /document-templates/:id).
public record DocumentTemplateSummary
{
    public string IDDOCUMENT_TEMPLATES { get; init; } = "";
    public string Nom { get; init; } = "";
    public string Type_document { get; init; } = "";
    public string? Description { get; init; }
    public string? Date_heure_creation { get; init; }
    public string? Date_heure_modification { get; init; }
}

public sealed record DocumentTemplatesResponse(List<DocumentTemplateSummary> documentTemplates);
