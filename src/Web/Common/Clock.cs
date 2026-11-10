using System.Globalization;

namespace Horizon.Web.Common;

// ms depuis 1970, utilisé par le planning
public static class Clock
{
    public static double Now => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    public static double ToMs(DateTime local) =>
        new DateTimeOffset(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), TimeZoneInfo.Local.GetUtcOffset(local)).ToUnixTimeMilliseconds();

    public static DateTime ToLocal(double ms) => DateTimeOffset.FromUnixTimeMilliseconds((long)ms).ToLocalTime().DateTime;

    public static double Parse(string iso) =>
        DateTimeOffset.Parse(iso, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal).ToUnixTimeMilliseconds();

    public static string ToIso(double ms) =>
        DateTimeOffset.FromUnixTimeMilliseconds((long)ms).UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
}
