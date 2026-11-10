namespace Horizon.Shared.Models;

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

// avec le HTML des 3 zones, absent de la liste
public sealed record DocumentTemplateDetail
{
    public string IDDOCUMENT_TEMPLATES { get; init; } = "";
    public string Nom { get; init; } = "";
    public string Type_document { get; init; } = "";
    public string? Description { get; init; }
    public string? Date_heure_creation { get; init; }
    public string? Date_heure_modification { get; init; }
    public string? Contenu_entete { get; init; }
    public string Contenu_html { get; init; } = "";
    public string? Contenu_pied { get; init; }
}
