using System.Globalization;
using Horizon.Shared;
using Horizon.Shared.Models;
using Horizon.Web.Common;

namespace Horizon.Web.Features.Pointages;

public sealed record SheetDay(DateTime Date, string Key, List<PointageFeuille> Pointages);

public sealed record SheetTotals
{
    public double Total { get; set; }
    public double Worked { get; set; }
    public double Coupure { get; set; }
    public double Nuit { get; set; }
    public double Liaison { get; set; }
    public double Standby { get; set; }
    public int JoursPrestes { get; set; }
    public int Nuitees { get; set; }
    public int Samedis { get; set; }
    public double HeuresDimanche { get; set; }
}

public sealed record SheetAmounts(double ChequeRepas, double Nuitees, double HeuresNuit, double Samedis, double Standby);

// Calculs de la feuille de pointage (jours, heures travaillées, totaux,
// montants).
public static class FeuilleCalc
{
    // Colonne TIME (Heure_*, Debut_pause...) : renvoyée comme un datetime du
    // 01/01/1970 en UTC — seules les heures et minutes comptent.
    public static double TimeMinutes(string? value)
    {
        if (string.IsNullOrEmpty(value)) return 0;
        var parts = (value.Length > 11 ? value[11..Math.Min(16, value.Length)] : "").Split(':');
        var h = JsNumber.Parse(parts[0]);
        var m = parts.Length > 1 ? JsNumber.Parse(parts[1]) : 0;
        return (double.IsNaN(h) ? 0 : h) * 60 + (double.IsNaN(m) ? 0 : m);
    }

    // Heure locale d'un horodatage (Date_heure_debut / Date_heure_fin).
    public static string ClockTime(string? value) => Format.ParseLocal(value) is { } date ? Format.Time(date) : "";

    // Durée en minutes → « 07:36 » (une journée) ou « 45 h 36 » (un total).
    public static string FormatDuration(double minutes, bool total = false)
    {
        var sign = minutes < 0 ? "-" : "";
        var abs = Math.Abs(DateUtils.JsRound(minutes));
        var h = (long)Math.Floor(abs / 60);
        var m = ((long)abs % 60).ToString("00", CultureInfo.InvariantCulture);
        return total ? $"{sign}{h} h {m}" : $"{sign}{h.ToString("00", CultureInfo.InvariantCulture)}:{m}";
    }

    // H. total = fin − début.
    public static double TotalMinutes(PointageFeuille p)
    {
        if (string.IsNullOrEmpty(p.Date_heure_debut) || string.IsNullOrEmpty(p.Date_heure_fin)) return 0;
        var ms = Clock.Parse(p.Date_heure_fin) - Clock.Parse(p.Date_heure_debut);
        return ms > 0 ? ms / 60_000 : 0;
    }

    // H. travaillé : Heure_jour quand il est renseigné (journée au forfait,
    // ex. 07:36 sans heures de début et de fin), sinon total − coupure −
    // pause. Règle déduite de l'écran WinDev et de l'ancienne liste.
    public static double WorkedMinutes(PointageFeuille p)
    {
        var forfait = TimeMinutes(p.Heure_jour);
        if (forfait > 0) return forfait;
        var pause = Math.Max(TimeMinutes(p.Fin_Pause) - TimeMinutes(p.Debut_pause), 0);
        return Math.Max(TotalMinutes(p) - TimeMinutes(p.Heure_coupure) - pause, 0);
    }

    // Une entrée par jour de la période, avec ses pointages (souvent 0 ou 1).
    public static List<SheetDay> BuildDays(IEnumerable<DateTime> days, IEnumerable<PointageFeuille> pointages)
    {
        var byDay = new Dictionary<string, List<PointageFeuille>>();
        foreach (var p in pointages)
        {
            if (p.Date_application is not { Length: >= 10 } d) continue;
            var key = d[..10];
            if (!byDay.TryGetValue(key, out var list)) byDay[key] = list = [];
            list.Add(p);
        }
        return [.. days.Select(date =>
        {
            var key = DateUtils.ToInputValue(date);
            return new SheetDay(date, key, byDay.GetValueOrDefault(key) ?? []);
        })];
    }

    // Un jour compte comme presté s'il a des heures travaillées ou un statut
    // « de travail ».
    private static bool IsWorked(PointageFeuille p) => WorkedMinutes(p) > 0 || p.TypeStatut?.Statut_de_travail == 1;

    public static SheetTotals ComputeTotals(IEnumerable<SheetDay> days)
    {
        var totals = new SheetTotals();
        foreach (var day in days)
        {
            foreach (var p in day.Pointages)
            {
                totals.Total += TotalMinutes(p);
                totals.Worked += WorkedMinutes(p);
                totals.Coupure += TimeMinutes(p.Heure_coupure);
                totals.Nuit += TimeMinutes(p.Heure_nuit);
                totals.Liaison += TimeMinutes(p.Heure_liaison);
                totals.Standby += TimeMinutes(p.Heure_Stanby);
                if (p.Nuitee == 1) totals.Nuitees++;
                if (day.Date.DayOfWeek == DayOfWeek.Sunday) totals.HeuresDimanche += WorkedMinutes(p);
            }
            if (day.Pointages.Any(IsWorked))
            {
                totals.JoursPrestes++;
                if (day.Date.DayOfWeek == DayOfWeek.Saturday) totals.Samedis++;
            }
        }
        return totals;
    }

    // Number(valeur) || 0.
    public static double Rate(string? value)
    {
        var n = JsNumber.Parse(value ?? "0");
        return double.IsNaN(n) ? 0 : n;
    }

    // Montants de la période à partir des taux du contrat. Chèques repas =
    // jours prestés × taux (vérifié sur l'écran WinDev : 6 × 8 € = 48 €) ; les
    // autres suivent la même logique quantité × taux. Heures supplémentaires
    // et balance ne sont pas calculées : leur règle (heures dues) n'est pas
    // connue.
    public static SheetAmounts? ComputeAmounts(SheetTotals totals, FeuilleTaux? taux)
    {
        if (taux is null) return null;
        return new SheetAmounts(
            totals.JoursPrestes * Rate(taux.Taux_cheque_repas),
            totals.Nuitees * Rate(taux.Taux_nuitee),
            totals.Nuit / 60 * Rate(taux.Taux_horaire_nuitee),
            totals.Samedis * Rate(taux.Taux_samedi),
            totals.Standby / 60 * Rate(taux.Taux_stanby));
    }
}
