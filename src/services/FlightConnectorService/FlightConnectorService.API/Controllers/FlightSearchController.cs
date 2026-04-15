using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using TBE.Contracts.Inventory;
using TBE.Contracts.Inventory.Models;

namespace TBE.FlightConnectorService.API.Controllers;

[ApiController]
[Route("flights")]
public class FlightSearchController(
    [FromKeyedServices("amadeus")] IFlightAvailabilityProvider amadeus) : ControllerBase
{
    [HttpPost("search")]
    public async Task<IActionResult> Search([FromBody] FlightSearchRequest request, CancellationToken ct)
    {
        // Input validation: IATA codes must be 3 uppercase letters
        if (!System.Text.RegularExpressions.Regex.IsMatch(request.Origin, @"^[A-Z]{3}$") ||
            !System.Text.RegularExpressions.Regex.IsMatch(request.Destination, @"^[A-Z]{3}$"))
            return BadRequest("Origin and Destination must be valid 3-letter IATA codes.");

        if (request.Adults < 1 || request.Adults > 9)
            return BadRequest("Adults must be between 1 and 9.");

        var results = await amadeus.SearchAsync(request, ct);
        return Ok(results);
    }
}
