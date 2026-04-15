using TBE.Contracts.Inventory.Models;
namespace TBE.Contracts.Inventory;
public interface IFlightAvailabilityProvider
{
    string Name { get; }
    Task<IReadOnlyList<UnifiedFlightOffer>> SearchAsync(FlightSearchRequest request, CancellationToken ct = default);
}
