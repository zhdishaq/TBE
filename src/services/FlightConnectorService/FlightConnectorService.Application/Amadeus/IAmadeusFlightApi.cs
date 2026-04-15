using Refit;
using TBE.FlightConnectorService.Application.Amadeus.Models;

namespace TBE.FlightConnectorService.Application.Amadeus;

[Headers("Accept: application/json")]
public interface IAmadeusFlightApi
{
    [Get("/shopping/flight-offers")]
    Task<AmadeusFlightOffersResponse> SearchAsync(
        [AliasAs("originLocationCode")]      string origin,
        [AliasAs("destinationLocationCode")] string destination,
        [AliasAs("departureDate")]           string departureDate,
        [AliasAs("adults")]                  int adults,
        [AliasAs("returnDate")]              string? returnDate = null,
        [AliasAs("children")]                int? children = null,
        [AliasAs("infants")]                 int? infants = null,
        [AliasAs("travelClass")]             string? travelClass = null,
        [AliasAs("nonStop")]                 bool? nonStop = null,
        [AliasAs("currencyCode")]            string? currencyCode = null,
        [AliasAs("max")]                     int max = 50,
        CancellationToken cancellationToken = default);
}
