namespace Horizon.Web.Components.Projection;

// projection vers une table liée, filtrage côté API (?via=&ids=)
public sealed record EntityInfo(string Title, string Plural);

// DefaultField : champ prérempli quand on part d'une seule ligne
public sealed record Relation(string Target, string? DefaultField = null);

// Defaults : préremplissage du Nouveau, projection d'une seule ligne
public sealed record ProjectionView(string Source, IReadOnlyList<string> Ids, string Heading, IReadOnlyDictionary<string, string> Defaults)
{
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

    // à garder aligné sur les Projections des services de l'API
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
            // liés via la fiche chauffeur, rien à préremplir
            new("attelages"),
        ],
        ["vehicules"] = [new("societes"), new("attelages")],
        ["attelages"] = [new("chauffeurs"), new("vehicules"), new("personnel")],
        ["pointage"] = [new("personnel")],
        ["points"] = [new("societes"), new("contacts")],
        ["marchandises"] = [new("commandes")],
    };
}

// en cascade depuis ProjectionProvider
public sealed class ProjectionContext(Action<string, string, IReadOnlyList<string>, string?> open)
{
    // ex. "Contrats de ARDENNE CONTAINER" au lieu de "Contrats de 1 société"
    public void Open(string source, string target, IReadOnlyList<string> ids, string? singleLabel) => open(source, target, ids, singleLabel);
}
