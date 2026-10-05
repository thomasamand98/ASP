using System.Globalization;

namespace Horizon.Web.Common;

// Dates locales (heure du navigateur) du planning, de la feuille de pointage
// et des filtres de période. Une « date » est un DateTime sans fuseau, en
// heure locale, comme un Date de JavaScript manipulé avec getDate/setDate.
public static class DateUtils
{
    public const int MinuteMs = 60_000;
    public const int DayMinutes = 24 * 60;
    public const int SnapMinutes = 15;

    public static DateTime StartOfDay(DateTime date) => date.Date;

    // Semaine du lundi au dimanche.
    public static DateTime StartOfWeek(DateTime date) => date.Date.AddDays(-(((int)date.DayOfWeek + 6) % 7));

    public static DateTime AddDays(DateTime date, int days) => date.AddDays(days);

    // Jours de `from` à `to` inclus.
    public static List<DateTime> DaysInRange(DateTime from, DateTime to)
    {
        var days = new List<DateTime>();
        for (var d = from.Date; d <= to; d = d.AddDays(1)) days.Add(d);
        return days;
    }

    public static bool IsSameDay(DateTime a, DateTime b) => a.Date == b.Date;

    public static bool IsWeekend(DateTime date) => date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

    // Valeur d'un <input type="date"> (AAAA-MM-JJ, heure locale).
    public static string ToInputValue(DateTime date) => date.ToString("yyyy'-'MM'-'dd", CultureInfo.InvariantCulture);

    public static DateTime? FromInputValue(string? value)
    {
        var parts = (value ?? "").Split('-');
        if (parts.Length < 3) return null;
        if (!int.TryParse(parts[0], out var y) || !int.TryParse(parts[1], out var m) || !int.TryParse(parts[2], out var d)) return null;
        if (y == 0 || m == 0 || d == 0) return null;
        // new Date(y, m - 1, d) : débordements reportés (31/02 → 03/03).
        try
        {
            return new DateTime(y, 1, 1).AddMonths(m - 1).AddDays(d - 1);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    // Arrondi JavaScript (Math.round : moitié vers +∞).
    public static double JsRound(double value) => Math.Floor(value + 0.5);

    public static double Snap(double minutes) => JsRound(minutes / SnapMinutes) * SnapMinutes;

    public static double Clamp(double value, double min, double max) => Math.Min(Math.Max(value, min), max);

    // Répartit en « couloirs » les éléments qui se chevauchent, pour les
    // afficher côte à côte au lieu de les empiler. `Lanes` est le nombre de
    // couloirs du groupe de chevauchement auquel appartient l'élément.
    public static Dictionary<string, (int Lane, int Lanes)> LayoutLanes(IEnumerable<(string Id, double Start, double End)> items)
    {
        var result = new Dictionary<string, (int Lane, int Lanes)>();
        var sorted = items.OrderBy(i => i.Start).ThenBy(i => i.End).ToList();
        var cluster = new List<(string Id, int Lane)>();
        var laneEnds = new List<double>();
        var clusterEnd = double.NegativeInfinity;

        void Flush()
        {
            foreach (var (id, lane) in cluster) result[id] = (lane, laneEnds.Count);
            cluster.Clear();
            laneEnds.Clear();
            clusterEnd = double.NegativeInfinity;
        }

        foreach (var item in sorted)
        {
            if (item.Start >= clusterEnd) Flush();
            var lane = laneEnds.FindIndex(end => end <= item.Start);
            if (lane == -1)
            {
                lane = laneEnds.Count;
                laneEnds.Add(item.End);
            }
            else
            {
                laneEnds[lane] = item.End;
            }
            cluster.Add((item.Id, lane));
            clusterEnd = Math.Max(clusterEnd, item.End);
        }
        Flush();
        return result;
    }
}
