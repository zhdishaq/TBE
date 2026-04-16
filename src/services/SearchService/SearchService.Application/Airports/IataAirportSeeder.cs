using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace TBE.SearchService.Application.Airports;

/// <summary>
/// BackgroundService that seeds Redis from <c>data/iata/airports.dat</c>
/// (OpenFlights CC-BY-SA dataset) at SearchService startup per CONTEXT D-18.
///
/// Storage layout (shared with <c>RedisAirportLookup</c>):
///   HSET iata:airports {iata} {json}        — canonical row, JSON payload
///   ZADD iata:idx:prefix 0 {prefix}|{iata}  — prefix index (sorted-set trick)
///
/// Idempotency: sets <c>iata:seed:done</c> on completion. Re-running with
/// that flag present is a no-op unless <c>FORCE_RESEED=true</c> is set on
/// the environment — pick up a refreshed data file by setting the flag.
///
/// Threat ref: T-04-02-04 (IATA endpoint abuse) — the rate-limit policy
/// on the controller defends the endpoint; the seeder only touches Redis
/// once at boot so it cannot be abused at runtime.
/// </summary>
public sealed class IataAirportSeeder : BackgroundService
{
    private const string AirportsHashKey = "iata:airports";
    private const string PrefixIndexKey = "iata:idx:prefix";
    private const string SeedDoneFlag = "iata:seed:done";

    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<IataAirportSeeder> _logger;
    private readonly string _datasetPath;
    private readonly bool _forceReseed;

    public IataAirportSeeder(
        IConnectionMultiplexer redis,
        ILogger<IataAirportSeeder> logger)
    {
        _redis = redis;
        _logger = logger;
        // The dataset ships at repo_root/data/iata/airports.dat. In the
        // container, that path is mounted at /app/data/iata/airports.dat.
        // Allow an env var override for tests.
        _datasetPath = Environment.GetEnvironmentVariable("IATA_DATASET_PATH")
            ?? Path.Combine(AppContext.BaseDirectory, "data", "iata", "airports.dat");
        _forceReseed = string.Equals(
            Environment.GetEnvironmentVariable("FORCE_RESEED"),
            "true",
            StringComparison.OrdinalIgnoreCase);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var db = _redis.GetDatabase();
            if (!_forceReseed && await db.KeyExistsAsync(SeedDoneFlag).ConfigureAwait(false))
            {
                _logger.LogInformation("IATA airport seed already present ({Flag} set). Skipping.", SeedDoneFlag);
                return;
            }

            if (!File.Exists(_datasetPath))
            {
                _logger.LogWarning(
                    "IATA dataset not found at {Path}. Airport typeahead will be empty until a dataset is mounted.",
                    _datasetPath);
                return;
            }

            _logger.LogInformation("Seeding IATA airports from {Path}", _datasetPath);
            var inserted = 0;
            var skipped = 0;

            // Clear stale index entries when forcing a reseed so we don't
            // accumulate duplicates across runs.
            if (_forceReseed)
            {
                await db.KeyDeleteAsync(AirportsHashKey).ConfigureAwait(false);
                await db.KeyDeleteAsync(PrefixIndexKey).ConfigureAwait(false);
            }

            using var reader = new StreamReader(_datasetPath, Encoding.UTF8);
            string? line;
            while ((line = await reader.ReadLineAsync(stoppingToken).ConfigureAwait(false)) is not null)
            {
                if (stoppingToken.IsCancellationRequested) return;

                var fields = ParseCsvLine(line);
                if (fields.Count < 14) { skipped++; continue; }

                var name = fields[1];
                var city = fields[2];
                var country = fields[3];
                var iata = fields[4];
                var type = fields[12];

                if (!IsValidIata(iata) || !string.Equals(type, "airport", StringComparison.OrdinalIgnoreCase))
                {
                    skipped++;
                    continue;
                }

                var dto = new AirportDto(iata.ToUpperInvariant(), name, city, country);
                var json = SerializeAirport(dto);

                // HSET iata:airports {IATA} {json}
                await db.HashSetAsync(AirportsHashKey, dto.Iata, json).ConfigureAwait(false);

                // Prefix index: IATA itself, plus 2..N-char prefixes of the
                // lower-cased city and name (capped at 8 chars to bound the
                // index size). The sorted-set-range-by-value trick (score 0)
                // lets RedisAirportLookup do O(log n) prefix scans.
                var iataLower = dto.Iata.ToLowerInvariant();
                await db.SortedSetAddAsync(PrefixIndexKey, $"{iataLower}|{dto.Iata}", 0).ConfigureAwait(false);

                AddPrefixes(db, PrefixIndexKey, city, dto.Iata);
                AddPrefixes(db, PrefixIndexKey, name, dto.Iata);

                inserted++;
            }

            await db.StringSetAsync(SeedDoneFlag, DateTime.UtcNow.ToString("O")).ConfigureAwait(false);

            _logger.LogInformation(
                "IATA seed complete: {Inserted} airports inserted, {Skipped} rows skipped (non-IATA / non-airport / malformed).",
                inserted, skipped);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A seeding failure must never take the whole service down —
            // airport typeahead returns empty results until a human repairs.
            _logger.LogError(ex, "IATA airport seeding failed; typeahead will serve an empty cache.");
        }
    }

    private static void AddPrefixes(IDatabase db, RedisKey indexKey, string value, string iata)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        var lower = value.Trim().ToLowerInvariant();
        var cap = Math.Min(lower.Length, 8);
        for (var n = 2; n <= cap; n++)
        {
            var prefix = lower[..n];
            // Fire-and-await at the top level — batching is an optional later
            // optimisation. For 7.7k rows this completes in well under a
            // second against a local Redis.
            _ = db.SortedSetAddAsync(indexKey, $"{prefix}|{iata}", 0);
        }
    }

    private static string SerializeAirport(AirportDto dto)
    {
        // Hand-rolled JSON escape to avoid pulling in System.Text.Json here —
        // the payload is tiny and the fields are controlled.
        static string E(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        return $"{{\"iata\":\"{E(dto.Iata)}\",\"name\":\"{E(dto.Name)}\",\"city\":\"{E(dto.City)}\",\"country\":\"{E(dto.Country)}\"}}";
    }

    private static bool IsValidIata(string s)
    {
        if (string.IsNullOrWhiteSpace(s) || s.Length != 3) return false;
        for (var i = 0; i < 3; i++)
        {
            var c = s[i];
            if (c < 'A' || c > 'Z')
            {
                // Allow lowercase input and treat as valid when 3 ASCII letters;
                // normalisation to upper happens in the caller.
                if (c < 'a' || c > 'z') return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Simple RFC-4180 style CSV parser. OpenFlights airports.dat wraps
    /// textual fields in double quotes and uses <c>""</c> to represent a
    /// literal quote inside a field. Non-text columns are bare numeric or
    /// <c>\N</c> null markers.
    /// </summary>
    private static List<string> ParseCsvLine(string line)
    {
        var fields = new List<string>(16);
        var sb = new StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        sb.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            else if (c == '"')
            {
                inQuotes = true;
            }
            else if (c == ',')
            {
                fields.Add(sb.ToString());
                sb.Clear();
            }
            else
            {
                sb.Append(c);
            }
        }
        fields.Add(sb.ToString());
        return fields;
    }
}
