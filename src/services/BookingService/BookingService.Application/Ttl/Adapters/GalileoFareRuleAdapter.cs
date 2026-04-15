using System.Text.RegularExpressions;

namespace TBE.BookingService.Application.Ttl.Adapters;

/// <summary>
/// Galileo fare-rule adapter. Parses the Galileo-native <c>T.TAU/DDMMM[YY]</c> ticketing-limit
/// token (TAU = Ticket Assignment Until). Galileo does not emit a time component in this token,
/// so the deadline is fixed at 23:59 UTC on the indicated date.
/// </summary>
public sealed class GalileoFareRuleAdapter : IFareRuleAdapter
{
    public string Gds => "galileo";

    private static readonly Regex TauRegex = new(
        @"T\.TAU/(?<day>\d{1,2})(?<month>[A-Z]{3})(?<year>\d{2,4})?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public bool TryParse(string raw, out DateTime deadlineUtc)
    {
        deadlineUtc = default;
        if (string.IsNullOrWhiteSpace(raw)) return false;

        var m = TauRegex.Match(raw);
        if (!m.Success) return false;

        if (!FareRuleDateBuilder.TryBuildUtc(m, out var candidate, defaultHour: 23, defaultMinute: 59))
        {
            return false;
        }

        if (candidate <= DateTime.UtcNow) return false;
        deadlineUtc = candidate;
        return true;
    }
}
