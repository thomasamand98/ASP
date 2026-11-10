using Horizon.Shared.Models;
using Horizon.Web.Common;

namespace Horizon.Web.Features.Personnels;

public static class ContratTravailFormat
{
    // format AAAA-MM-JJ, l'ordre des caractères suffit
    public static readonly StringComparer LocaleComparer = StringComparer.Ordinal;

    // libellés de l'écran WinDev (Taux brut, T.H. nuit...)
    public sealed record Rate(string Field, string Label, string Unit, Func<TauxHoraire, string?> Get);

    public static readonly IReadOnlyList<Rate> Rates =
    [
        new("Taux_horaire", "Taux brut", "/ h", t => t.Taux_horaire),
        new("Taux_horaire_supplementaire", "Heure sup.", "/ h", t => t.Taux_horaire_supplementaire),
        new("Taux_horaire_nuitee", "Heure de nuit", "/ h", t => t.Taux_horaire_nuitee),
        new("Taux_nuitee", "Nuitée", "/ nuit", t => t.Taux_nuitee),
        new("Taux_samedi", "Forfait samedi", "/ jour", t => t.Taux_samedi),
        new("Taux_cheque_repas", "Chèque repas", "/ jour", t => t.Taux_cheque_repas),
        new("Taux_stanby", "Standby", "/ h", t => t.Taux_stanby),
    ];

    public sealed record Activite(string Label, Func<ContratTravail, double?> Get);

    public static readonly IReadOnlyList<Activite> Activites =
    [
        new("Routier", c => c.Routier),
        new("Manutention", c => c.Manutention),
        new("Atelier", c => c.Atelier),
    ];

    public static readonly string[] TypesContrat = ["CDI", "CDD", "Intérim", "Étudiant", "Flexi-job"];

    // date du jour en UTC
    public static string Today() => DateTime.UtcNow.ToString("yyyy'-'MM'-'dd", System.Globalization.CultureInfo.InvariantCulture);

    private static string Day(string value) => value[..Math.Min(10, value.Length)];

    // 2099 et plus = sans limite (convention WinDev)
    public static bool IsOpenEnded(string? day) => string.IsNullOrEmpty(day) || Horizon.Shared.JsNumber.Parse(day[..Math.Min(4, day.Length)]) >= 2099;

    public static (string Tone, string Label) StatusOf(string? debut, string? fin)
    {
        var now = Today();
        if (!string.IsNullOrEmpty(debut) && string.CompareOrdinal(Day(debut), now) > 0) return ("future", "À venir");
        if (!IsOpenEnded(fin) && string.CompareOrdinal(Day(fin!), now) < 0) return ("past", "Terminé");
        return ("current", "En cours");
    }

    // le plus récent déjà applicable (taux triés du plus récent au plus ancien)
    public static TauxHoraire? ApplicableRate(ContratTravail c)
    {
        var reference = IsOpenEnded(c.Date_fin) ? Today() : new[] { Today(), Day(c.Date_fin!) }.Order(StringComparer.Ordinal).First();
        return c.TauxHoraireContratTravails.FirstOrDefault(t => string.IsNullOrEmpty(t.Date_application) || string.CompareOrdinal(Day(t.Date_application), reference) <= 0);
    }

    // arrivent en 13.400000, on retire les zéros
    public static string RateInput(string? value) => value is null ? "" : Horizon.Shared.JsNumber.ToString(Horizon.Shared.JsNumber.Parse(value));

    public static string DateLabel(string? value) => ContratFormat.Date(value);
}
