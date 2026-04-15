using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;
using System.Text.Json;
using TBE.Contracts.Inventory.Models;

namespace TBE.SearchService.Application.Cache;

public sealed class SearchCacheService(HybridCache hybridCache, IDistributedCache redis) : ISearchCacheService
{
    // Browse TTL: 10 min in Redis (L2), 2 min in-process (L1)
    private static readonly HybridCacheEntryOptions BrowseTtl = new()
    {
        Expiration           = TimeSpan.FromMinutes(10),
        LocalCacheExpiration = TimeSpan.FromMinutes(2),
    };

    // Selection TTL: 90 sec in Redis, 30 sec in-process
    private static readonly HybridCacheEntryOptions SelectionTtl = new()
    {
        Expiration           = TimeSpan.FromSeconds(90),
        LocalCacheExpiration = TimeSpan.FromSeconds(30),
    };

    public Task<IReadOnlyList<UnifiedFlightOffer>> GetOrSearchAsync(
        string cacheKey,
        Func<CancellationToken, ValueTask<IReadOnlyList<UnifiedFlightOffer>>> factory,
        bool isSelection = false,
        CancellationToken ct = default)
    {
        var opts = isSelection ? SelectionTtl : BrowseTtl;
        // HybridCache provides stampede protection — only one factory execution per cache miss
        return hybridCache.GetOrCreateAsync<IReadOnlyList<UnifiedFlightOffer>>(
            cacheKey, factory, opts, cancellationToken: ct).AsTask();
    }

    /// <summary>
    /// Stores a booking token (fare snapshot) in Redis.
    /// TTL is derived from offer.ExpiresAt — NOT a fixed duration.
    /// </summary>
    public async Task StoreBookingTokenAsync(string sessionId, UnifiedFlightOffer offer, CancellationToken ct = default)
    {
        // SECURITY: session ID is caller-supplied UUID — validated as non-empty; key is namespaced
        if (string.IsNullOrWhiteSpace(sessionId))
            throw new ArgumentException("Session ID cannot be empty.", nameof(sessionId));

        var key = $"booking-token:{sessionId}";
        var ttl = offer.ExpiresAt - DateTimeOffset.UtcNow;
        if (ttl <= TimeSpan.Zero) ttl = TimeSpan.FromMinutes(30); // fallback if already expired

        var value = JsonSerializer.SerializeToUtf8Bytes(offer);
        await redis.SetAsync(key, value, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ttl
        }, ct);
    }

    public async Task<UnifiedFlightOffer?> GetBookingTokenAsync(string sessionId, CancellationToken ct = default)
    {
        var key = $"booking-token:{sessionId}";
        var value = await redis.GetAsync(key, ct);
        if (value is null) return null;
        return JsonSerializer.Deserialize<UnifiedFlightOffer>(value);
    }
}
