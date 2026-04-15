using Microsoft.AspNetCore.Mvc;
using TBE.Contracts.Inventory;
using TBE.Contracts.Inventory.Models;

namespace TBE.HotelConnectorService.API.Controllers;

[ApiController]
[Route("cars")]
public class CarSearchController(ICarAvailabilityProvider car) : ControllerBase
{
    [HttpPost("search")]
    public async Task<IActionResult> Search([FromBody] CarSearchRequest request, CancellationToken ct)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(request.PickupLocationCode, @"^[A-Z]{3}$") ||
            !System.Text.RegularExpressions.Regex.IsMatch(request.DropoffLocationCode, @"^[A-Z]{3}$"))
            return BadRequest("PickupLocationCode and DropoffLocationCode must be valid 3-letter IATA codes.");

        var results = await car.SearchAsync(request, ct);
        return Ok(results);
    }
}
