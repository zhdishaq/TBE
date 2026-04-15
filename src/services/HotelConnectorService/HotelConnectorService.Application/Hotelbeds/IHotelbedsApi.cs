using Refit;
using TBE.HotelConnectorService.Application.Hotelbeds.Models;

namespace TBE.HotelConnectorService.Application.Hotelbeds;

[Headers("Accept: application/json", "Content-Type: application/json")]
public interface IHotelbedsApi
{
    [Post("/hotels")]
    Task<HotelbedsAvailabilityResponse> SearchAvailabilityAsync(
        [Body] HotelbedsAvailabilityRequest request,
        CancellationToken cancellationToken = default);
}
