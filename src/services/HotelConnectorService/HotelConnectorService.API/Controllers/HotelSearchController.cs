using Microsoft.AspNetCore.Mvc;
using TBE.Contracts.Inventory;
using TBE.Contracts.Inventory.Models;

namespace TBE.HotelConnectorService.API.Controllers;

[ApiController]
[Route("hotels")]
public class HotelSearchController(IHotelAvailabilityProvider hotel) : ControllerBase
{
    [HttpPost("search")]
    public async Task<IActionResult> Search([FromBody] HotelSearchRequest request, CancellationToken ct)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(request.DestinationCode, @"^[A-Z]{3}$"))
            return BadRequest("DestinationCode must be a valid 3-letter IATA city code.");
        if (request.CheckOut <= request.CheckIn)
            return BadRequest("CheckOut must be after CheckIn.");

        var results = await hotel.SearchAsync(request, ct);
        return Ok(results);
    }
}
