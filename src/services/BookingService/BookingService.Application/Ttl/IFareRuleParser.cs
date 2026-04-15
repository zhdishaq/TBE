namespace TBE.BookingService.Application.Ttl;

/// <summary>
/// Parses the ticketing deadline out of a raw fare-rule payload emitted by a specific GDS adapter.
/// Returns <c>false</c> on any parse failure — the caller is responsible for applying the D-07
/// fallback (UtcNow + 2h) and publishing a <c>FareRuleParseFailedAlert</c>.
/// </summary>
public interface IFareRuleParser
{
    /// <summary>
    /// Attempts to extract the ticketing deadline from a raw fare-rule payload produced by the
    /// named GDS adapter.
    /// </summary>
    /// <param name="gdsCode">One of <c>"amadeus" | "sabre" | "galileo"</c> (case-insensitive).</param>
    /// <param name="rawPayload">Raw JSON / XML / flat-text payload produced by the GDS.</param>
    /// <param name="deadlineUtc">Extracted UTC deadline on success; <c>default(DateTime)</c> on failure.</param>
    /// <returns><c>true</c> on successful parse; <c>false</c> if the adapter is unknown, the payload
    /// is malformed, or the extracted deadline is already in the past (Pitfall 5 guard).</returns>
    bool TryParse(string gdsCode, string rawPayload, out DateTime deadlineUtc);
}

/// <summary>
/// Per-GDS fare-rule adapter. Implementations are registered via keyed DI under their
/// <see cref="Gds"/> discriminator and resolved by <see cref="FareRuleParser"/>.
/// </summary>
public interface IFareRuleAdapter
{
    /// <summary>GDS code this adapter handles. Compared case-insensitively.</summary>
    string Gds { get; }

    /// <summary>See <see cref="IFareRuleParser.TryParse"/>.</summary>
    bool TryParse(string rawPayload, out DateTime deadlineUtc);
}
