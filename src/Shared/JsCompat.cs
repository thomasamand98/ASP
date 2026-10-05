using System.Globalization;
using System.Text.RegularExpressions;

namespace Horizon.Shared;

// Reproduit Number(texte) de JavaScript, utilisé par le backend NestJS pour
// lire les paramètres numériques (pagination, quantités...).
public static partial class JsNumber
{
    public static double Parse(string? raw)
    {
        var text = (raw ?? "").Trim();
        if (text.Length == 0) return 0;
        if (text is "Infinity" or "+Infinity") return double.PositiveInfinity;
        if (text == "-Infinity") return double.NegativeInfinity;
        if (Prefixed().Match(text) is { Success: true } prefixed)
        {
            var radix = char.ToLowerInvariant(text[1]) switch { 'x' => 16, 'o' => 8, _ => 2 };
            try
            {
                return (double)Convert.ToUInt64(prefixed.Groups[1].Value, radix);
            }
            catch (Exception e) when (e is FormatException or OverflowException)
            {
                return double.NaN;
            }
        }
        return DecimalLiteral().IsMatch(text) ? double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture) : double.NaN;
    }

    public static bool IsInteger(double value) => double.IsFinite(value) && Math.Floor(value) == value;

    // String(nombre) de JavaScript pour les valeurs courantes.
    public static string ToString(double value)
    {
        if (IsInteger(value) && Math.Abs(value) < 1e21) return value.ToString("0", CultureInfo.InvariantCulture);
        return value.ToString("R", CultureInfo.InvariantCulture).Replace("E+", "e+").Replace("E-", "e-");
    }

    [GeneratedRegex(@"^0[xXoObB]([0-9a-fA-F]+)$")]
    private static partial Regex Prefixed();

    [GeneratedRegex(@"^[+-]?(\d+\.?\d*|\.\d+)([eE][+-]?\d+)?$")]
    private static partial Regex DecimalLiteral();
}

// Reproduit Date.parse de JavaScript pour les formats ISO 8601 : une date
// seule est en UTC, une date-heure sans fuseau est en heure locale du
// serveur. Les autres formats (hérités, propres à V8) sont lus au mieux par
// DateTime.Parse.
public static partial class JsDate
{
    public static bool TryParse(string? raw, out DateTime utc)
    {
        utc = default;
        if (raw is null) return false;
        var m = Iso().Match(raw);
        if (!m.Success)
        {
            if (!DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal | DateTimeStyles.AdjustToUniversal | DateTimeStyles.AllowWhiteSpaces, out var legacy)) return false;
            utc = DateTime.SpecifyKind(legacy, DateTimeKind.Utc);
            return true;
        }

        int Group(string name, int fallback) => m.Groups[name].Success ? int.Parse(m.Groups[name].Value, CultureInfo.InvariantCulture) : fallback;
        var (year, month, day) = (Group("y", 0), Group("mo", 1), Group("d", 1));
        var (hour, minute, second) = (Group("h", 0), Group("mi", 0), Group("s", 0));
        var millisecond = m.Groups["f"].Success ? int.Parse(m.Groups["f"].Value.PadRight(3, '0')[..3], CultureInfo.InvariantCulture) : 0;
        if (year < 1 || month is < 1 or > 12 || day is < 1 or > 31 || minute > 59 || second > 59) return false;
        if (hour > 24 || (hour == 24 && (minute | second | millisecond) != 0)) return false;

        var value = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Unspecified)
            .AddDays(day - 1).AddHours(hour).AddMinutes(minute).AddSeconds(second).AddMilliseconds(millisecond);

        if (m.Groups["z"].Success)
        {
            // Z ou décalage explicite.
        }
        else if (m.Groups["sign"].Success)
        {
            var offset = new TimeSpan(Group("oh", 0), Group("om", 0), 0);
            value = m.Groups["sign"].Value == "-" ? value + offset : value - offset;
        }
        else if (m.Groups["h"].Success)
        {
            value = TimeZoneInfo.ConvertTimeToUtc(value, TimeZoneInfo.Local);
        }
        utc = DateTime.SpecifyKind(value, DateTimeKind.Utc);
        return true;
    }

    [GeneratedRegex(@"^(?<y>\d{4})(-(?<mo>\d{2})(-(?<d>\d{2}))?)?(T(?<h>\d{2}):(?<mi>\d{2})(:(?<s>\d{2})(\.(?<f>\d+))?)?((?<z>Z)|(?<sign>[+-])(?<oh>\d{2}):(?<om>\d{2}))?)?$")]
    private static partial Regex Iso();
}
