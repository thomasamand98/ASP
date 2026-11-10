namespace Horizon.Shared;

// ids et dates en texte, vérifiés par le service

// chauffeurId/remorqueId seulement si changement de ligne, null = Non affecté
public class MovePlanningExecutionDto
{
    [Text(Required = true)] public Opt<string> start { get; set; }
    [Text(Required = true)] public Opt<string> end { get; set; }
    [Id(Nullable = true)] public Opt<string?> chauffeurId { get; set; }
    [Id(Nullable = true)] public Opt<string?> remorqueId { get; set; }
}

// commande déposée sur le planning
public class CreatePlanningExecutionDto
{
    [Id(Required = true)] public Opt<string> commandeId { get; set; }
    [Text(Required = true)] public Opt<string> start { get; set; }
    [Text(Required = true)] public Opt<string> end { get; set; }
    [Id(Required = true, Nullable = true)] public Opt<string?> chauffeurId { get; set; }
    [Id(Required = true, Nullable = true)] public Opt<string?> tracteurId { get; set; }
    [Id(Required = true, Nullable = true)] public Opt<string?> remorqueId { get; set; }
}
