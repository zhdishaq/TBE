using TBE.Contracts.Inventory.Models;
namespace TBE.Contracts.Inventory;
public interface IHotelAvailabilityProvider
{
    string Name { get; }
    Task<IReadOnlyList<UnifiedHotelOffer>> SearchAsync(HotelSearchRequest request, CancellationToken ct = default);
}
