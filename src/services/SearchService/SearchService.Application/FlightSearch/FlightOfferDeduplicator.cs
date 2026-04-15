using TBE.Contracts.Inventory.Models;

namespace TBE.SearchService.Application.FlightSearch;

public static class FlightOfferDeduplicator
{
    /// <summary>
    /// Removes offers with duplicate SourceRef (same offer from multiple GDS sources).
    /// When duplicates exist, the first occurrence is kept (typically lower-priced GDS appears first after sort).
    /// </summary>
    public static IReadOnlyList<UnifiedFlightOffer> Deduplicate(
        IEnumerable<UnifiedFlightOffer> offers) =>
        offers
            .DistinctBy(o => o.SourceRef)
            .OrderBy(o => o.Price.GrandTotal)
            .ToList();
}
