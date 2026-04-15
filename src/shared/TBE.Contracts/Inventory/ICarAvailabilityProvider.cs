using TBE.Contracts.Inventory.Models;
namespace TBE.Contracts.Inventory;
public interface ICarAvailabilityProvider
{
    string Name { get; }
    Task<IReadOnlyList<UnifiedCarOffer>> SearchAsync(CarSearchRequest request, CancellationToken ct = default);
}
