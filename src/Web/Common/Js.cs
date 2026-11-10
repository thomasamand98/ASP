namespace Horizon.Web.Common;

public static class Js
{
    // comme encodeURIComponent, ! ' ( ) * non échappés
    public static string EncodeUriComponent(string value) =>
        Uri.EscapeDataString(value).Replace("%21", "!").Replace("%27", "'").Replace("%28", "(").Replace("%29", ")").Replace("%2A", "*");
}
