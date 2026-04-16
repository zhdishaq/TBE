namespace TBE.SearchService.Application.Airports;

/// <summary>
/// Airport typeahead lookup backed by Redis (CONTEXT D-18).
///
/// Implementations MUST NOT fetch from any external API per keystroke —
/// the lookup is served from the in-process connection's Redis cache that
/// the <c>IataAirportSeeder</c> populates from <c>data/iata/airports.dat</c>
/// at service startup.
/// </summary>
public interface IAirportLookup
{
    /// <summary>
    /// Return up to <paramref name="limit"/> airports whose lower-cased IATA,
    /// city or name begin with <paramref name="prefix"/> (also lower-cased).
    /// </summary>
    /// <param name="prefix">Caller-provided query (min 2 chars enforced at the controller).</param>
    /// <param name="limit">Max results; the controller caps at 20.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<IReadOnlyList<AirportDto>> SearchAsync(string prefix, int limit, CancellationToken ct);
}

/// <summary>
/// Public airport DTO returned from the typeahead endpoint. Contains only
/// fields the UI needs (no coordinates, no tz data) — keeps the payload
/// small enough to cache at the CDN edge.
/// </summary>
public record AirportDto(string Iata, string Name, string City, string Country);
