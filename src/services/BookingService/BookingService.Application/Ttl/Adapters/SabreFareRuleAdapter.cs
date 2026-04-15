using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;

namespace TBE.BookingService.Application.Ttl.Adapters;

/// <summary>
/// Sabre fare-rule adapter. Prefers a structured <c>&lt;TimeLimit&gt;</c> XML element
/// (ISO 8601 timestamp); falls back to a free-text regex <c>TKT TL DD-MMM-YY</c>.
/// Returns <c>false</c> if the extracted deadline has already passed.
/// </summary>
public sealed class SabreFareRuleAdapter : IFareRuleAdapter
{
    public string Gds => "sabre";

    private static readonly Regex TktTlRegex = new(
        @"TKT\s+TL\s+(?<day>\d{1,2})[-\s]?(?<month>[A-Z]{3,})[-\s]?(?<year>\d{2,4})?(?:\s+(?<hour>\d{2}):(?<minute>\d{2}))?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public bool TryParse(string raw, out DateTime deadlineUtc)
    {
        deadlineUtc = default;
        if (string.IsNullOrWhiteSpace(raw)) return false;

        // Attempt 1: <TimeLimit>…</TimeLimit> XML element.
        if (raw.TrimStart().StartsWith('<'))
        {
            try
            {
                var doc = new XmlDocument();
                doc.LoadXml(raw);
                var node = doc.GetElementsByTagName("TimeLimit");
                if (node.Count > 0 &&
                    DateTime.TryParse(
                        node[0]!.InnerText,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                        out var dt))
                {
                    var candidate = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
                    if (candidate > DateTime.UtcNow)
                    {
                        deadlineUtc = candidate;
                        return true;
                    }
                }
            }
            catch (XmlException)
            {
                // Fall through to regex.
            }
        }

        // Attempt 2: free-text "TKT TL DD-MMM-YY …" regex.
        var m = TktTlRegex.Match(raw);
        if (m.Success && FareRuleDateBuilder.TryBuildUtc(m, out var fromRegex) && fromRegex > DateTime.UtcNow)
        {
            deadlineUtc = fromRegex;
            return true;
        }

        return false;
    }
}
