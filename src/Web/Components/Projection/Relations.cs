namespace Horizon.Web.Components.Projection;

// Projection : sélectionner des lignes d'une table puis afficher, dans un
// nouvel onglet, les enregistrements d'une table liée (ex. 3 sociétés →
// leurs contrats). Décrit les tables concernées et leurs liens directs ; le
// filtrage est fait côté serveur (?via=&ids=, voir Api/Common/Projection.cs et
// les Projections des services). Clé d'une table = son endpoint API
// (GET /societes...), aussi utilisée comme valeur de ?via=.
public sealed record EntityInfo(string Title, string Plural);

// DefaultField : champ de la fiche cible prérempli avec la ligne d'origine
// quand la projection part d'une seule ligne (« Nouveau » dans le résultat)
// — absent quand le lien ne se traduit pas par un champ unique.
public sealed record Relation(string Target, string? DefaultField = null);

// Ce que reçoit une page ouverte en résultat de projection.
//   Heading : en-tête et onglet, ex. « Contrats de 3 sociétés ».
//   Defaults : valeurs préremplies du « Nouveau » (projection d'une seule ligne).
public sealed record ProjectionView(string Source, IReadOnlyList<string> Ids, string Heading, IReadOnlyDictionary<string, string> Defaults)
{
    // Filtre envoyé à l'API par la liste (voir ApiList.Filters).
    public static Dictionary<string, string> Filters(ProjectionView? projection) =>
        projection is null ? [] : new() { ["via"] = projection.Source, ["ids"] = string.Join(',', projection.Ids) };

    public string? Default(string field) => Defaults.TryGetValue(field, out var value) ? value : null;
}

public static class Relations
{
    public static readonly IReadOnlyDictionary<string, EntityInfo> Entities = new Dictionary<string, EntityInfo>
    {
        ["societes"] = new("Sociétés", "sociétés"),
        ["contrats"] = new("Contrats", "contrats"),
        ["commandes"] = new("Commandes", "commandes"),
        ["contacts"] = new("Contacts", "contacts"),
        ["chauffeurs"] = new("Chauffeurs", "chauffeurs"),
        ["personnel"] = new("Personnel", "membres du personnel"),
        ["vehicules"] = new("Véhicules", "véhicules"),
        ["attelages"] = new("Attelages", "attelages"),
        ["pointage"] = new("Pointages", "pointages"),
        ["points"] = new("Points", "points"),
        ["marchandises"] = new("Marchandises", "marchandises"),
    };

    // Liens directs proposés par le bouton Projection de chaque table — pour
    // aller plus loin, on refait une projection depuis le résultat. Doit
    // rester aligné sur les Projections des services de l'API.
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<Relation>> Of = new Dictionary<string, IReadOnlyList<Relation>>
    {
        ["societes"] =
        [
            new("contrats", "IDSOCIETES"),
            new("contacts", "IDSOCIETES"),
            new("vehicules", "IDSOCIETES"),
            new("chauffeurs", "IDSOCIETES"),
            new("points", "IDSOCIETES"),
        ],
        ["contrats"] = [new("societes"), new("commandes", "IDCONTRATS")],
        ["commandes"] = [new("contrats"), new("marchandises")],
        ["contacts"] = [new("societes"), new("points")],
        ["chauffeurs"] = [new("societes"), new("personnel"), new("attelages", "IDCHAUFFEUR")],
        ["personnel"] =
        [
            new("chauffeurs", "IDPERSONNELS"),
            new("pointage", "IDPERSONNELS"),
            // Attelages de référence : liés au salarié via sa fiche chauffeur,
            // pas de champ à préremplir.
            new("attelages"),
        ],
        ["vehicules"] = [new("societes"), new("attelages")],
        ["attelages"] = [new("chauffeurs"), new("vehicules"), new("personnel")],
        ["pointage"] = [new("personnel")],
        ["points"] = [new("societes"), new("contacts")],
        ["marchandises"] = [new("commandes")],
    };
}

// Ouverture d'une projection, fournie par ProjectionProvider (paramètre en
// cascade) aux pages tableau.
public sealed class ProjectionContext(Action<string, string, IReadOnlyList<string>, string?> open)
{
    // singleLabel : libellé de la ligne quand une seule est sélectionnée
    // (« Contrats de ARDENNE CONTAINER » plutôt que « Contrats de 1 société »).
    public void Open(string source, string target, IReadOnlyList<string> ids, string? singleLabel) => open(source, target, ids, singleLabel);
}
