using System.Globalization;
using System.Text.RegularExpressions;

namespace Horizon.Shared;

// couleur WinDev = BGR (R + G*256 + B*65536), 0 = pas de couleur
public static partial class WindevColor
{
    public static string? ToHex(long? value)
    {
        if (value is not { } n || n <= 0 || n > 0xffffff) return null;
        return $"#{n & 0xff:x2}{(n >> 8) & 0xff:x2}{(n >> 16) & 0xff:x2}";
    }

    // nombre ou texte (BigInt), null si vide, 0 ou invalide
    public static string? ToHex(object? value)
    {
        double n;
        switch (value)
        {
            case null:
            case string { Length: 0 }:
                return null;
            case string text:
                n = JsNumber.Parse(text);
                break;
            case IConvertible number when value is not bool:
                n = number.ToDouble(CultureInfo.InvariantCulture);
                break;
            default:
                return null;
        }
        return JsNumber.IsInteger(n) ? ToHex((long)n) : null;
    }

    // depuis un <input type="color">, 0 si invalide
    public static long FromHex(string hex)
    {
        var match = HexPattern().Match(hex);
        if (!match.Success) return 0;
        int Channel(int group) => int.Parse(match.Groups[group].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return Channel(1) + Channel(2) * 256L + Channel(3) * 65536L;
    }

    [GeneratedRegex("^#?([0-9a-f]{2})([0-9a-f]{2})([0-9a-f]{2})$", RegexOptions.IgnoreCase)]
    private static partial Regex HexPattern();
}
