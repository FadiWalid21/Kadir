namespace Kadr.Platform.Domain.Attendance;

/// <summary>Resolves IANA / Windows time zone ids without extra packages.</summary>
public static class TenantTimeZones
{
    private static readonly Dictionary<string, string> WindowsAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Africa/Cairo"] = "Egypt Standard Time",
        ["Asia/Riyadh"] = "Arab Standard Time",
        ["UTC"] = "UTC",
    };

    public static TimeZoneInfo Resolve(string? timeZoneId)
    {
        var id = string.IsNullOrWhiteSpace(timeZoneId) ? "Africa/Cairo" : timeZoneId.Trim();
        if (TimeZoneInfo.TryFindSystemTimeZoneById(id, out var tz))
        {
            return tz;
        }

        if (WindowsAliases.TryGetValue(id, out var windowsId)
            && TimeZoneInfo.TryFindSystemTimeZoneById(windowsId, out tz))
        {
            return tz;
        }

        // Reverse: Windows id provided, prefer IANA when available.
        foreach (var (iana, windows) in WindowsAliases)
        {
            if (string.Equals(windows, id, StringComparison.OrdinalIgnoreCase)
                && TimeZoneInfo.TryFindSystemTimeZoneById(iana, out tz))
            {
                return tz;
            }
        }

        throw new TimeZoneNotFoundException($"Unknown time zone '{id}'.");
    }

    public static DateOnly ToWorkDate(DateTimeOffset utc, string? timeZoneId)
    {
        var tz = Resolve(timeZoneId);
        var local = TimeZoneInfo.ConvertTime(utc, tz);
        return DateOnly.FromDateTime(local.DateTime);
    }

    public static DateTimeOffset ToUtc(DateOnly date, TimeOnly time, string? timeZoneId)
    {
        var tz = Resolve(timeZoneId);
        var unspecified = date.ToDateTime(time, DateTimeKind.Unspecified);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(unspecified, tz));
    }
}
