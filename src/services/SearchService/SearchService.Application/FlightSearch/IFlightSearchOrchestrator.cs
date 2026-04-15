using TBE.Contracts.Inventory.Models;

namespace TBE.SearchService.Application.FlightSearch;

public interface IFlightSearchOrchestrator
{
    Task<IReadOnlyList<UnifiedFlightOffer>> SearchAsync(FlightSearchRequest request, CancellationToken ct = default);
}
