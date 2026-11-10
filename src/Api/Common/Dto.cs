using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Common;

public static class Dto
{
    public static long Id(string value) => long.Parse(value, NumberStyles.None, CultureInfo.InvariantCulture);

    // tronqué, hors int32 = exception (500)
    public static int Int(double value)
    {
        var truncated = Math.Truncate(value);
        return truncated is >= int.MinValue and <= int.MaxValue
            ? (int)truncated
            : throw new InvalidOperationException($"Entier 32 bits attendu, reçu {value.ToString(CultureInfo.InvariantCulture)}.");
    }

    // absent = inchangé, "" = NULL (le 0 de WinDev casserait la FK)
    public static Opt<long?> OptionalId(Opt<string> value) =>
        value.IsSet ? new Opt<long?>(value.Value.Length > 0 ? Id(value.Value) : null) : default;

    public static long? IdOrNull(Opt<string> value) => value.IsSet && value.Value.Length > 0 ? Id(value.Value) : null;

    public static void ApplyInt(this Opt<double> value, Action<int> assign)
    {
        if (value.IsSet) assign(Int(value.Value));
    }

    // absent = inchangé, "" = NULL
    public static void ApplyDate(this Opt<string> value, Action<DateTime?> assign, bool dateOnly = false)
    {
        if (!value.IsSet) return;
        if (value.Value.Length == 0) assign(null);
        else assign(dateOnly ? Date(value.Value).Date : Date(value.Value));
    }

    // AAAA-MM-JJ seulement, minuit UTC
    public static DateTime? ToDay(string? raw, string field) =>
        string.IsNullOrEmpty(raw) ? null : Params.ParseDay(raw.Length > 10 ? raw[..10] : raw, field);

    // déjà validée par [Date]
    public static DateTime Date(string value) =>
        JsDate.TryParse(value, out var date) ? date : throw new InvalidOperationException($"Date invalide : {value}");
}

public static class QueryExtensions
{
    public static async Task<T> FirstOrThrowAsync<T>(this IQueryable<T> query, CancellationToken ct = default) =>
        await query.FirstOrDefaultAsync(ct) ?? throw new RecordNotFoundException();

    public static async Task DeleteOrThrowAsync<T>(this IQueryable<T> query, CancellationToken ct = default)
    {
        if (await query.ExecuteDeleteAsync(ct) == 0) throw new RecordNotFoundException();
    }
}

// POST de création en 201
[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    protected async Task<IActionResult> Created201(Task<object> result) => StatusCode(StatusCodes.Status201Created, await result);
}
