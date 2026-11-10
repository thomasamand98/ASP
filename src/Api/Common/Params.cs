using System.Globalization;
using System.Text.RegularExpressions;

namespace Api.Common;

// 400 si mal formé
public static partial class Params
{
    // pageSize=0 permis : option "Tous"
    public const int MaxPageSize = 500;

    public static long ParseId(string? raw)
    {
        if (raw is null || !Digits().IsMatch(raw) || !long.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var id))
        {
            throw new BadRequestException("Identifiant invalide.");
        }
        return id;
    }

    public static long? ParseOptionalId(string? raw) => string.IsNullOrEmpty(raw) ? null : ParseId(raw);

    // absent = pas de pagination
    public static int? ParsePage(string? raw)
    {
        if (string.IsNullOrEmpty(raw)) return null;
        var page = JsNumber.Parse(raw);
        if (!JsNumber.IsInteger(page) || page < 1) throw new BadRequestException("Numéro de page invalide.");
        return (int)Math.Min(page, int.MaxValue);
    }

    public static int? ParsePageSize(string? raw)
    {
        if (string.IsNullOrEmpty(raw)) return null;
        var pageSize = JsNumber.Parse(raw);
        if (!JsNumber.IsInteger(pageSize) || pageSize < 0) throw new BadRequestException("Taille de page invalide.");
        return (int)Math.Min(pageSize, MaxPageSize);
    }

    // "0" inclut les NULL, absent = pas de filtre
    public static bool? ParseArchive(string? raw)
    {
        if (string.IsNullOrEmpty(raw)) return null;
        if (raw == "1") return true;
        if (raw == "0") return false;
        throw new BadRequestException("Filtre « archive » invalide (0 ou 1 attendu).");
    }

    // minuit UTC
    public static DateTime ParseDay(string? raw, string field)
    {
        if (raw is not null && Day().IsMatch(raw) && JsDate.TryParse($"{raw}T00:00:00Z", out var date)) return date;
        throw new BadRequestException($"Date invalide pour « {field} ».");
    }

    // bornes incluses, une seule possible
    public static Period? ParsePeriod(string? fromRaw, string? toRaw)
    {
        if (string.IsNullOrEmpty(fromRaw) && string.IsNullOrEmpty(toRaw)) return null;
        DateTime? from = string.IsNullOrEmpty(fromRaw) ? null : ParseDay(fromRaw, "from");
        DateTime? to = string.IsNullOrEmpty(toRaw) ? null : ParseDay(toRaw, "to");
        if (from is not null && to is not null && to < from) throw new BadRequestException("La fin de la période doit être après son début.");
        return new Period(from, to);
    }

    [GeneratedRegex(@"^\d+$")]
    private static partial Regex Digits();

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}$")]
    private static partial Regex Day();
}

public record Period(DateTime? Gte, DateTime? Lte);
