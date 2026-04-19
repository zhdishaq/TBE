using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Nodes;
using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TBE.BackofficeService.Application.Entities;
using TBE.BackofficeService.Infrastructure;

// Namespace kept as Application.Controllers per 06-01-PLAN Task 4
// manifest. Physical project is Infrastructure because the controller
// depends on BackofficeDbContext + ISendEndpointProvider. The API
// project auto-discovers it via ApplicationPart scan of referenced
// assemblies.
namespace TBE.BackofficeService.Application.Controllers;

/// <summary>
/// Plan 06-01 Task 4 — BO-09 (list) + BO-10 (requeue/resolve) DLQ surface.
///
/// Role gates (Pitfall 4 — scheme pin already applied in Program.cs):
///   - GET   → BackofficeReadPolicy (ops-read/cs/finance/admin)
///   - POST /requeue, /resolve → BackofficeAdminPolicy (ops-admin only)
///
/// Fail-closed actor extraction (Pitfall 28): every mutation reads the
/// <c>preferred_username</c> claim and returns 401 problem+json when it
/// is missing. Never fall back to "system" — an unauthenticated mutation
/// with no audit actor is worse than no mutation.
///
/// Requeue preserves MessageId + CorrelationId + headers via
/// <c>PublishContextCallback</c> so downstream consumer idempotency keys
/// continue to deduplicate (RESEARCH anti-pattern 3).
/// </summary>
[ApiController]
[Route("api/backoffice/dlq")]
[Authorize(Policy = "BackofficeReadPolicy")]
public sealed class DlqController : ControllerBase
{
    private readonly BackofficeDbContext _db;
    private readonly ISendEndpointProvider _sendEndpointProvider;
    private readonly ILogger<DlqController> _logger;

    public DlqController(
        BackofficeDbContext db,
        ISendEndpointProvider sendEndpointProvider,
        ILogger<DlqController> logger)
    {
        _db = db;
        _sendEndpointProvider = sendEndpointProvider;
        _logger = logger;
    }

    public sealed record DlqListQuery(
        string? Status = "unresolved",
        DateTime? From = null,
        DateTime? To = null,
        int Page = 1,
        int PageSize = 20);

    public sealed record DlqListRow(
        Guid Id,
        Guid MessageId,
        Guid? CorrelationId,
        string MessageType,
        string OriginalQueue,
        string FailureReason,
        string Preview,
        DateTime FirstFailedAt,
        DateTime? LastRequeuedAt,
        int RequeueCount,
        DateTime? ResolvedAt,
        string? ResolvedBy);

    public sealed record DlqListResponse(
        IReadOnlyList<DlqListRow> Rows,
        int TotalCount,
        int Page,
        int PageSize);

    public sealed record DlqDetailResponse(
        Guid Id,
        Guid MessageId,
        Guid? CorrelationId,
        string MessageType,
        string OriginalQueue,
        string Payload,
        string FailureReason,
        DateTime FirstFailedAt,
        DateTime? LastRequeuedAt,
        int RequeueCount,
        DateTime? ResolvedAt,
        string? ResolvedBy,
        string? ResolutionReason);

    public sealed class DlqResolveRequest
    {
        [Required]
        [StringLength(500, MinimumLength = 1)]
        public string Reason { get; set; } = string.Empty;
    }

    [HttpGet]
    public async Task<ActionResult<DlqListResponse>> List(
        [FromQuery] DlqListQuery query,
        CancellationToken ct)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        IQueryable<DeadLetterQueueRow> q = _db.DeadLetterQueue;

        switch ((query.Status ?? "unresolved").ToLowerInvariant())
        {
            case "resolved":
                q = q.Where(r => r.ResolvedAt != null);
                break;
            case "all":
                break;
            case "unresolved":
            default:
                q = q.Where(r => r.ResolvedAt == null);
                break;
        }

        if (query.From is { } from)
            q = q.Where(r => r.FirstFailedAt >= from);
        if (query.To is { } to)
            q = q.Where(r => r.FirstFailedAt <= to);

        var total = await q.CountAsync(ct);

        var rows = await q
            .OrderByDescending(r => r.FirstFailedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new DlqListRow(
                r.Id,
                r.MessageId,
                r.CorrelationId,
                r.MessageType,
                r.OriginalQueue,
                r.FailureReason,
                r.FailureReason.Length > 80
                    ? r.FailureReason.Substring(0, 80)
                    : r.FailureReason,
                r.FirstFailedAt,
                r.LastRequeuedAt,
                r.RequeueCount,
                r.ResolvedAt,
                r.ResolvedBy))
            .ToListAsync(ct);

        return Ok(new DlqListResponse(rows, total, page, pageSize));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DlqDetailResponse>> Get(Guid id, CancellationToken ct)
    {
        var row = await _db.DeadLetterQueue.SingleOrDefaultAsync(r => r.Id == id, ct);
        if (row is null) return NotFound();
        return Ok(new DlqDetailResponse(
            row.Id,
            row.MessageId,
            row.CorrelationId,
            row.MessageType,
            row.OriginalQueue,
            row.Payload,
            row.FailureReason,
            row.FirstFailedAt,
            row.LastRequeuedAt,
            row.RequeueCount,
            row.ResolvedAt,
            row.ResolvedBy,
            row.ResolutionReason));
    }

    [HttpPost("{id:guid}/requeue")]
    [Authorize(Policy = "BackofficeAdminPolicy")]
    public async Task<IActionResult> Requeue(Guid id, CancellationToken ct)
    {
        var actor = User.FindFirst("preferred_username")?.Value;
        if (string.IsNullOrEmpty(actor))
            return Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "missing_actor",
                detail: "missing preferred_username claim");

        var row = await _db.DeadLetterQueue.SingleOrDefaultAsync(r => r.Id == id, ct);
        if (row is null) return NotFound();
        if (row.ResolvedAt is not null)
            return Conflict(new { error = "row is already resolved" });

        try
        {
            // Deserialise payload back into a JsonObject so MassTransit can
            // re-publish with the original shape.
            var payload = JsonNode.Parse(row.Payload)?.AsObject() ?? new JsonObject();

            var endpoint = await _sendEndpointProvider.GetSendEndpoint(
                new Uri($"queue:{row.OriginalQueue}"));

            var preservedMessageId = row.MessageId;
            var preservedCorrelationId = row.CorrelationId;

            await endpoint.Send<JsonObject>(payload, ctx =>
            {
                ctx.MessageId = preservedMessageId;
                if (preservedCorrelationId.HasValue)
                    ctx.CorrelationId = preservedCorrelationId;
                ctx.Headers.Set("MT-Requeued-By", actor);
                ctx.Headers.Set("MT-Requeued-At", DateTime.UtcNow.ToString("O"));
            }, ct);

            row.RequeueCount += 1;
            row.LastRequeuedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "dlq-requeue {Id} {OriginalQueue} by={Actor}",
                row.Id, row.OriginalQueue, actor);

            return NoContent();
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "dlq-requeue-parse-failed {Id}", id);
            return Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "payload_unparseable",
                detail: "stored payload is not a valid JSON object");
        }
    }

    [HttpPost("{id:guid}/resolve")]
    [Authorize(Policy = "BackofficeAdminPolicy")]
    public async Task<IActionResult> Resolve(
        Guid id,
        [FromBody] DlqResolveRequest body,
        CancellationToken ct)
    {
        var actor = User.FindFirst("preferred_username")?.Value;
        if (string.IsNullOrEmpty(actor))
            return Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "missing_actor",
                detail: "missing preferred_username claim");

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var row = await _db.DeadLetterQueue.SingleOrDefaultAsync(r => r.Id == id, ct);
        if (row is null) return NotFound();
        if (row.ResolvedAt is not null)
            return Conflict(new { error = "row is already resolved" });

        row.ResolvedAt = DateTime.UtcNow;
        row.ResolvedBy = actor;
        row.ResolutionReason = body.Reason;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "dlq-resolve {Id} by={Actor} reason={Reason}",
            row.Id, actor, body.Reason);

        return NoContent();
    }
}
