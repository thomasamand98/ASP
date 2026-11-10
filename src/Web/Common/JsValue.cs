using Horizon.Shared;

namespace Horizon.Web.Common;

// sémantique JS pour le tri, le filtre et l'export du tableau
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

    public static string ToJsString(object? value) => value switch
    {
        null => "null",
        string s => s,
        bool b => b ? "true" : "false",
        _ when IsNumber(value) => JsNumber.ToString(ToDouble(value)),
        _ => "[object Object]",
    };

    // null et booléens n'affichent rien
    public static string Display(object? value) => value switch
    {
        null or bool => "",
        string s => s,
        _ when IsNumber(value) => JsNumber.ToString(ToDouble(value)),
        _ => ToJsString(value),
    };

    // tri texte : nombres finis en texte, le reste vide
    public static string SortText(object? value) => value switch
    {
        string s => s,
        _ when IsNumber(value) => double.IsFinite(ToDouble(value)) ? JsNumber.ToString(ToDouble(value)) : "",
        _ => "",
    };

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

    public static int CompareBasic(object? a, object? b) => StrictEquals(a, b) ? 0 : Greater(a, b) ? 1 : -1;
}
