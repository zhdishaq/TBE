using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using TBE.Contracts.Inventory;
using TBE.Contracts.Inventory.Models;

namespace TBE.FlightConnectorService.API.Controllers;

[ApiController]
[Authorize]
[Route("flights")]
public class FlightSearchController(
    IEnumerable<IFlightAvailabilityProvider> providers,
    ILogger<FlightSearchController> logger) : ControllerBase
{
    [HttpPost("search")]
    public async Task<IActionResult> Search(
        [FromBody] FlightSearchRequest request,
        [FromQuery] string? source,
        CancellationToken ct)
    {
        // Input validation: IATA codes must be 3 uppercase letters
        if (!System.Text.RegularExpressions.Regex.IsMatch(request.Origin, @"^[A-Z]{3}$") ||
            !System.Text.RegularExpressions.Regex.IsMatch(request.Destination, @"^[A-Z]{3}$"))
            return BadRequest("Origin and Destination must be valid 3-letter IATA codes.");

        if (request.Adults < 1 || request.Adults > 9)
            return BadRequest("Adults must be between 1 and 9.");

        // Filter by source name if provided; otherwise fan-out to all providers
        var selected = source is null || source == "all"
            ? providers
            : providers.Where(p => p.Name.Equals(source, StringComparison.OrdinalIgnoreCase));

        var tasks = selected.Select(p => SearchSafeAsync(p, request, ct));
        var results = await Task.WhenAll(tasks);
        var combined = results.SelectMany(r => r).ToList();
        return Ok(combined);
    }

    private async Task<IReadOnlyList<UnifiedFlightOffer>> SearchSafeAsync(
        IFlightAvailabilityProvider provider, FlightSearchRequest request, CancellationToken ct)
    {
        try
        {
            return await provider.SearchAsync(request, ct);
        }
        catch (OperationCanceledException)
        {
            throw; // propagate cancellation — do not swallow
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Provider {Provider} failed during flight search", provider.Name);
            return [];
        }
    }
}
