using System.Globalization;
using System.Text.RegularExpressions;

namespace TBE.BookingService.Application.Ttl.Adapters;

/// <summary>
/// Helper used by the per-GDS adapters to rebuild a UTC <see cref="DateTime"/> from a
/// <see cref="Match"/> with named groups <c>day</c>, <c>month</c> (3-letter alpha),
/// optional <c>year</c>, optional <c>hour</c>, optional <c>minute</c>.
/// </summary>
internal static class FareRuleDateBuilder
{
    public static bool TryBuildUtc(
        Match m,
        out DateTime utc,
        int defaultHour = 23,
        int defaultMinute = 59)
    {
        utc = default;
        if (!m.Success) return false;

        if (!int.TryParse(m.Groups["day"].Value, out var day)) return false;

        var monthStr = m.Groups["month"].Value?.Trim();
        if (string.IsNullOrEmpty(monthStr)) return false;
        var month = ParseMonth(monthStr);
        if (month == 0) return false;

        int year;
        var yearGroup = m.Groups["year"];
        if (yearGroup.Success && !string.IsNullOrWhiteSpace(yearGroup.Value))
        {
            if (!int.TryParse(yearGroup.Value, out year)) return false;
            if (year < 100) year += 2000;
        }
        else
        {
            // Year omitted → infer the next occurrence of (month, day) from "now".
            var now = DateTime.UtcNow;
            year = now.Year;
            var probe = TryCreateUtc(year, month, day, defaultHour, defaultMinute);
            if (probe is null) return false;
            if (probe.Value <= now) year += 1;
        }

        int hour = defaultHour, minute = defaultMinute;
        if (m.Groups["hour"].Success && int.TryParse(m.Groups["hour"].Value, out var parsedHour)) hour = parsedHour;
        if (m.Groups["minute"].Success && int.TryParse(m.Groups["minute"].Value, out var parsedMinute)) minute = parsedMinute;

        var built = TryCreateUtc(year, month, day, hour, minute);
        if (built is null) return false;
        utc = built.Value;
        return true;
    }

    private static DateTime? TryCreateUtc(int year, int month, int day, int hour, int minute)
    {
        try
        {
            return new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Utc);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private static int ParseMonth(string monthStr)
    {
        var mon = monthStr.Length >= 3 ? monthStr[..3] : monthStr;
        var months = CultureInfo.InvariantCulture.DateTimeFormat.AbbreviatedMonthNames;
        for (var i = 0; i < months.Length; i++)
        {
            if (string.Equals(months[i], mon, StringComparison.OrdinalIgnoreCase))
            {
                return i + 1; // AbbreviatedMonthNames is zero-indexed with Jan=0
            }
        }
        return 0;
    }
}
