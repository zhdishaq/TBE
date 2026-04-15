using Refit;
using TBE.HotelConnectorService.Application.Car.Models;

namespace TBE.HotelConnectorService.Application.Car;

[Headers("Accept: application/json")]
public interface IAmadeusTransferApi
{
    [Get("/shopping/availability/transfer-offers")]
    Task<AmadeusTransferOffersResponse> SearchAsync(
        [AliasAs("startLocationCode")]  string startLocationCode,
        [AliasAs("endLocationCode")]    string endLocationCode,
        [AliasAs("transferType")]       string transferType,
        [AliasAs("startDateTime")]      string startDateTime,       // yyyy-MM-ddTHH:mm:ss
        [AliasAs("passengers")]         int passengers = 1,
        CancellationToken cancellationToken = default);
}
