namespace RotationsPlus.Api.Modules.Dashboard;

/// <summary>
/// The platform's business calendar (US/Pacific, matching the legacy dashboard). Centralizes the
/// timezone resolution + month-boundary math the dashboard aggregations share, so "today"/"this month"
/// windows are computed one way everywhere. Resolved cross-platform (IANA on Linux, the Windows id as a
/// fallback), then UTC as a last resort so a minimal container without the zone never fails a request.
/// </summary>
internal static class BusinessCalendar
{
    public static readonly TimeZoneInfo Zone = Resolve();

    /// <summary>Adds <paramref name="delta"/> months to a (year, month) pair, handling year rollover.
    /// Month is 1-12 on input and output.</summary>
    public static (int Year, int Month) AddMonths(int year, int month, int delta)
    {
        var zeroBased = (year * 12 + (month - 1)) + delta;
        return (zeroBased / 12, zeroBased % 12 + 1);
    }

    /// <summary>The UTC instant of midnight on the first of the given business month. Local midnight on
    /// the 1st is always a valid, unambiguous instant (no DST gap there), so the conversion is safe.</summary>
    public static DateTimeOffset StartOfMonthUtc(int year, int month)
    {
        var localMidnight = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Unspecified);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localMidnight, Zone));
    }

    private static TimeZoneInfo Resolve()
    {
        foreach (var id in new[] { "America/Los_Angeles", "Pacific Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
            {
                // Try the next id form, then fall through to UTC.
            }
        }

        return TimeZoneInfo.Utc;
    }
}
