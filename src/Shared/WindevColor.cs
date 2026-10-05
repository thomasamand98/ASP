namespace Horizon.Shared;

// Couleurs WinDev (Marchandise.CouleurPlanning, TypeStatut.CouleurStatut...) :
// un entier renvoyé par la fonction RGB() de WinDev, R + G×256 + B×65536
// (ordre BGR d'un COLORREF Windows), entre 0 et 16 777 215. 0 (noir) est la
// valeur par défaut des colonnes, traitée comme « pas de couleur ».
// Pendant côté frontend : frontend/src/lib/windevColor.ts.
public static class WindevColor
{
    public static string? ToHex(long? value)
    {
        if (value is not { } n || n <= 0 || n > 0xffffff) return null;
        return $"#{n & 0xff:x2}{(n >> 8) & 0xff:x2}{(n >> 16) & 0xff:x2}";
    }
}
