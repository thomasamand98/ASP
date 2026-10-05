using Horizon.Shared.Models;
using Horizon.Web.Common;

namespace Horizon.Web.Features.Personnels;

// Règles d'affichage des contrats de travail (onglet Contrat de la fiche
// personnel).
public static class ContratTravailFormat
{
    // Tri des dates « AAAA-MM-JJ… » : l'ordre des caractères suffit.
    public static readonly StringComparer LocaleComparer = StringComparer.Ordinal;

    // Libellés et unités des taux (écran WinDev : Taux brut, T.H. nuit...).
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

    // new Date().toISOString().slice(0, 10) : date du jour en UTC.
    public static string Today() => DateTime.UtcNow.ToString("yyyy'-'MM'-'dd", System.Globalization.CultureInfo.InvariantCulture);

    private static string Day(string value) => value[..Math.Min(10, value.Length)];

    // Les dates en 2099 ou après sont une convention WinDev pour « sans limite ».
    public static bool IsOpenEnded(string? day) => string.IsNullOrEmpty(day) || Horizon.Shared.JsNumber.Parse(day[..Math.Min(4, day.Length)]) >= 2099;

    // (tonalité, libellé) : « future », « past » ou « current ».
    public static (string Tone, string Label) StatusOf(string? debut, string? fin)
    {
        var now = Today();
        if (!string.IsNullOrEmpty(debut) && string.CompareOrdinal(Day(debut), now) > 0) return ("future", "À venir");
        if (!IsOpenEnded(fin) && string.CompareOrdinal(Day(fin!), now) < 0) return ("past", "Terminé");
        return ("current", "En cours");
    }

    // Taux applicable : le plus récent dont la date d'application est passée
    // à la date de référence (aujourd'hui, ou la fin d'un contrat terminé).
    // Les taux arrivent triés du plus récent au plus ancien.
    public static TauxHoraire? ApplicableRate(ContratTravail c)
    {
        var reference = IsOpenEnded(c.Date_fin) ? Today() : new[] { Today(), Day(c.Date_fin!) }.Order(StringComparer.Ordinal).First();
        return c.TauxHoraireContratTravails.FirstOrDefault(t => string.IsNullOrEmpty(t.Date_application) || string.CompareOrdinal(Day(t.Date_application), reference) <= 0);
    }

    // Les Decimal arrivent en « 13.400000 » : affichés sans zéros superflus.
    public static string RateInput(string? value) => value is null ? "" : Horizon.Shared.JsNumber.ToString(Horizon.Shared.JsNumber.Parse(value));

    // Mise en forme réutilisée par l'éditeur.
    public static string DateLabel(string? value) => ContratFormat.Date(value);
}
