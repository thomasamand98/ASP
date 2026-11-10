namespace Horizon.Web.Common;

// AAAA-MM-JJ bornes incluses, null = toutes les dates
public sealed record Period(string From, string To)
{
    public static Period Today()
    {
        var today = DateUtils.ToInputValue(DateTime.Now);
        return new Period(today, today);
    }

    public static Dictionary<string, string> Filters(Period? period) =>
        period is null ? [] : new() { ["from"] = period.From, ["to"] = period.To };
}
