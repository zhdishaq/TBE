using Refit;
using TBE.FlightConnectorService.Application.Sabre.Models;

namespace TBE.FlightConnectorService.Application.Sabre;

[Headers("Accept: application/json", "Content-Type: application/json")]
public interface ISabreFlightApi
{
    // Sabre Bargain Finder Max v5 — official spec endpoint
    // Cert:  https://api.cert.platform.sabre.com/v5/offers/shop
    // Prod:  https://api.platform.sabre.com/v5/offers/shop
    [Post("/v5/offers/shop")]
    Task<SabreBfmResponse> SearchAsync(
        [Body] SabreBfmRequest request,
        CancellationToken cancellationToken = default);
}
