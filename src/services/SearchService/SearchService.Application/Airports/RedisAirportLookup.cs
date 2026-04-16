using StackExchange.Redis;

namespace TBE.SearchService.Application.Airports;

/// <summary>
/// Redis-backed IATA airport typeahead (CONTEXT D-18, Pitfall 10).
///
/// Storage layout (written by <c>IataAirportSeeder</c>):
///   HSET iata:airports {IATA} {json}       — canonical row (HashSet lookup)
///   ZADD iata:idx:prefix 0 {prefix}|{IATA}  — prefix index (sorted-set scan)
///
/// The sorted-set-range-by-value trick: when every element scores 0 the
/// set is lexically ordered, so
///   SortedSetRangeByValueAsync(key, min="lon", max="lon" + END)
/// returns every member whose value starts with "lon|" in O(log N + M).
/// That is the O(log N) prefix scan we need for a ≥60 req/min/IP endpoint.
///
/// Note: SearchService's two-project layout (API + Application) has no
/// Infrastructure project, so this implementation lives in Application.
/// See 04-02-SUMMARY §Deviations for the rationale.
/// </summary>
public sealed class RedisAirportLookup : IAirportLookup
{
    private const string AirportsHashKey = "iata:airports";
    private const string PrefixIndexKey = "iata:idx:prefix";
    private const char PrefixSeparator = '|';

    // End-of-range marker. `|` (0x7C) is the separator used in the value
    // "{prefix}|{IATA}", so the lexical upper bound for "lon*" is
    // "lon" + "}"  (0x7D, i.e. char(PrefixSeparator) + 1). SortedSet range
    // comparison is strictly lexical. We store as a string because RedisValue
    // has ambiguous implicit conversions from `char`.
    private static readonly string RangeEndChar = ((char)(PrefixSeparator + 1)).ToString();

    private readonly IConnectionMultiplexer _redis;

    public RedisAirportLookup(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task<IReadOnlyList<AirportDto>> SearchAsync(
        string prefix,
        int limit,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(prefix)) return Array.Empty<AirportDto>();

        var normalised = prefix.Trim().ToLowerInvariant();
        var safeLimit = Math.Max(1, Math.Min(limit, 20));

        var db = _redis.GetDatabase();

        // Prefix scan via sorted-set lexical range. We request 2x the final
        // limit so we can dedupe across city/name/IATA-prefix collisions and
        // still land at `safeLimit` distinct IATAs.
        RedisValue min = normalised + PrefixSeparator;

        // Upper bound: keep the same prefix but advance the separator. Every
        // member written by the seeder is "{prefix}|{IATA}" so anything
        // lexically < "{prefix}}" and > "{prefix}|" is a prefix hit.
        RedisValue max = normalised + RangeEndChar;

        var hits = await db.SortedSetRangeByValueAsync(
            PrefixIndexKey,
            min,
            max,
            Exclude.None,
            take: safeLimit * 4).ConfigureAwait(false);

        if (hits.Length == 0) return Array.Empty<AirportDto>();

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var codes = new List<string>(safeLimit);
        foreach (var hit in hits)
        {
            var raw = (string?)hit;
            if (raw is null) continue;
            var sep = raw.IndexOf(PrefixSeparator);
            if (sep < 0 || sep == raw.Length - 1) continue;
            var iata = raw[(sep + 1)..];
            if (seen.Add(iata)) codes.Add(iata);
            if (codes.Count >= safeLimit) break;
        }
        if (codes.Count == 0) return Array.Empty<AirportDto>();

        // HashField batch-get by IATA. StackExchange.Redis HashGetAsync takes
        // an array of fields and returns values in the same order.
        var fields = codes.Select(c => (RedisValue)c).ToArray();
        var jsons = await db.HashGetAsync(AirportsHashKey, fields).ConfigureAwait(false);

        var result = new List<AirportDto>(jsons.Length);
        for (var i = 0; i < jsons.Length; i++)
        {
            var json = (string?)jsons[i];
            if (string.IsNullOrEmpty(json)) continue;
            var dto = DeserializeAirport(json);
            if (dto is not null) result.Add(dto);
        }

        return result;
    }

    /// <summary>
    /// Minimalist JSON reader. Avoids pulling System.Text.Json into the
    /// hot path; the schema is fixed and produced by the seeder.
    /// </summary>
    private static AirportDto? DeserializeAirport(string json)
    {
        static string? ReadField(string src, string field)
        {
            var token = $"\"{field}\":\"";
            var start = src.IndexOf(token, StringComparison.Ordinal);
            if (start < 0) return null;
            start += token.Length;
            var sb = new System.Text.StringBuilder();
            for (var i = start; i < src.Length; i++)
            {
                var c = src[i];
                if (c == '\\' && i + 1 < src.Length)
                {
                    sb.Append(src[++i]);
                }
                else if (c == '"')
                {
                    return sb.ToString();
                }
                else
                {
                    sb.Append(c);
                }
            }
            return null;
        }

        var iata = ReadField(json, "iata");
        var name = ReadField(json, "name");
        var city = ReadField(json, "city");
        var country = ReadField(json, "country");
        if (iata is null || name is null || city is null || country is null) return null;
        return new AirportDto(iata, name, city, country);
    }
}
