using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TBE.BookingService.Infrastructure;

namespace TBE.BookingService.API.Controllers;

/// <summary>
/// Plan 05-04 Task 1 — Agency dashboard summary endpoint.
///
/// <para>
/// Returns a single summary DTO for the caller's agency covering wallet
/// balance, TTL alert counts (urgent/warn), pending bookings, and the top-5
/// recent bookings. One RSC-friendly request replaces the four separate
/// lookups the portal would otherwise have to chain.
/// </para>
///
/// <para>
/// <b>D-34 OVERRIDE</b> — All agent roles see AGENCY-WIDE counts. Filter by
/// <c>agency_id</c> claim ONLY; never additionally by <c>sub</c>. See
/// <c>.planning/phases/05-b2b-agent-portal/05-CONTEXT.md</c> D-34.
/// </para>
///
/// <para>
/// <b>Pitfall 28</b> — missing <c>agency_id</c> claim is a fail-closed 401
/// not a "default to all agencies" smell.
/// </para>
///
/// <para>
/// NOTE: This is a Plan 05-04 Task 1 RED stub. The GREEN implementation
/// arrives in the same plan once the RED tests pin the contract.
/// </para>
/// </summary>
[ApiController]
[Route("api/dashboard")]
[Authorize(Policy = "B2BPolicy")]
public sealed class AgencyDashboardController(
    BookingDbContext db,
    ILogger<AgencyDashboardController> logger) : ControllerBase
{
    [HttpGet("summary")]
    public Task<IActionResult> GetSummaryAsync(CancellationToken ct)
        => throw new NotImplementedException("Plan 05-04 Task 1 GREEN — pending implementation");
}
