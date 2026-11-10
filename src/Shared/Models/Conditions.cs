namespace Horizon.Shared.Models;

// Type_Prestation : code énumération type_prestation
public sealed record Condition(string IDCONDITIONS_EXECUTION, double? Type_Prestation, double? CMR_or_FDR, string? Libelle);
