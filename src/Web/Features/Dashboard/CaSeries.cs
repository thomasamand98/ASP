using Horizon.Shared.Models;
using Horizon.Web.Common;

namespace Horizon.Web.Features.Dashboard;

// ordre = ordre des couleurs --series-N (validé daltonisme), ne pas réordonner
public sealed record CaSeries(string Key, string Label, string Color, Func<ChiffreAffairesPoint, double> Value)
{
    public static readonly IReadOnlyList<CaSeries> All =
    [
        new("facture", "Facturé", "var(--series-1)", p => p.facture),
        new("factureN1", "Facturé N-1", "var(--series-2)", p => p.factureN1),
        new("production", "Production", "var(--series-3)", p => p.production),
        new("productionN1", "Production N-1", "var(--series-4)", p => p.productionN1),
    ];

    public static readonly string[] MonthsShort = ["janv.", "févr.", "mars", "avr.", "mai", "juin", "juil.", "août", "sept.", "oct.", "nov.", "déc."];

    public static readonly string[] MonthsLong = ["janvier", "février", "mars", "avril", "mai", "juin", "juillet", "août", "septembre", "octobre", "novembre", "décembre"];

    // "05/03" ou "Mars"
    public static string LabelShort(string cle, string granularite)
    {
        if (granularite == "jour") return $"{cle[8..10]}/{cle[5..7]}";
        return Format.Capitalize(MonthsShort[int.Parse(cle[5..7]) - 1]);
    }

    // "05/03/2026" ou "Mars 2026"
    public static string LabelLong(string cle, string granularite)
    {
        if (granularite == "jour") return $"{cle[8..10]}/{cle[5..7]}/{cle[0..4]}";
        return Format.Capitalize($"{MonthsLong[int.Parse(cle[5..7]) - 1]} {cle[0..4]}");
    }
}
