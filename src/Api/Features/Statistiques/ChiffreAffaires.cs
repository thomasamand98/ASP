using System.Globalization;

namespace Api.Features.Statistiques;

// Découpage d'une période en intervalles (jours ou mois) pour le graphe du
// chiffre d'affaires du tableau de bord — logique pure, sans base de
// données (voir ChiffreAffairesTests). Toutes les dates sont des jours à
// minuit UTC, comme les colonnes DATE.
public static class ChiffreAffaires
{
    public const string Jour = "jour";
    public const string Mois = "mois";

    // Au-delà de deux mois, un point par jour devient illisible : on passe au mois.
    private const int MaxJoursParJour = 62;

    public static string GranulariteDe(DateTime from, DateTime to) => (to - from).TotalDays + 1 > MaxJoursParJour ? Mois : Jour;

    // Clé de l'intervalle contenant `date` : « AAAA-MM » ou « AAAA-MM-JJ ».
    public static string CleIntervalle(DateTime date, string granularite) =>
        date.ToString(granularite == Mois ? "yyyy-MM" : "yyyy-MM-dd", CultureInfo.InvariantCulture);

    // Clés de tous les intervalles de la période, dans l'ordre (même vides).
    public static List<string> Intervalles(DateTime from, DateTime to, string granularite)
    {
        var cles = new List<string>();
        var courant = granularite == Mois ? new DateTime(from.Year, from.Month, 1).Add(from.TimeOfDay) : from;
        while (courant <= to)
        {
            cles.Add(CleIntervalle(courant, granularite));
            courant = granularite == Mois ? courant.AddMonths(1) : courant.AddDays(1);
        }
        return cles;
    }

    // Même jour décalé de `annees` ans ; un 29 février devient le 28.
    public static DateTime DecalerAnnee(DateTime date, int annees)
    {
        var year = date.Year + annees;
        return new DateTime(year, date.Month, Math.Min(date.Day, DateTime.DaysInMonth(year, date.Month)));
    }

    // Additionne des montants par intervalle. Les montants de N-1 (`decalage` =
    // 1) sont rangés dans l'intervalle de même rang de la période affichée.
    public static Dictionary<string, double> CumulerParIntervalle(IEnumerable<(DateTime? Date, double Montant)> lignes, string granularite, int decalage = 0)
    {
        var totaux = new Dictionary<string, double>();
        foreach (var (date, montant) in lignes)
        {
            if (date is null || montant == 0 || double.IsNaN(montant)) continue;
            var cle = CleIntervalle(decalage != 0 ? DecalerAnnee(date.Value, decalage) : date.Value, granularite);
            totaux[cle] = totaux.GetValueOrDefault(cle) + montant;
        }
        return totaux;
    }

    // Math.round de JavaScript : au centime, la demie arrondie vers le haut.
    public static double ArrondiCentimes(double value) => Math.Floor(value * 100 + 0.5) / 100;
}
