namespace Horizon.Web.Features.Documents;

// Arbre des champs de fusion disponibles pour l'éditeur de modèles de
// document (voir DocumentTemplateEditor.razor). Un champ « simple » se
// dépose sous forme d'un jeton dans le texte ; un champ de type « liste » se
// dépose sous forme d'un tableau répétable (une ligne par élément de la
// liste au moment de la fusion, côté backend — non implémenté ici).
//
// Les chemins (`Path`) reprennent le nom des entités exposées par l'API,
// pour qu'un futur moteur de fusion puisse les résoudre sans table de
// correspondance supplémentaire. `[]` marque une collection : un champ sous
// une liste a un chemin du type `Contrat.Prestations[].Prix_unitaire`.

// Kind "image" (ex. Entite.Logo) : le champ se dépose comme une vignette
// image plutôt qu'un jeton texte, résolue à la génération contre la data URI
// base64 du logo.
public sealed record MergeField(string Path, string Label, string Kind);

public sealed record MergeFieldGroup(
    string Id,
    string Label,
    IReadOnlyList<MergeField> Fields,
    // Chemin de la collection pour un groupe de type liste (répétable).
    string? ListPath = null,
    // Sous-listes imbriquées (ex. les suppléments d'une prestation).
    IReadOnlyList<MergeFieldGroup>? Children = null,
    // Colonnes proposées par défaut quand on dépose le groupe entier (tableau
    // répétable) — sous-ensemble de Fields, dans l'ordre d'affichage. À
    // défaut, les 4 premiers champs du groupe sont utilisés.
    IReadOnlyList<string>? DefaultColumns = null)
{
    public IReadOnlyList<MergeField> Columns(IReadOnlyList<string>? columnPaths = null)
    {
        var paths = columnPaths ?? DefaultColumns ?? [.. Fields.Take(4).Select(f => f.Path)];
        return [.. paths.Select(p => Fields.FirstOrDefault(f => f.Path == p)).OfType<MergeField>()];
    }
}

public static class MergeFields
{
    public static readonly IReadOnlyList<MergeFieldGroup> Groups =
    [
        new("emetteur", "Émetteur (notre société)",
        [
            new("Entite.Logo", "Logo", "image"),
            new("Entite.Nom_societe", "Raison sociale", "text"),
            new("Entite.Adresse.Adresse1", "Adresse", "text"),
            new("Entite.Adresse.CP", "Code postal", "text"),
            new("Entite.Adresse.Localite", "Localité", "text"),
            new("Entite.Adresse.Pays_full_name", "Pays", "text"),
            new("Entite.Num_TVA", "N° TVA", "text"),
            new("Entite.Num_Telephone", "Téléphone", "text"),
            new("Entite.Email_contact", "E-mail", "text"),
            new("Entite.Nom_Banque", "Banque", "text"),
            new("Entite.Iban", "IBAN", "text"),
            new("Entite.Bic", "BIC", "text"),
            new("Entite.Signataire", "Signataire", "text"),
        ]),
        new("contrat", "Contrat",
        [
            new("Contrat.Num_contrat", "N° contrat", "text"),
            new("Contrat.Version_contrat", "Version", "text"),
            new("Contrat.Date_debut", "Date de début", "date"),
            new("Contrat.Date_fin", "Date de fin", "date"),
            new("Contrat.Reference_client", "Référence client", "text"),
            new("Contrat.Taux_tva", "Taux TVA", "number"),
            new("Contrat.Instruction_CMR", "Instruction CMR", "text"),
            new("Contrat.Description_projet", "Description du projet", "text"),
            new("Contrat.Commissionnaire", "Commissionnaire", "text"),
            new("Contrat.TypeFacture.Nom", "Type de facture", "text"),
        ]),
        new("client", "Client (Société)",
        [
            new("Contrat.Societe.Nom_societe", "Nom société", "text"),
            new("Contrat.Societe.Denomination", "Dénomination", "text"),
            new("Contrat.Societe.TVA", "N° TVA", "text"),
            new("Contrat.Societe.Client.Numero_client", "N° client", "text"),
            new("Contrat.Societe.Client.Delai_paiement", "Délai de paiement", "text"),
            new("Contrat.Societe.Adresse.Adresse1", "Adresse", "text"),
            new("Contrat.Societe.Adresse.CP", "Code postal", "text"),
            new("Contrat.Societe.Adresse.Localite", "Localité", "text"),
            new("Contrat.Societe.Adresse.Pays_full_name", "Pays", "text"),
        ]),
        new("prestations", "Prestations",
            ListPath: "Contrat.Prestations[]",
            DefaultColumns:
            [
                "Contrat.Prestations[].Type",
                "Contrat.Prestations[].Description_prestation",
                "Contrat.Prestations[].Unite",
                "Contrat.Prestations[].Prix_unitaire",
            ],
            Fields:
            [
                new("Contrat.Prestations[].Type", "Type", "text"),
                new("Contrat.Prestations[].Description_prestation", "Description", "text"),
                new("Contrat.Prestations[].Description_courte", "Description courte", "text"),
                new("Contrat.Prestations[].Prix_unitaire", "P.U.", "number"),
                new("Contrat.Prestations[].Unite", "Unité", "text"),
                new("Contrat.Prestations[].Marchandise.Nom_marchandise", "Marchandise", "text"),
                new("Contrat.Prestations[].Instruction", "Instruction", "text"),
            ],
            Children:
            [
                new("prestations-supplementaires", "Prestations supplémentaires",
                    ListPath: "Contrat.Prestations[].PrestationSupplementaires[]",
                    DefaultColumns:
                    [
                        "Contrat.Prestations[].PrestationSupplementaires[].Description_prestation",
                        "Contrat.Prestations[].PrestationSupplementaires[].Unite",
                        "Contrat.Prestations[].PrestationSupplementaires[].Prix_unitaire",
                    ],
                    Fields:
                    [
                        new("Contrat.Prestations[].PrestationSupplementaires[].Description_prestation", "Description", "text"),
                        new("Contrat.Prestations[].PrestationSupplementaires[].Prix_unitaire", "P.U.", "number"),
                        new("Contrat.Prestations[].PrestationSupplementaires[].Unite", "Unité", "text"),
                    ]),
            ]),
        new("divers", "Divers",
        [
            new("Divers.Date_edition", "Date d’édition", "date"),
            new("Divers.CGV_URL", "Lien conditions générales de vente", "text"),
        ]),
    ];

    // Aplatit l'arbre (groupes + sous-groupes) pour la recherche et pour
    // retrouver un champ par chemin.
    public static IEnumerable<MergeFieldGroup> Flatten(IEnumerable<MergeFieldGroup> groups) =>
        groups.SelectMany(g => (IEnumerable<MergeFieldGroup>)[g, .. g.Children is null ? [] : Flatten(g.Children)]);

    public static MergeField? FindField(string path) => Flatten(Groups).SelectMany(g => g.Fields).FirstOrDefault(f => f.Path == path);

    public static MergeFieldGroup? FindGroup(string id) => Flatten(Groups).FirstOrDefault(g => g.Id == id);

    // Groupes de type liste (répétables), toutes profondeurs confondues —
    // utilisés pour le sélecteur « Insérer un bloc répétable » de la barre
    // d'outils.
    public static IEnumerable<MergeFieldGroup> ListGroups() => Flatten(Groups).Where(g => g.ListPath is not null);
}
