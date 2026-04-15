using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace TBE.BookingService.Application.Ttl.Adapters;

/// <summary>
/// Amadeus fare-rule adapter. Prefers the structured <c>lastTicketingDate</c> JSON field
/// (ISO 8601, combined with 23:59 UTC cut-off); falls back to a free-text regex
/// <c>TICKET BY DD MMM [YY] HH:mm</c> on parse failure. Returns <c>false</c> if the extracted
/// deadline has already passed (Pitfall 5 — let caller apply D-07 fallback).
/// </summary>
public sealed class AmadeusFareRuleAdapter : IFareRuleAdapter
{
    public string Gds => "amadeus";

    private static readonly Regex TicketByRegex = new(
        @"TICKET\s+BY\s+(?<day>\d{1,2})\s*(?<month>[A-Z]{3,})\s*(?<year>\d{2,4})?\s+(?<hour>\d{2}):(?<minute>\d{2})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public bool TryParse(string raw, out DateTime deadlineUtc)
    {
        deadlineUtc = default;
        if (string.IsNullOrWhiteSpace(raw)) return false;

        // Attempt 1: structured JSON "lastTicketingDate".
        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("lastTicketingDate", out var ltd) &&
                ltd.ValueKind == JsonValueKind.String &&
                DateTime.TryParse(
                    ltd.GetString(),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var dt))
            {
                // Use end-of-day UTC (23:59) as the conservative deadline.
                var candidate = DateTime.SpecifyKind(dt.Date, DateTimeKind.Utc).AddHours(23).AddMinutes(59);
                if (candidate > DateTime.UtcNow)
                {
                    deadlineUtc = candidate;
                    return true;
                }
            }
        }
        catch (JsonException)
        {
            // Not JSON — fall through to regex.
        }

        // Attempt 2: free-text "TICKET BY …" regex.
        var m = TicketByRegex.Match(raw);
        if (m.Success && FareRuleDateBuilder.TryBuildUtc(m, out var fromRegex) && fromRegex > DateTime.UtcNow)
        {
            deadlineUtc = fromRegex;
            return true;
        }

        return false;
    }
}
