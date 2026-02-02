namespace JdeClient.Core.Internal;

/// <summary>
/// Converts JDE Julian date integers into <see cref="DateTime"/> values.
/// </summary>
internal static class JdeJulianDateConverter
{
    /// <summary>
    /// Convert a JDE Julian date to an ISO-8601 date string (yyyy-MM-dd).
    /// </summary>
    public static string ToDateString(int jdeJulian)
    {
        DateTime? date = ToDate(jdeJulian);
        return date.HasValue ? date.Value.ToString("yyyy-MM-dd") : string.Empty;
    }

    /// <summary>
    /// Convert a JDE Julian date to a <see cref="DateTime"/>, or null when invalid.
    /// </summary>
    public static DateTime? ToDate(int jdeJulian)
    {
        if (jdeJulian <= 0)
        {
            return null;
        }

        int c = jdeJulian / 100000;
        int yy = (jdeJulian / 1000) % 100;
        int ddd = jdeJulian % 1000;

        int year = 1900 + (c * 100) + yy;
        if (ddd <= 0)
        {
            return null;
        }

        try
        {
            return new DateTime(year, 1, 1).AddDays(ddd - 1);
        }
        catch
        {
            return null;
        }
    }
}
