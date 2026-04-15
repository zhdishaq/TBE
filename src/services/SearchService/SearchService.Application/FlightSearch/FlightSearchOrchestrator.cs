using Microsoft.Extensions.Logging;
using TBE.Contracts.Inventory;
using TBE.Contracts.Inventory.Models;

namespace TBE.SearchService.Application.FlightSearch;

public sealed class FlightSearchOrchestrator(
    IEnumerable<IFlightAvailabilityProvider> providers,
    ILogger<FlightSearchOrchestrator> logger) : IFlightSearchOrchestrator
{
    public async Task<IReadOnlyList<UnifiedFlightOffer>> SearchAsync(
        FlightSearchRequest request, CancellationToken ct = default)
    {
        // CRITICAL: wrap each provider in SearchSafeAsync — bare Task.WhenAll propagates exceptions
        var tasks = providers.Select(p => SearchSafeAsync(p, request, ct));
        var results = await Task.WhenAll(tasks);
        return FlightOfferDeduplicator.Deduplicate(results.SelectMany(r => r));
    }

    private async Task<IReadOnlyList<UnifiedFlightOffer>> SearchSafeAsync(
        IFlightAvailabilityProvider provider, FlightSearchRequest request, CancellationToken ct)
    {
        try
        {
            return await provider.SearchAsync(request, ct);
        }
        catch (Exception ex)
        {
            // One GDS failure must NOT fail the whole search — return empty and log
            logger.LogWarning(ex, "GDS provider {Name} failed for search {Origin}-{Dest}",
                provider.Name, request.Origin, request.Destination);
            return [];
        }
    }
}
