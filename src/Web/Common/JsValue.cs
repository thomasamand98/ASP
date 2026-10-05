using Horizon.Shared;

namespace Horizon.Web.Common;

// Valeurs de cellule traitées comme en JavaScript (types string / number /
// boolean / null), pour que le tri, le filtre, l'affichage et l'export du
// tableau donnent exactement les mêmes résultats que le front d'origine.
public static class JsValue
{
    public static bool IsNumber(object? value) => value is double or float or int or long or decimal or short or byte;

    public static double ToDouble(object? value) => value switch
    {
        double d => d,
        float f => f,
        int i => i,
        long l => l,
        decimal m => (double)m,
        short s => s,
        byte b => b,
        _ => double.NaN,
    };

    // String(valeur).
    public static string ToJsString(object? value) => value switch
    {
        null => "null",
        string s => s,
        bool b => b ? "true" : "false",
        _ when IsNumber(value) => JsNumber.ToString(ToDouble(value)),
        _ => "[object Object]",
    };

    // Rendu React d'une valeur brute : texte et nombres affichés, rien pour
    // null, undefined et les booléens.
    public static string Display(object? value) => value switch
    {
        null or bool => "",
        string s => s,
        _ when IsNumber(value) => JsNumber.ToString(ToDouble(value)),
        _ => ToJsString(value),
    };

    // Conversion utilisée par les tris « text » de TanStack Table : nombres
    // finis en texte, tout le reste (null, objets...) en chaîne vide.
    public static string SortText(object? value) => value switch
    {
        string s => s,
        _ when IsNumber(value) => double.IsFinite(ToDouble(value)) ? JsNumber.ToString(ToDouble(value)) : "",
        _ => "",
    };

    // Number(valeur) pour les comparaisons relationnelles (< >).
    private static double ToNumber(object? value) => value switch
    {
        null => 0,
        bool b => b ? 1 : 0,
        string s => JsNumber.Parse(s),
        _ when IsNumber(value) => ToDouble(value),
        _ => double.NaN,
    };

    private static bool StrictEquals(object? a, object? b) => (a, b) switch
    {
        (null, null) => true,
        (string x, string y) => string.Equals(x, y, StringComparison.Ordinal),
        (bool x, bool y) => x == y,
        _ when IsNumber(a) && IsNumber(b) => ToDouble(a) == ToDouble(b),
        _ => false,
    };

    private static bool Greater(object? a, object? b) =>
        a is string x && b is string y ? string.CompareOrdinal(x, y) > 0 : ToNumber(a) > ToNumber(b);

    // compareBasic : a === b ? 0 : a > b ? 1 : -1.
    public static int CompareBasic(object? a, object? b) => StrictEquals(a, b) ? 0 : Greater(a, b) ? 1 : -1;
}
