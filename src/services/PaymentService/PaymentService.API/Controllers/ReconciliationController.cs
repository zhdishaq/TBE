using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TBE.PaymentService.Infrastructure;
using TBE.PaymentService.Infrastructure.Reconciliation;

namespace TBE.PaymentService.API.Controllers;

/// <summary>
/// Plan 06-02 Task 3 (BO-06) — payment reconciliation queue surface.
///
/// <para>
/// List endpoint is open to any ops-* role via <c>BackofficeReadPolicy</c>
/// (same realm scheme as BackofficeService controllers so tokens from
/// tbe-b2b / tbe-customer cannot reach it — Pitfall 4). Resolve endpoint
/// is gated on <c>BackofficeFinancePolicy</c> (ops-finance + ops-admin).
/// </para>
///
/// <para>
/// Problem+json type URIs:
/// <list type="bullet">
///   <item>/errors/reconciliation-item-not-found (404)</item>
///   <item>/errors/reconciliation-already-resolved (409)</item>
///   <item>/errors/missing-actor (401)</item>
/// </list>
/// </para>
/// </summary>
[ApiController]
[Route("api/backoffice/reconciliation")]
[Authorize(Policy = "BackofficeReadPolicy", AuthenticationSchemes = "Backoffice")]
public sealed class ReconciliationController : ControllerBase
{
    private readonly PaymentDbContext _db;
    private readonly TimeProvider _clock;
    private readonly ILogger<ReconciliationController> _logger;

    public ReconciliationController(
        PaymentDbContext db,
        TimeProvider clock,
        ILogger<ReconciliationController> logger)
    {
        _db = db;
        _clock = clock;
        _logger = logger;
    }

    public sealed class ListQuery
    {
        /// <summary>Pending | Resolved (case-sensitive). Default Pending.</summary>
        public string? Status { get; set; }

        /// <summary>OrphanStripeEvent | OrphanWalletRow | AmountDrift | UnprocessedEvent.</summary>
        public string? DiscrepancyType { get; set; }

        /// <summary>Low | Medium | High.</summary>
        public string? Severity { get; set; }

        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }

    public sealed class ListResponse
    {
        public List<PaymentReconciliationItem> Rows { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }

    public sealed class ResolveRequest
    {
        [Required]
        [StringLength(2000, MinimumLength = 1)]
        public string Notes { get; set; } = string.Empty;
    }

    [HttpGet("")]
    public async Task<IActionResult> List(
        [FromQuery] ListQuery query,
        CancellationToken ct)
    {
        query ??= new ListQuery();
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > 500 ? 50 : query.PageSize;
        var statusFilter = string.IsNullOrWhiteSpace(query.Status) ? "Pending" : query.Status;

        var q = _db.ReconciliationQueue
            .AsNoTracking()
            .Where(r => r.Status == statusFilter);

        if (!string.IsNullOrWhiteSpace(query.DiscrepancyType))
            q = q.Where(r => r.DiscrepancyType == query.DiscrepancyType);
        if (!string.IsNullOrWhiteSpace(query.Severity))
            q = q.Where(r => r.Severity == query.Severity);

        var total = await q.CountAsync(ct);
        var rows = await q
            .OrderByDescending(r => r.DetectedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return Ok(new ListResponse
        {
            Rows = rows,
            TotalCount = total,
            Page = page,
            PageSize = pageSize,
        });
    }

    [HttpPost("{id:guid}/resolve")]
    [Authorize(Policy = "BackofficeFinancePolicy", AuthenticationSchemes = "Backoffice")]
    public async Task<IActionResult> Resolve(
        Guid id,
        [FromBody] ResolveRequest body,
        CancellationToken ct)
    {
        var actor = User.FindFirst("preferred_username")?.Value;
        if (string.IsNullOrEmpty(actor))
        {
            return Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                type: "/errors/missing-actor",
                title: "missing_actor",
                detail: "missing preferred_username claim");
        }

        if (body is null || string.IsNullOrWhiteSpace(body.Notes))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                type: "/errors/reconciliation-invalid-notes",
                title: "reconciliation_invalid_notes",
                detail: "ResolutionNotes required (1-2000 chars)");
        }

        var row = await _db.ReconciliationQueue.SingleOrDefaultAsync(r => r.Id == id, ct);
        if (row is null)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                type: "/errors/reconciliation-item-not-found",
                title: "reconciliation_item_not_found",
                detail: $"no reconciliation item with id {id}");
        }

        if (!string.Equals(row.Status, "Pending", StringComparison.Ordinal))
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                type: "/errors/reconciliation-already-resolved",
                title: "reconciliation_already_resolved",
                detail: $"item is already {row.Status}");
        }

        row.Status = "Resolved";
        row.ResolvedBy = actor;
        row.ResolvedAtUtc = _clock.GetUtcNow().UtcDateTime;
        row.ResolutionNotes = body.Notes;

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "reconciliation-resolved {ItemId} by={Actor} type={Type}",
            row.Id, actor, row.DiscrepancyType);

        return NoContent();
    }
}
