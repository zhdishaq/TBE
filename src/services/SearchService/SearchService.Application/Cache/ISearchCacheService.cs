using TBE.Contracts.Inventory.Models;

namespace TBE.SearchService.Application.Cache;

public interface ISearchCacheService
{
    Task<IReadOnlyList<UnifiedFlightOffer>> GetOrSearchAsync(
        string cacheKey,
        Func<CancellationToken, ValueTask<IReadOnlyList<UnifiedFlightOffer>>> factory,
        bool isSelection = false,
        CancellationToken ct = default);

    Task StoreBookingTokenAsync(string sessionId, UnifiedFlightOffer offer, CancellationToken ct = default);

    Task<UnifiedFlightOffer?> GetBookingTokenAsync(string sessionId, CancellationToken ct = default);
}
