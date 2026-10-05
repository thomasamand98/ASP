namespace Horizon.Shared;

// Corps des requêtes de l'écran Planning (frontend/src/features/planning/).
// Les ids sont des strings côté JSON, les dates des strings ISO — convertis
// et vérifiés par le service.

// Déplacement / redimensionnement d'une exécution par glisser-déposer. Les
// champs chauffeurId / remorqueId ne sont envoyés que si la carte a changé
// de ligne (null = « Non affecté ») ; absents, l'affectation est conservée.
public class MovePlanningExecutionDto
{
    [Text(Required = true)] public Opt<string> start { get; set; }
    [Text(Required = true)] public Opt<string> end { get; set; }
    [Id(Nullable = true)] public Opt<string?> chauffeurId { get; set; }
    [Id(Nullable = true)] public Opt<string?> remorqueId { get; set; }
}

// Création d'une exécution en déposant une commande sur le planning.
public class CreatePlanningExecutionDto
{
    [Id(Required = true)] public Opt<string> commandeId { get; set; }
    [Text(Required = true)] public Opt<string> start { get; set; }
    [Text(Required = true)] public Opt<string> end { get; set; }
    [Id(Required = true, Nullable = true)] public Opt<string?> chauffeurId { get; set; }
    [Id(Required = true, Nullable = true)] public Opt<string?> tracteurId { get; set; }
    [Id(Required = true, Nullable = true)] public Opt<string?> remorqueId { get; set; }
}
