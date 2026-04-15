using Refit;
using TBE.FlightConnectorService.Application.Sabre.Models;

namespace TBE.FlightConnectorService.Application.Sabre;

[Headers("Accept: application/json", "Content-Type: application/json")]
public interface ISabreFlightApi
{
    [Post("/v4.3.0/shop/flights/reqs")]
    Task<SabreBfmResponse> SearchAsync(
        [Body] SabreBfmRequest request,
        CancellationToken cancellationToken = default);
}
