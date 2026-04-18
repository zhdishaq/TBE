using TBE.BookingService.Application.Saga;

namespace TBE.BookingService.Application.Pdf;

/// <summary>
/// Plan 05-04 Task 2 (B2B-08) — server-side QuestPDF backbone for
/// <c>GET /api/invoices/{bookingId}.pdf</c>.
///
/// <para>
/// <b>D-43 (GROSS-only):</b> implementations MUST render the
/// <see cref="BookingSagaState.AgencyGrossAmount"/> and the customer-facing
/// fare breakdown only. The document MUST NEVER contain the string "NET",
/// "Markup", or "Commission" — those are agency-internal fields that never
/// appear on a customer-facing invoice. Enforced by
/// <c>AgencyInvoiceDocumentTests</c> via a PdfPig-decompressed substring
/// negative assertion.
/// </para>
///
/// <para>
/// <b>Invoice number format:</b>
/// <c>INV-{agencyId[..8]}-{yyyyMMdd}-{bookingId[..6]}</c>
/// (uppercase hex, deterministic — no database sequence required).
/// </para>
/// </summary>
public interface IAgencyInvoicePdfGenerator
{
    /// <summary>Render the agency's customer-facing invoice PDF.</summary>
    Task<byte[]> GenerateAsync(BookingSagaState state, CancellationToken ct);
}
