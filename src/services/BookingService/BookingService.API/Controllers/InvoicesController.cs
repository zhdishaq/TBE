using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TBE.BookingService.Application.Pdf;
using TBE.BookingService.Infrastructure;

namespace TBE.BookingService.API.Controllers;

/// <summary>
/// Plan 05-04 Task 2 (B2B-08) — agency-invoice PDF endpoint.
///
/// <para>
/// Route: <c>GET /api/invoices/{bookingId}.pdf</c>.
/// </para>
///
/// <para>
/// <b>D-43 GROSS-only:</b> the PDF renders only customer-facing figures — never
/// the agency-internal NET / Markup / Commission numbers. Enforced by
/// <c>AgencyInvoiceDocumentTests</c> which runs a PdfPig substring negative
/// assertion on the rendered byte stream.
/// </para>
///
/// <para>
/// <b>Pitfall 10 — IDOR 404 (never 403):</b> if the booking is either missing
/// OR belongs to a different <c>agency_id</c> claim, this endpoint returns
/// 404 NotFound. It MUST NOT return 403 — a 403 would leak the existence of
/// the booking to a cross-tenant caller. Enforced by
/// <c>AgencyInvoiceControllerTests.GetInvoice_returns_404_*</c>.
/// </para>
///
/// <para>
/// <b>Pitfall 28 — missing claim fail-closed 401:</b> if the JWT lacks the
/// <c>agency_id</c> claim, the caller gets 401 Unauthorized. A missing claim
/// is NEVER interpreted as "default to first agency" or similar fall-back.
/// </para>
/// </summary>
[ApiController]
[Route("api/invoices")]
[Authorize(Policy = "B2BPolicy")]
public sealed class InvoicesController(
    BookingDbContext db,
    IAgencyInvoicePdfGenerator pdfGen,
    ILogger<InvoicesController> logger) : ControllerBase
{
    [HttpGet("{bookingId:guid}.pdf")]
    public async Task<IActionResult> GetInvoiceAsync(Guid bookingId, CancellationToken ct)
    {
        // Pitfall 28 — fail-closed when agency_id claim is missing. Fail with
        // 401 Unauthorized so the caller can't mistakenly interpret the
        // response as "booking doesn't exist for you" (vs "you're not authed
        // at all").
        var agencyIdClaim = User.FindFirst("agency_id")?.Value;
        if (string.IsNullOrWhiteSpace(agencyIdClaim) || !Guid.TryParse(agencyIdClaim, out var agencyId))
            return Unauthorized(new { error = "missing agency_id claim" });

        var booking = await db.BookingSagaStates
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.CorrelationId == bookingId, ct);

        // Pitfall 10 — 404 covers BOTH "booking does not exist" AND
        // "booking belongs to a different agency". NEVER 403.
        if (booking is null || booking.AgencyId != agencyId)
        {
            logger.LogInformation(
                "Invoice request denied (404) booking={BookingId} caller_agency={CallerAgency} owner_agency={OwnerAgency}",
                bookingId, agencyId, booking?.AgencyId);
            return NotFound();
        }

        var bytes = await pdfGen.GenerateAsync(booking, ct);

        // Content-Disposition: inline so the browser renders directly; the
        // filename drives File → Save-As.
        return File(bytes, "application/pdf", $"invoice-{booking.BookingReference}.pdf");
    }
}
