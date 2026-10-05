namespace Horizon.Web.Common;

// Période d'une liste, en jours « AAAA-MM-JJ » (bornes incluses) — envoyée
// telle quelle en ?from=&to= (voir Params.ParsePeriod côté API). null =
// toutes les dates. Sélecteur : Components/PeriodFilter.razor.
public sealed record Period(string From, string To)
{
    // Période d'un seul jour : aujourd'hui.
    public static Period Today()
    {
        var today = DateUtils.ToInputValue(DateTime.Now);
        return new Period(today, today);
    }

    // Filtres ?from=&to= à passer à la liste (voir ApiList).
    public static Dictionary<string, string> Filters(Period? period) =>
        period is null ? [] : new() { ["from"] = period.From, ["to"] = period.To };
}
