using System.Globalization;

namespace Horizon.Web.Common;

// Instants en millisecondes depuis 1970 (comme Date.getTime() en
// JavaScript), convertis en heure locale du navigateur pour les calculs de
// jours. Utilisé par le planning, qui positionne ses cartes en millisecondes.
public static class Clock
{
    public static double Now => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    // Date locale → instant (new Date(y, m, d, h...).getTime()).
    public static double ToMs(DateTime local) =>
        new DateTimeOffset(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), TimeZoneInfo.Local.GetUtcOffset(local)).ToUnixTimeMilliseconds();

    // Instant → date locale (new Date(ms)).
    public static DateTime ToLocal(double ms) => DateTimeOffset.FromUnixTimeMilliseconds((long)ms).ToLocalTime().DateTime;

    // Date.parse(iso).
    public static double Parse(string iso) =>
        DateTimeOffset.Parse(iso, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal).ToUnixTimeMilliseconds();

    // new Date(ms).toISOString().
    public static string ToIso(double ms) =>
        DateTimeOffset.FromUnixTimeMilliseconds((long)ms).UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
}
