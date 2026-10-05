namespace Horizon.Shared.Models;

// Un point du graphique du chiffre d'affaires (GET /statistiques/chiffre-affaires) :
// un mois (« AAAA-MM ») ou un jour (« AAAA-MM-JJ ») de la période, avec les
// montants correspondants un an plus tôt (N-1).
public sealed record ChiffreAffairesPoint(string cle, double facture, double factureN1, double production, double productionN1);

public sealed record ChiffreAffairesResponse(string granularite, List<ChiffreAffairesPoint> points);
