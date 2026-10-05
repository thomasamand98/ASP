using Horizon.Shared;

namespace Horizon.Web.Common;

// Formats d'affichage des contrats (numéro, dates des onglets, montants).
public static class ContratFormat
{
    // Numéro affiché d'un contrat : numéro + version (« 1680.24/07 »), comme
    // dans l'ancienne application. « 0 » est la version par défaut de la base,
    // pour un contrat jamais versionné.
    public static string Numero(string? numContrat, string? version) =>
        !string.IsNullOrEmpty(version) && version != "0" ? $"{numContrat ?? ""}.{version}" : numContrat ?? "";

    // "2024-07-01T00:00:00.000Z" → "01/07/2024". Découpé à la main plutôt que
    // converti en date locale : les colonnes DATE sont à minuit UTC et seraient
    // décalées d'un jour dans un fuseau négatif.
    public static string Date(string? value) =>
        string.IsNullOrEmpty(value) ? "—" : $"{Slice(value, 8, 10)}/{Slice(value, 5, 7)}/{Slice(value, 0, 4)}";

    private static string Slice(string value, int start, int end) => start >= value.Length ? "" : value[start..Math.Min(end, value.Length)];

    // Decimal reçu en texte (« 1234.500000 ») → « 1 234,50 € » (2 à 4 décimales).
    public static string Euro(string? value)
    {
        if (value is null || value.Length == 0) return "—";
        var amount = JsNumber.Parse(value);
        return double.IsFinite(amount) ? Format.Money(amount, 2, 4) : "—";
    }
}
