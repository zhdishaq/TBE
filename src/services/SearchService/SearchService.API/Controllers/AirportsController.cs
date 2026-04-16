using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TBE.SearchService.Application.Airports;

namespace TBE.SearchService.API.Controllers;

/// <summary>
/// Public IATA airport typeahead for the B2C portal's flight-search form
/// (CONTEXT D-18). Served entirely from Redis — no GDS calls, no per-keystroke
/// upstream HTTP. See <see cref="RedisAirportLookup"/> for the O(log n) prefix
/// scan implementation and <see cref="IataAirportSeeder"/> for the dataset
/// seed at service startup.
///
/// <para>
/// <b>Security (T-04-02-04, IATA endpoint abuse).</b>
/// The endpoint is intentionally anonymous — anonymous users must be able to
/// browse and search per CONTEXT — so abuse mitigations are layered:
/// </para>
/// <list type="bullet">
///   <item>Minimum 2 chars, maximum 8 chars (rejects single-char enumeration and
///         overlong query spam).</item>
///   <item>Limit capped at 20 results (prevents large-result scraping).</item>
///   <item>Per-IP fixed-window rate limit: 60 req/min (see "airports" policy
///         in <c>Program.cs</c>).</item>
/// </list>
///
/// <para>
/// <b>Attribution.</b> Responses carry an <c>X-Data-Attribution</c> header
/// satisfying the OpenFlights CC-BY-SA 3.0 licence requirement.
/// </para>
/// </summary>
[ApiController]
[Route("airports")]
public class AirportsController : ControllerBase
{
    private const int MinPrefixLength = 2;
    private const int MaxPrefixLength = 8;
    private const int MaxLimit = 20;
    private const string AttributionHeaderValue =
        "Airport data by OpenFlights (openflights.org), licensed under CC-BY-SA 3.0";

    private readonly IAirportLookup _lookup;

    public AirportsController(IAirportLookup lookup)
    {
        _lookup = lookup;
    }

    [HttpGet]
    [EnableRateLimiting("airports")]
    [ResponseCache(Duration = 60, Location = ResponseCacheLocation.Any, VaryByQueryKeys = new[] { "q", "limit" })]
    public async Task<IActionResult> Get(
        [FromQuery] string? q,
        [FromQuery] int limit = 10,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(q))
            return BadRequest(new { error = "Query parameter 'q' is required." });

        var trimmed = q.Trim();
        if (trimmed.Length < MinPrefixLength)
            return BadRequest(new { error = $"Query must be at least {MinPrefixLength} characters." });
        if (trimmed.Length > MaxPrefixLength)
            return BadRequest(new { error = $"Query must be at most {MaxPrefixLength} characters." });

        var safeLimit = Math.Clamp(limit, 1, MaxLimit);

        var results = await _lookup.SearchAsync(trimmed, safeLimit, ct).ConfigureAwait(false);

        Response.Headers["X-Data-Attribution"] = AttributionHeaderValue;
        return Ok(results);
    }
}
