namespace Horizon.Shared.Models;

// Réponses de l'API — module Conditions d'exécution (GET /conditions).
// Type_Prestation : code de l'énumération « type_prestation » ; CMR_or_FDR :
// code 0/1.
public sealed record Condition(string IDCONDITIONS_EXECUTION, double? Type_Prestation, double? CMR_or_FDR, string? Libelle);
