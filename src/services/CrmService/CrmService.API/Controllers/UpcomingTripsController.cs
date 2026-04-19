using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TBE.CrmService.Infrastructure;

namespace TBE.CrmService.API.Controllers;

/// <summary>
/// Plan 06-04 Task 1 / CRM-05 — Upcoming Trips surface. Default window
/// is today..today+30d; callers can override via from/to query. Supports
/// filter by status / agency (useful for ops-cs triage).
/// </summary>
[ApiController]
[Route("api/crm/trips/upcoming")]
[Authorize(Policy = "BackofficeReadPolicy", AuthenticationSchemes = "Backoffice")]
public sealed class UpcomingTripsController : ControllerBase
{
    private readonly CrmDbContext _db;

    public UpcomingTripsController(CrmDbContext db) => _db = db;

    [HttpGet("")]
    public async Task<IActionResult> List(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? status,
        [FromQuery] Guid? agencyId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize is < 1 or > 100) pageSize = 20;

        var fromDate = from ?? DateTime.UtcNow.Date;
        var toDate = to ?? DateTime.UtcNow.Date.AddDays(30);

        var q = _db.UpcomingTrips.AsNoTracking()
            .Where(u => u.TravelDate >= fromDate && u.TravelDate <= toDate);

        if (!string.IsNullOrWhiteSpace(status))
        {
            q = q.Where(u => u.Status == status);
        }
        if (agencyId is Guid aid)
        {
            q = q.Where(u => u.AgencyId == aid);
        }

        var total = await q.CountAsync(ct);
        var rows = await q
            .OrderBy(u => u.TravelDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return Ok(new { total, page, pageSize, rows });
    }
}
