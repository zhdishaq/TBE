using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TBE.CrmService.Infrastructure;

namespace TBE.CrmService.API.Controllers;

/// <summary>
/// Plan 06-04 Task 1 / CRM-01 — read-only surface for the backoffice
/// Customer 360 page. Anonymised customers (IsErased=true) still return
/// a row with NULL PII so the portal can render the "Anonymised Customer"
/// banner while the booking history tab remains functional.
/// </summary>
[ApiController]
[Route("api/crm/customers")]
[Authorize(Policy = "BackofficeReadPolicy", AuthenticationSchemes = "Backoffice")]
public sealed class CustomersController : ControllerBase
{
    private readonly CrmDbContext _db;

    public CustomersController(CrmDbContext db) => _db = db;

    [HttpGet("")]
    public async Task<IActionResult> List(
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize is < 1 or > 100) pageSize = 20;

        var query = _db.Customers.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var like = $"%{q.Trim()}%";
            query = query.Where(c =>
                (c.Email != null && EF.Functions.Like(c.Email, like))
                || (c.Name != null && EF.Functions.Like(c.Name, like)));
        }

        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return Ok(new { total, page, pageSize, rows });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var c = await _db.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return NotFound();
        return Ok(c);
    }

    [HttpGet("{id:guid}/bookings")]
    public async Task<IActionResult> Bookings(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize is < 1 or > 100) pageSize = 20;

        var q = _db.BookingProjections.AsNoTracking().Where(b => b.CustomerId == id);
        var total = await q.CountAsync(ct);
        var rows = await q
            .OrderByDescending(b => b.ConfirmedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return Ok(new { total, page, pageSize, rows });
    }

    [HttpGet("erasures")]
    [Authorize(Policy = "BackofficeAdminPolicy", AuthenticationSchemes = "Backoffice")]
    public async Task<IActionResult> Erasures(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize is < 1 or > 100) pageSize = 20;

        var q = _db.CustomerErasureTombstones.AsNoTracking();
        var total = await q.CountAsync(ct);
        var rows = await q
            .OrderByDescending(t => t.ErasedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return Ok(new { total, page, pageSize, rows });
    }
}
