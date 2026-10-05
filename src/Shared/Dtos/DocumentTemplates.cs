namespace Horizon.Shared;

// Champs qu'un client peut envoyer pour créer/modifier un modèle de
// document. Mêmes clés que DocumentTemplate côté frontend (voir
// frontend/src/features/documents/types.ts). Type_document désigne le type
// d'enregistrement pour lequel le modèle est utilisable (ex. "CONTRAT") —
// une liste fermée connue du code, pas une catégorie d'énumération éditable
// par l'utilisateur. Contenu_entete/Contenu_pied : en-tête et pied de page,
// répétés sur chaque page à l'impression — optionnels.
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

// Les trois zones d'un modèle (voir DocumentTemplateEditor.tsx : en-tête /
// contenu / pied, chacune éditée indépendamment).
public record DocumentTemplateContent(string? Contenu_entete, string Contenu_html, string? Contenu_pied);
