using System.Globalization;
using System.Text;

namespace Horizon.Web.Common;

// fr-BE en dur, CultureInfo("fr-BE") varie selon la version d'ICU
public static class Format
{
    private const char Group = ' ';
    private const char Nbsp = ' ';

    private static readonly string[] MonthsLong =
        ["janvier", "février", "mars", "avril", "mai", "juin", "juillet", "août", "septembre", "octobre", "novembre", "décembre"];

    private static readonly string[] MonthsShort =
        ["janv.", "févr.", "mars", "avr.", "mai", "juin", "juil.", "août", "sept.", "oct.", "nov.", "déc."];

    private static readonly string[] WeekdaysLong = ["dimanche", "lundi", "mardi", "mercredi", "jeudi", "vendredi", "samedi"];

    private static readonly string[] WeekdaysShort = ["dim.", "lun.", "mar.", "mer.", "jeu.", "ven.", "sam."];

    // ISO UTC vers heure locale du navigateur
    public static DateTime? ParseLocal(string? iso)
    {
        if (string.IsNullOrEmpty(iso)) return null;
        return DateTimeOffset.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed.ToLocalTime().DateTime
            : null;
    }

    public static string Date(DateTime date) => date.ToString("dd'/'MM'/'yyyy", CultureInfo.InvariantCulture);

    public static string DateTimeFull(DateTime date) => date.ToString("dd'/'MM'/'yyyy HH':'mm':'ss", CultureInfo.InvariantCulture);

    // "5 janv. 2026, 08:07"
    public static string DateTimeMedium(DateTime date) =>
        $"{date.Day} {MonthsShort[date.Month - 1]} {date.Year}, {date:HH':'mm}";

    // vide si absente ou invalide
    public static string IsoDate(string? iso) => ParseLocal(iso) is { } date ? Date(date) : "";

    public static string Time(DateTime date) => date.ToString("HH':'mm", CultureInfo.InvariantCulture);

    public static string WeekdayLong(DateTime date) => WeekdaysLong[(int)date.DayOfWeek];

    public static string MonthLong(int month) => MonthsLong[month - 1];

    // "Lundi 5 janvier"
    public static string DayLong(DateTime date) => Capitalize($"{WeekdayLong(date)} {date.Day} {MonthLong(date.Month)}");

    // "Lun. 05/01"
    public static string DayShort(DateTime date) => Capitalize($"{WeekdaysShort[(int)date.DayOfWeek]} {date:dd'/'MM}");

    public static string DateShort(DateTime date) => Date(date);

    public static string MonthYear(DateTime date) => $"{MonthLong(date.Month)} {date.Year}";

    public static string Capitalize(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

    // arrondi away from zero comme le navigateur
    public static string Number(double value, int minFraction = 0, int maxFraction = 3, bool grouping = true)
    {
        if (double.IsNaN(value)) return "NaN";
        if (double.IsInfinity(value)) return value > 0 ? "∞" : "-∞";
        var rounded = Math.Round((decimal)value, maxFraction, MidpointRounding.AwayFromZero);
        var negative = rounded < 0;
        var text = Math.Abs(rounded).ToString("0." + new string('0', minFraction) + new string('#', maxFraction - minFraction), CultureInfo.InvariantCulture);
        var parts = text.Split('.');
        var integer = parts[0];
        var builder = new StringBuilder();
        if (negative) builder.Append('-');
        if (grouping && integer.Length > 3)
        {
            var first = integer.Length % 3;
            if (first > 0) builder.Append(integer, 0, first);
            for (var i = first; i < integer.Length; i += 3)
            {
                if (i > 0) builder.Append(Group);
                builder.Append(integer, i, 3);
            }
        }
        else
        {
            builder.Append(integer);
        }
        if (parts.Length > 1 && parts[1].Length > 0) builder.Append(',').Append(parts[1]);
        return builder.ToString();
    }

    public static string Money(double value, int minFraction = 2, int maxFraction = 2) =>
        Number(value, minFraction, maxFraction) + Nbsp + "€";

    // "12,5 k €", "2,5 M €", "1 Md €"
    public static string MoneyCompact(double value)
    {
        var abs = Math.Abs(value);
        var (divisor, suffix) = abs >= 1e12 ? (1e12, "Bn") : abs >= 1e9 ? (1e9, "Md") : abs >= 1e6 ? (1e6, "M") : abs >= 1e3 ? (1e3, "k") : (1d, "");
        return Number(value / divisor, 0, 1) + (suffix.Length > 0 ? Nbsp + suffix : "") + Nbsp + "€";
    }

    // "+5 %", "-19,7 %", "0 %" (pas de signe pour zéro)
    public static string Percent(double ratio)
    {
        var text = Number(ratio * 100, 0, 1);
        return (text is not "0" && ratio > 0 ? "+" : "") + (text == "-0" ? "0" : text) + Nbsp + "%";
    }

    public static string Number(long value) => Number((double)value);
}
