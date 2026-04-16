using TBE.BookingService.Application.Saga;

namespace TBE.BookingService.Application.Pdf;

/// <summary>
/// Plan 04-01 / CONTEXT D-15 — server-side QuestPDF receipt backbone for
/// <c>GET /bookings/{id}/receipt.pdf</c>. Implementations MUST render the
/// fare breakdown required by FLTB-03 (base fare, YQ/YR surcharges and
/// taxes as separate line items) so the PDF is legally compliant in
/// EU/UK markets.
/// </summary>
public interface IBookingReceiptPdfGenerator
{
    /// <summary>Render a receipt PDF for the given persisted saga state.</summary>
    /// <param name="state">The booking saga state — treated as the single source of truth.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A byte array containing the PDF — not a file path.</returns>
    Task<byte[]> GenerateAsync(BookingSagaState state, CancellationToken ct);
}
