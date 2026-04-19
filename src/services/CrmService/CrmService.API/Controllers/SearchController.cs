using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TBE.CrmService.Infrastructure;

namespace TBE.CrmService.API.Controllers;

/// <summary>
/// Plan 06-04 Task 1 / CRM-03 — unified backoffice search across
/// BookingProjection / CustomerProjection / AgencyProjection. Fan-outs
/// via <see cref="Task.WhenAll{TResult}"/>; caps each source at 20
/// and the merged result at 50 (T-6-58 DoS mitigation).
/// </summary>
/// <remarks>
/// Exposed on BOTH <c>/api/crm/search</c> AND <c>/api/backoffice/search</c>
/// so the YARP gateway can route through either the CRM cluster (preferred)
/// or the Backoffice cluster (fallback if CRM is rebuilding).
/// </remarks>
[ApiController]
[Authorize(Policy = "BackofficeReadPolicy", AuthenticationSchemes = "Backoffice")]
public sealed class SearchController : ControllerBase
{
    private readonly CrmDbContext _db;

    public SearchController(CrmDbContext db) => _db = db;

    public sealed record SearchResult(string Kind, Guid Id, string Label);

    [HttpGet("api/crm/search")]
    [HttpGet("api/backoffice/search")]
    public async Task<IActionResult> Search([FromQuery] string? q, CancellationToken ct)
    {
        var term = q?.Trim();
        if (string.IsNullOrEmpty(term) || term.Length < 2)
        {
            return Problem(
                title: "Search term must be at least 2 characters",
                type: "/errors/search-term-too-short",
                statusCode: 400);
        }

        var like = $"%{term}%";

        var bookingsQ = _db.BookingProjections.AsNoTracking()
            .Where(b => (b.Pnr != null && EF.Functions.Like(b.Pnr, like))
                     || (b.BookingReference != null && EF.Functions.Like(b.BookingReference, like))
                     || (b.CustomerName != null && EF.Functions.Like(b.CustomerName, like)))
            .OrderByDescending(b => b.ConfirmedAt)
            .Take(20)
            .Select(b => new SearchResult(
                "booking",
                b.Id,
                $"Booking {(b.Pnr ?? b.BookingReference ?? b.Id.ToString())}"
                    + (b.CustomerName != null ? $" — {b.CustomerName}" : "")));

        var customersQ = _db.Customers.AsNoTracking()
            .Where(c => !c.IsErased
                     && ((c.Email != null && EF.Functions.Like(c.Email, like))
                      || (c.Name != null && EF.Functions.Like(c.Name, like))))
            .OrderByDescending(c => c.CreatedAt)
            .Take(20)
            .Select(c => new SearchResult("customer", c.Id, c.Name ?? c.Email ?? c.Id.ToString()));

        var agenciesQ = _db.Agencies.AsNoTracking()
            .Where(a => EF.Functions.Like(a.Name, like))
            .OrderBy(a => a.Name)
            .Take(20)
            .Select(a => new SearchResult("agency", a.Id, a.Name));

        var bookingsT = bookingsQ.ToListAsync(ct);
        var customersT = customersQ.ToListAsync(ct);
        var agenciesT = agenciesQ.ToListAsync(ct);
        await Task.WhenAll(bookingsT, customersT, agenciesT);

        var all = new List<SearchResult>();
        all.AddRange(bookingsT.Result);
        all.AddRange(customersT.Result);
        all.AddRange(agenciesT.Result);

        var ranked = all
            .OrderByDescending(r => r.Label.StartsWith(term, StringComparison.OrdinalIgnoreCase))
            .ThenBy(r => r.Kind)
            .Take(50)
            .ToList();

        return Ok(new { q = term, results = ranked });
    }
}
