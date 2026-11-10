using Horizon.Shared;

namespace Horizon.Web.Common;

public static class ContratFormat
{
    // "1680.24/07", version "0" = jamais versionné
    public static string Numero(string? numContrat, string? version) =>
        !string.IsNullOrEmpty(version) && version != "0" ? $"{numContrat ?? ""}.{version}" : numContrat ?? "";

    // pas de conversion locale : minuit UTC reculerait d'un jour en fuseau négatif
    public static string Date(string? value) =>
        string.IsNullOrEmpty(value) ? "—" : $"{Slice(value, 8, 10)}/{Slice(value, 5, 7)}/{Slice(value, 0, 4)}";

    private static string Slice(string value, int start, int end) => start >= value.Length ? "" : value[start..Math.Min(end, value.Length)];

    // "1234.500000" donne "1 234,50 €" (2 à 4 décimales)
    public static string Euro(string? value)
    {
        if (value is null || value.Length == 0) return "—";
        var amount = JsNumber.Parse(value);
        return double.IsFinite(amount) ? Format.Money(amount, 2, 4) : "—";
    }
}
