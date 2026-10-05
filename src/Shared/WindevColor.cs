using System.Globalization;
using System.Text.RegularExpressions;

namespace Horizon.Shared;

// Couleurs WinDev (Marchandise.CouleurPlanning, TypeStatut.CouleurStatut...) :
// un entier renvoyé par la fonction RGB() de WinDev, R + G×256 + B×65536
// (ordre BGR d'un COLORREF Windows), entre 0 et 16 777 215. 0 (noir) est la
// valeur par défaut des colonnes, traitée comme « pas de couleur ».
// Utilisé par l'API (planning) et par le front (pastilles, sélecteur).
public static partial class WindevColor
{
    public static string? ToHex(long? value)
    {
        if (value is not { } n || n <= 0 || n > 0xffffff) return null;
        return $"#{n & 0xff:x2}{(n >> 8) & 0xff:x2}{(n >> 16) & 0xff:x2}";
    }

    // Entier WinDev reçu en nombre, ou en texte pour les colonnes BigInt
    // sérialisées → « #rrggbb » ; null si pas de couleur (vide, 0, hors plage,
    // non entier).
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

    // « #rrggbb » (valeur d'un <input type="color">) → entier WinDev ; 0 si
    // la valeur n'est pas une couleur.
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
