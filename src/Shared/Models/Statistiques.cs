namespace Horizon.Shared.Models;

// un mois (AAAA-MM) ou un jour (AAAA-MM-JJ), avec N-1
public sealed record ChiffreAffairesPoint(string cle, double facture, double factureN1, double production, double productionN1);

public sealed record ChiffreAffairesResponse(string granularite, List<ChiffreAffairesPoint> points);
