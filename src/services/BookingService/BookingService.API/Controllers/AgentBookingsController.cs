using MassTransit;
using Microsoft.AspNetCore.Mvc;
using TBE.BookingService.Infrastructure;

namespace TBE.BookingService.API.Controllers;

/// <summary>
/// Plan 05-02 Task 2 RED-phase stub. The body-less implementation throws so
/// the Task 2 controller tests fail by assertion; the GREEN commit replaces
/// the bodies with the real 403/401/agency-scoped flow.
/// </summary>
[ApiController]
[Route("agent/bookings")]
public sealed class AgentBookingsController(
    BookingDbContext db,
    IPublishEndpoint publishEndpoint,
    ILogger<AgentBookingsController> logger) : ControllerBase
{
    [HttpPost]
    public Task<IActionResult> CreateAsync(
        [FromBody] CreateAgentBookingRequest req,
        CancellationToken ct)
        => throw new NotImplementedException("05-02 Task 2 GREEN — AgentBookingsController.CreateAsync");

    [HttpGet("me")]
    public Task<IActionResult> ListForAgencyAsync(
        int page = 1,
        int size = 20,
        CancellationToken ct = default)
        => throw new NotImplementedException("05-02 Task 2 GREEN — AgentBookingsController.ListForAgencyAsync");

    [HttpGet("{id:guid}")]
    public Task<IActionResult> GetByIdAsync(Guid id, CancellationToken ct)
        => throw new NotImplementedException("05-02 Task 2 GREEN — AgentBookingsController.GetByIdAsync");
}

/// <summary>
/// POST /agent/bookings request body. Intentionally OMITS <c>AgencyId</c> and
/// <c>Channel</c> so they cannot be forged (T-05-02-01 / T-05-02-08).
/// </summary>
public sealed record CreateAgentBookingRequest(
    string ProductType,
    string OfferId,
    decimal AgencyNetFare,
    decimal AgencyMarkupAmount,
    decimal AgencyGrossAmount,
    decimal AgencyCommissionAmount,
    decimal? AgencyMarkupOverride,
    string Currency,
    Guid? WalletId,
    string CustomerName,
    string CustomerEmail,
    string CustomerPhone);
