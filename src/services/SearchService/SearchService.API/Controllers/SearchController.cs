using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Json;
using TBE.Contracts.Inventory.Models;

namespace TBE.SearchService.API.Controllers;

[ApiController]
[Route("search")]
public class SearchController(IHttpClientFactory httpClientFactory) : ControllerBase
{
    // SearchService calls FlightConnectorService via internal HTTP — D-08 compliance
    [HttpPost("flights")]
    public async Task<IActionResult> SearchFlights(
        [FromBody] FlightSearchRequest request, CancellationToken ct)
    {
        // Validate IATA codes — defence in depth even though FlightConnectorService also validates
        if (!System.Text.RegularExpressions.Regex.IsMatch(request.Origin, @"^[A-Z]{3}$") ||
            !System.Text.RegularExpressions.Regex.IsMatch(request.Destination, @"^[A-Z]{3}$"))
            return BadRequest("Origin and Destination must be valid 3-letter IATA codes.");

        var client = httpClientFactory.CreateClient("flight-connector");
        var response = await client.PostAsJsonAsync("/flights/search", request, ct);

        if (!response.IsSuccessStatusCode)
            return StatusCode((int)response.StatusCode,
                await response.Content.ReadAsStringAsync(ct));

        var offers = await response.Content
            .ReadFromJsonAsync<IReadOnlyList<UnifiedFlightOffer>>(ct);
        return Ok(offers);
    }
}
