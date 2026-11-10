namespace Horizon.Shared.Models;

// Couleur = #rrggbb de la marchandise
public sealed record PlanningMarchandise(string id, string nom, string couleur);

public sealed record PlanningChauffeur(string id, string nom, string? sousTraitant, string? remorqueParDefautId, string? tracteurParDefautId, string? tracteurParDefaut);

public sealed record PlanningRemorque(string id, string immat, string type);

// nature chargement = déchargement différé
public sealed record ApiExecution(string id, string? commandeId, string? chauffeurId, string? remorqueId, string? tracteur, string? remorqueImmat,
    string start, string end, string client, PlanningMarchandise marchandise, string? reference, string depart, string arrivee, string statut,
    string nature, double montant, string refPlanning, string? instruction);

public sealed record ApiCommandeEnCours(string id, string numero, string? date, string client, PlanningMarchandise marchandise, string? reference,
    string depart, string arrivee, double qt, double qtPlanifie, double dureeMinutes, double prixUnitaire, string? instruction);

public sealed record ApiDechargementEnAttente(string id, string client, PlanningMarchandise marchandise, string? reference, string depart, string arrivee,
    string? tracteur, string? remorqueId, string? remorqueImmat, string? chauffeurId, string? dateChargement, double dureeMinutes, double montant);

public sealed record ApiPlanning(List<PlanningChauffeur> chauffeurs, List<PlanningRemorque> remorques, List<ApiExecution> executions,
    List<ApiCommandeEnCours> commandes, List<ApiDechargementEnAttente> dechargements);
