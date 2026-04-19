using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TBE.CrmService.Application.Projections;
using TBE.CrmService.Infrastructure;

namespace TBE.CrmService.API.Controllers;

/// <summary>
/// Plan 06-04 Task 1 / CRM-03 — Agency 360 read surface + admin-only
/// create hook. Markup rules / commission settlement remain owned by
/// PricingService + PaymentService; this controller is read-only for
/// those tabs (the UI fans out to the owning services).
/// </summary>
[ApiController]
[Route("api/crm/agencies")]
[Authorize(Policy = "BackofficeReadPolicy", AuthenticationSchemes = "Backoffice")]
public sealed class AgenciesController : ControllerBase
{
    private readonly CrmDbContext _db;

    public AgenciesController(CrmDbContext db) => _db = db;

    public sealed record CreateAgencyRequest(string Name, string? ContactEmail, string? ContactPhone);

    [HttpGet("")]
    public async Task<IActionResult> List(
        [FromQuery] string? q,
        [FromQuery] bool? active,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize is < 1 or > 100) pageSize = 20;

        var query = _db.Agencies.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var like = $"%{q.Trim()}%";
            query = query.Where(a => EF.Functions.Like(a.Name, like));
        }
        if (active is bool isActive)
        {
            query = query.Where(a => a.IsActive == isActive);
        }

        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderBy(a => a.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return Ok(new { total, page, pageSize, rows });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var a = await _db.Agencies.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (a is null) return NotFound();
        return Ok(a);
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

        var q = _db.BookingProjections.AsNoTracking().Where(b => b.AgencyId == id);
        var total = await q.CountAsync(ct);
        var rows = await q
            .OrderByDescending(b => b.ConfirmedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return Ok(new { total, page, pageSize, rows });
    }

    [HttpPost("")]
    [Authorize(Policy = "BackofficeAdminPolicy", AuthenticationSchemes = "Backoffice")]
    public async Task<IActionResult> Create(
        [FromBody] CreateAgencyRequest req,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
        {
            return Problem(
                title: "Agency name is required",
                type: "/errors/agency-name-required",
                statusCode: 400);
        }

        var row = new AgencyProjection
        {
            Id = Guid.NewGuid(),
            Name = req.Name.Trim(),
            ContactEmail = req.ContactEmail,
            ContactPhone = req.ContactPhone,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        };
        _db.Agencies.Add(row);
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(Get), new { id = row.Id }, row);
    }
}
