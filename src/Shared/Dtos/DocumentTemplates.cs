namespace Horizon.Shared;

// Type_document : liste fixée dans le code ("CONTRAT"...), pas une énumération
public class CreateDocumentTemplateDto
{
    public const string TypeContrat = "CONTRAT";

    [Text(Required = true)] public Opt<string> Nom { get; set; }
    [OneOf(TypeContrat, Required = true)] public Opt<string> Type_document { get; set; }
    [Text] public Opt<string> Description { get; set; }
    [Text] public Opt<string> Contenu_entete { get; set; }
    [Text(Required = true)] public Opt<string> Contenu_html { get; set; }
    [Text] public Opt<string> Contenu_pied { get; set; }
}

[Partial]
public class UpdateDocumentTemplateDto : CreateDocumentTemplateDto;

// en-tête, contenu, pied
public record DocumentTemplateContent(string? Contenu_entete, string Contenu_html, string? Contenu_pied);
