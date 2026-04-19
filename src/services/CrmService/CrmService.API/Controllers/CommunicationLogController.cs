using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TBE.Contracts.Events;
using TBE.CrmService.Application.Projections;
using TBE.CrmService.Infrastructure;

namespace TBE.CrmService.API.Controllers;

/// <summary>
/// Plan 06-04 Task 1 / CRM-04 / D-62 — free-form ops communications log.
/// Plain-text markdown body; no HTML sanitisation required because the
/// body is treated as untrusted source at render time.
/// </summary>
[ApiController]
[Route("api/crm/communication-log")]
[Authorize(Policy = "BackofficeReadPolicy", AuthenticationSchemes = "Backoffice")]
public sealed class CommunicationLogController : ControllerBase
{
    private readonly CrmDbContext _db;
    private readonly IPublishEndpoint _publish;

    public CommunicationLogController(CrmDbContext db, IPublishEndpoint publish)
    {
        _db = db;
        _publish = publish;
    }

    public sealed record CreateLogRequest(string EntityType, Guid EntityId, string Body);

    [HttpGet("")]
    public async Task<IActionResult> List(
        [FromQuery] string entityType,
        [FromQuery] Guid entityId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize is < 1 or > 100) pageSize = 20;

        if (string.IsNullOrWhiteSpace(entityType))
        {
            return Problem(
                title: "entityType is required",
                type: "/errors/communication-log-entity-type-required",
                statusCode: 400);
        }

        var q = _db.CommunicationLog.AsNoTracking()
            .Where(c => c.EntityType == entityType && c.EntityId == entityId);

        if (from is DateTime f) q = q.Where(c => c.CreatedAt >= f);
        if (to is DateTime t) q = q.Where(c => c.CreatedAt <= t);

        var total = await q.CountAsync(ct);
        var rows = await q
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return Ok(new { total, page, pageSize, rows });
    }

    [HttpPost("")]
    [Authorize(Policy = "BackofficeCsPolicy", AuthenticationSchemes = "Backoffice")]
    public async Task<IActionResult> Create(
        [FromBody] CreateLogRequest req,
        CancellationToken ct = default)
    {
        if (req is null || string.IsNullOrWhiteSpace(req.EntityType))
        {
            return Problem(
                title: "entityType is required",
                type: "/errors/communication-log-entity-type-required",
                statusCode: 400);
        }
        if (req.EntityType is not ("Customer" or "Agency"))
        {
            return Problem(
                title: "entityType must be 'Customer' or 'Agency'",
                type: "/errors/communication-log-entity-type-invalid",
                statusCode: 400);
        }
        if (string.IsNullOrWhiteSpace(req.Body))
        {
            return Problem(
                title: "body is required",
                type: "/errors/communication-log-body-required",
                statusCode: 400);
        }
        if (req.Body.Length > 10000)
        {
            return Problem(
                title: "body exceeds 10000 chars",
                type: "/errors/communication-log-body-too-long",
                statusCode: 400);
        }

        // Pitfall 28 — actor is the Keycloak preferred_username; fail-closed
        // if the claim is absent.
        var actor = User.FindFirst("preferred_username")?.Value;
        if (string.IsNullOrWhiteSpace(actor))
        {
            return Problem(
                title: "preferred_username claim missing",
                type: "/errors/auth-missing-actor",
                statusCode: 401);
        }

        var row = new CommunicationLogRow
        {
            LogId = Guid.NewGuid(),
            EntityType = req.EntityType,
            EntityId = req.EntityId,
            CreatedBy = actor,
            CreatedAt = DateTime.UtcNow,
            Body = req.Body,
        };

        _db.CommunicationLog.Add(row);
        await _db.SaveChangesAsync(ct);

        // Atomic outbox publish — the EF outbox on CrmDbContext captures
        // this envelope in the same transaction as the SaveChangesAsync
        // above (Plan 03-01 pattern).
        await _publish.Publish(new CustomerCommunicationLogged(
            row.LogId, row.EntityType, row.EntityId, row.CreatedBy, row.Body, row.CreatedAt), ct);

        return CreatedAtAction(nameof(List), new { entityType = row.EntityType, entityId = row.EntityId }, row);
    }
}
