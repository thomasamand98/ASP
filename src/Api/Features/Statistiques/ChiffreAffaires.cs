using System.Globalization;

namespace Api.Features.Statistiques;

// dates = jours à minuit UTC (colonnes DATE)
public static class ChiffreAffaires
{
    public const string Jour = "jour";
    public const string Mois = "mois";

    // au-delà de 2 mois, un point par mois
    private const int MaxJoursParJour = 62;

    public static string GranulariteDe(DateTime from, DateTime to) => (to - from).TotalDays + 1 > MaxJoursParJour ? Mois : Jour;

    // "AAAA-MM" ou "AAAA-MM-JJ"
    public static string CleIntervalle(DateTime date, string granularite) =>
        date.ToString(granularite == Mois ? "yyyy-MM" : "yyyy-MM-dd", CultureInfo.InvariantCulture);

    // intervalles vides compris
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

    // 29 février devient 28
    public static DateTime DecalerAnnee(DateTime date, int annees)
    {
        var year = date.Year + annees;
        return new DateTime(year, date.Month, Math.Min(date.Day, DateTime.DaysInMonth(year, date.Month)));
    }

    // decalage = 1 : montants N-1 rangés sur la période affichée
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

    // demi-centime vers le haut, pas d'arrondi bancaire
    public static double ArrondiCentimes(double value) => Math.Floor(value * 100 + 0.5) / 100;
}
