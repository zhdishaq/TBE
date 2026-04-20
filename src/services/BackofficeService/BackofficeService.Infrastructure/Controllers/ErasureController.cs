using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TBE.BackofficeService.Infrastructure;
using TBE.Contracts.Events;

// Namespace kept as Application.Controllers per 06-01-PLAN Task 4 manifest
// (DlqController / StaffBookingActionsController precedent). Physical
// project is Infrastructure because the controller depends on
// BackofficeDbContext + IPublishEndpoint. The API project auto-discovers
// it via ApplicationPart scan of referenced assemblies.
namespace TBE.BackofficeService.Application.Controllers;

/// <summary>
/// Plan 06-04 Task 3 / COMP-03 / D-57 — GDPR "right to erasure" entry point.
/// ops-admin posts a typed-email confirmation; the controller performs three
/// checks then publishes <see cref="CustomerErasureRequested"/> via the EF
/// outbox for the CRM + Booking consumers to fan out:
///
/// <list type="number">
///   <item>
///     <strong>Typed-email match (400)</strong> — the request body's
///     <c>typedEmail</c> MUST equal the customer's email exactly (trim +
///     invariant-case compare). Prevents accidental erasure of the wrong
///     customer by requiring the ops-admin to retype the value they see
///     on screen (UI-SPEC §Confirmation dialogs #10 typed-confirm).
///   </item>
///   <item>
///     <strong>Open-saga block (409)</strong> — if the customer has any
///     <see cref="TBE.BackofficeService.Application.Entities.BookingReadRow"/>
///     whose <c>CurrentState</c> is not the terminal Confirmed state (7),
///     return 409 so ops finish the booking lifecycle first. Prevents
///     NULLing customer PII mid-saga which would break the CreatePnr /
///     ticket-issue / wallet-reserve consumers that carry the saga.
///   </item>
///   <item>
///     <strong>Duplicate-tombstone block (409)</strong> — the same
///     email has already been erased. The controller reads
///     <c>crm.CustomerErasureTombstones</c> via <see cref="BackofficeDbContext.CustomerErasureTombstoneReadModel"/>
///     keyed on SHA-256 hex of the normalised email (trim +
///     <c>ToLowerInvariant</c>). "Same person returns" = 409.
///   </item>
/// </list>
///
/// <para>
/// On success: <c>IPublishEndpoint.Publish(CustomerErasureRequested, ct)</c>
/// through the EF outbox, then <c>SaveChangesAsync</c> — the publish and
/// any (hypothetical future) local writes commit together. The controller
/// itself does not mutate any row; all state changes happen asynchronously
/// in the <c>CustomerErasureRequestedConsumer</c> fan-out (CrmService +
/// BookingService). Returns 202 Accepted with the generated request id.
/// </para>
///
/// <para>
/// <strong>Pitfall 4 (scheme pin)</strong> is already applied in Program.cs
/// via <c>AddAuthenticationSchemes("Backoffice")</c> on every policy, so
/// <c>[Authorize(Policy="BackofficeAdminPolicy")]</c> below inherits it
/// automatically. ops-admin is the only role allowed here — ops-cs /
/// ops-finance / ops-read all get 403 (BackofficeAdminPolicy =
/// <c>RequireRole("ops-admin")</c>).
/// </para>
///
/// <para>
/// <strong>Pitfall 28 (fail-closed actor extraction)</strong>: reads the
/// <c>preferred_username</c> claim for <c>RequestedBy</c> and returns 401
/// problem+json when it is missing. Never falls back to "system" or the
/// subject claim.
/// </para>
///
/// <para>
/// RFC-7807 problem+json with stable <c>/errors/...</c> type URIs per
/// PATTERNS.md Pattern G:
/// <list type="bullet">
///   <item><c>/errors/missing-actor</c> (401) — missing preferred_username.</item>
///   <item><c>/errors/customer-not-found</c> (404) — no CRM row.</item>
///   <item><c>/errors/customer-already-erased-internal</c> (409) —
///         the customer row itself is already flagged <c>IsErased</c>.</item>
///   <item><c>/errors/customer-erasure-typed-email-mismatch</c> (400).</item>
///   <item><c>/errors/customer-erasure-blocked-open-saga</c> (409).</item>
///   <item><c>/errors/customer-already-erased</c> (409) — tombstone exists
///         for the same email hash (same-person-returns dedup).</item>
/// </list>
/// </para>
/// </summary>
[ApiController]
[Route("api/backoffice/customers")]
[Authorize(Policy = "BackofficeAdminPolicy")]
public sealed class ErasureController : ControllerBase
{
    /// <summary>
    /// MassTransit saga state int for <c>Confirmed</c>. Mirrors
    /// <see cref="ManualBookingCommand.ConfirmedStateCode"/> in
    /// BookingService; we inline the int here to avoid taking a project
    /// reference on BookingService.Infrastructure from the backoffice
    /// assembly (the backoffice reads Saga.BookingSagaState via the
    /// cross-schema <see cref="BookingReadRow"/> and is otherwise
    /// decoupled). Any saga row with a different CurrentState is
    /// considered "in progress" for erasure purposes.
    /// </summary>
    private const int ConfirmedStateCode = 7;

    private readonly BackofficeDbContext _db;
    private readonly IPublishEndpoint _publish;
    private readonly ILogger<ErasureController> _logger;

    public ErasureController(
        BackofficeDbContext db,
        IPublishEndpoint publish,
        ILogger<ErasureController> logger)
    {
        _db = db;
        _publish = publish;
        _logger = logger;
    }

    public sealed class EraseCustomerReq
    {
        [Required]
        [StringLength(500, MinimumLength = 1)]
        public string Reason { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [StringLength(320, MinimumLength = 3)]
        public string TypedEmail { get; set; } = string.Empty;
    }

    /// <summary>
    /// Hashes the normalised email with SHA-256 and returns lowercase hex.
    /// Must stay byte-identical with
    /// <c>TBE.CrmService.Infrastructure.Consumers.CustomerErasureRequestedConsumer</c>
    /// and the <c>Sha256Hex</c> helper in <c>GdprErasureTests</c> — the
    /// tombstone key on both sides is computed the same way.
    /// </summary>
    private static string Sha256Hex(string normalised)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(normalised));
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }

    /// <summary>
    /// POST /api/backoffice/customers/{customerId:guid}/erase
    /// </summary>
    [HttpPost("{customerId:guid}/erase")]
    public async Task<IActionResult> Erase(
        Guid customerId,
        [FromBody] EraseCustomerReq body,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        // ---- Actor ----
        var actor = User.FindFirst("preferred_username")?.Value;
        if (string.IsNullOrEmpty(actor))
            return Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                type: "/errors/missing-actor",
                title: "missing_actor",
                detail: "missing preferred_username claim");

        // ---- Customer lookup (cross-schema crm.Customers) ----
        var customer = await _db.CustomerReadModel
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == customerId, ct);
        if (customer is null)
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                type: "/errors/customer-not-found",
                title: "customer_not_found",
                detail: $"no CRM projection for customer {customerId}");

        // Defensive: the CRM row is already anonymised. Surface as 409
        // so the portal can render "already erased" instead of silently
        // publishing a redundant erasure request.
        if (customer.IsErased || customer.Email is null)
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                type: "/errors/customer-already-erased-internal",
                title: "customer_already_erased",
                detail: $"customer {customerId} is already anonymised");

        // ---- Typed-email match ----
        var typed = body.TypedEmail?.Trim() ?? string.Empty;
        var stored = customer.Email.Trim();
        if (!string.Equals(typed, stored, StringComparison.OrdinalIgnoreCase))
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                type: "/errors/customer-erasure-typed-email-mismatch",
                title: "customer_erasure_typed_email_mismatch",
                detail: "typed email does not match customer email");

        // ---- Open-saga block ----
        // Any non-terminal saga row wearing the customer's id is a blocker.
        // Filtered index IX_BookingSagaState_CustomerId (Plan 06-04 Task 3
        // migration) backs this lookup.
        var openSagaBookingId = await _db.BookingReadModel
            .AsNoTracking()
            .Where(b => b.CurrentState != ConfirmedStateCode)
            .Where(b => b.CustomerEmail == stored)
            .Select(b => (Guid?)b.CorrelationId)
            .FirstOrDefaultAsync(ct);
        if (openSagaBookingId is Guid openBid)
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                type: "/errors/customer-erasure-blocked-open-saga",
                title: "customer_erasure_blocked_open_saga",
                detail: $"customer has an open saga (bookingId={openBid}); resolve before erasing");

        // ---- Duplicate-tombstone check ----
        // Normalise the same way the CRM consumer does — trim then
        // ToLowerInvariant — so the hash is stable regardless of
        // display-time casing. D-57: one tombstone per email; repeat
        // requests resolve to 409 with the existing tombstone.
        var emailHash = Sha256Hex(stored.ToLowerInvariant());
        var existingTombstone = await _db.CustomerErasureTombstoneReadModel
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.EmailHash == emailHash, ct);
        if (existingTombstone is not null)
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                type: "/errors/customer-already-erased",
                title: "customer_already_erased",
                detail: $"email already erased on {existingTombstone.ErasedAt:yyyy-MM-dd} by {existingTombstone.ErasedBy}");

        // ---- Publish via EF outbox ----
        var requestId = Guid.NewGuid();
        var at = DateTime.UtcNow;
        await _publish.Publish(new CustomerErasureRequested(
            RequestId: requestId,
            CustomerId: customerId,
            EmailHash: emailHash,
            RequestedBy: actor,
            Reason: body.Reason,
            At: at), ct);

        // SaveChangesAsync commits the outbox message atomically with any
        // local writes. The controller itself does not mutate any row;
        // fan-out happens in CrmService + BookingService consumers.
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "erasure requested customer={CustomerId} hash={EmailHash} request={RequestId} by={Actor} reason={Reason}",
            customerId, emailHash, requestId, actor, body.Reason);

        return Accepted(new { requestId, emailHash });
    }
}
