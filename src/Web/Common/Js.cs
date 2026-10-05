namespace Horizon.Web.Common;

// Fonctions de JavaScript reproduites à l'identique.
public static class Js
{
    // encodeURIComponent : comme Uri.EscapeDataString, mais laisse
    // ! ' ( ) * tels quels.
    public static string EncodeUriComponent(string value) =>
        Uri.EscapeDataString(value).Replace("%21", "!").Replace("%27", "'").Replace("%28", "(").Replace("%29", ")").Replace("%2A", "*");
}
