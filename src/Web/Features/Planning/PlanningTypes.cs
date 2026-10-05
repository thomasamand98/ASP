using Horizon.Web.Common;
using Horizon.Shared.Models;

namespace Horizon.Web.Features.Planning;

// Formes manipulées par l'écran Planning, construites depuis GET /planning :
// les dates ISO de l'API sont converties en millisecondes (heure locale)
// pour simplifier les calculs de position sur la grille.
public sealed record PlanningExecution(string Id, string? CommandeId, string? ChauffeurId, string? RemorqueId, string? Tracteur, string? RemorqueImmat,
    double Start, double End, string Client, PlanningMarchandise Marchandise, string? Reference, string Depart, string Arrivee, string Statut,
    string Nature, double Montant, string RefPlanning, string? Instruction)
{
    public static PlanningExecution From(ApiExecution e) => new(e.id, e.commandeId, e.chauffeurId, e.remorqueId, e.tracteur, e.remorqueImmat,
        Clock.Parse(e.start), Clock.Parse(e.end), e.client, e.marchandise, e.reference, e.depart, e.arrivee, e.statut, e.nature, e.montant,
        e.refPlanning, e.instruction);
}

public sealed record CommandeEnCours(string Id, string Numero, double? Date, string Client, PlanningMarchandise Marchandise, string? Reference,
    string Depart, string Arrivee, double Qt, double QtPlanifie, double DureeMinutes, double PrixUnitaire, string? Instruction);

public sealed record DechargementEnAttente(string Id, string Client, PlanningMarchandise Marchandise, string? Reference, string Depart, string Arrivee,
    string? Tracteur, string? RemorqueId, string? RemorqueImmat, string? ChauffeurId, double? DateChargement, double DureeMinutes, double Montant);

public sealed record PlanningData(List<PlanningChauffeur> Chauffeurs, List<PlanningRemorque> Remorques, List<PlanningExecution> Executions,
    List<CommandeEnCours> Commandes, List<DechargementEnAttente> Dechargements)
{
    public static PlanningData From(ApiPlanning api) => new(
        api.chauffeurs,
        api.remorques,
        [.. api.executions.Select(PlanningExecution.From)],
        [.. api.commandes.Select(c => new CommandeEnCours(c.id, c.numero, Optional(c.date), c.client, c.marchandise, c.reference, c.depart, c.arrivee,
            c.qt, c.qtPlanifie, c.dureeMinutes, c.prixUnitaire, c.instruction))],
        [.. api.dechargements.Select(d => new DechargementEnAttente(d.id, d.client, d.marchandise, d.reference, d.depart, d.arrivee, d.tracteur,
            d.remorqueId, d.remorqueImmat, d.chauffeurId, Optional(d.dateChargement), d.dureeMinutes, d.montant))]);

    private static double? Optional(string? iso) => string.IsNullOrEmpty(iso) ? null : Clock.Parse(iso);
}

// Ligne (mode ligne) ou colonne (mode colonne) du planning.
public sealed record PlanningResource(string Id, string Label, string? Sublabel);

// Élément en cours de glisser-déposer (« commande », « dechargement » ou
// « execution »). GrabOffsetMinutes : décalage entre le début de la carte et
// l'endroit où elle a été saisie, pour qu'une carte déplacée ne « saute » pas
// sous le curseur.
public sealed record DragItem(string Kind, string Id, double DurationMinutes, double GrabOffsetMinutes);

public sealed record DropPreview(string ResourceId, double Start, double End);

// Portion d'une exécution visible dans un intervalle (une journée en mode
// colonne, la période en mode ligne).
public sealed record Segment(string Id, PlanningExecution Execution, double Start, double End, bool ContinuesBefore, bool ContinuesAfter);

public static class PlanningBoard
{
    public static readonly IReadOnlyDictionary<string, string> StatutLabels = new Dictionary<string, string>
    {
        ["planifie"] = "Planifiée",
        ["en_cours"] = "En cours",
        ["termine"] = "Terminée",
    };

    // Ligne/colonne regroupant les exécutions sans chauffeur (ou sans
    // remorque en mode « Remorque ») — affichée seulement s'il y en a.
    public const string UnassignedId = "__non_affecte__";

    public static string ResourceIdOf(PlanningExecution execution, string groupBy) =>
        (groupBy == "executant" ? execution.ChauffeurId : execution.RemorqueId) ?? UnassignedId;

    public static List<Segment> ClipToRange(IEnumerable<PlanningExecution> executions, double from, double to) =>
    [
        .. executions
            .Where(e => e.End > from && e.Start < to)
            .Select(e => new Segment(e.Id, e, Math.Max(e.Start, from), Math.Min(e.End, to), e.Start < from, e.End > to)),
    ];

    public static Dictionary<string, (int Lane, int Lanes)> Lanes(IEnumerable<Segment> segments) =>
        DateUtils.LayoutLanes(segments.Select(s => (s.Id, s.Start, s.End)));

    // Valeur CSS numérique (style React : nombre → px), écrite comme en JavaScript.
    public static string Px(double value) => $"{Horizon.Shared.JsNumber.ToString(value)}px";

    public static string Num(double value) => Horizon.Shared.JsNumber.ToString(value);
}
