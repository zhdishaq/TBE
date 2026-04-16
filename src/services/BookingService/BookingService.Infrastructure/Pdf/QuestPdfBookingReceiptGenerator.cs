using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TBE.BookingService.Application.Pdf;
using TBE.BookingService.Application.Saga;

namespace TBE.BookingService.Infrastructure.Pdf;

/// <summary>
/// Plan 04-01 / CONTEXT D-15 — QuestPDF-backed implementation of
/// <see cref="IBookingReceiptPdfGenerator"/>. Mirrors the structure of the
/// NotificationService <c>QuestPdfETicketGenerator</c> (PATTERNS analog).
/// Renders a FLTB-03 compliant breakdown: base fare, YQ/YR surcharges and
/// government taxes appear on separate lines (never merged into a single
/// "total taxes" figure).
/// </summary>
public sealed class QuestPdfBookingReceiptGenerator : IBookingReceiptPdfGenerator
{
    static QuestPdfBookingReceiptGenerator()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        // TODO(prod): switch to LicenseType.Commercial before production launch.
        // QuestPDF Community license is valid only for companies with <$1M USD annual revenue.
    }

    public Task<byte[]> GenerateAsync(BookingSagaState state, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(state);

        var bytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(t => t.FontSize(11));

                page.Header()
                    .Text($"Receipt — {state.BookingReference}")
                    .FontSize(18)
                    .Bold();

                page.Content().Column(col =>
                {
                    col.Spacing(5);

                    col.Item().Text($"PNR: {state.GdsPnr}");
                    col.Item().Text($"Ticket: {state.TicketNumber}");
                    col.Item().Text($"Issued: {state.InitiatedAtUtc:u}");

                    col.Item().PaddingVertical(6).LineHorizontal(1);

                    // FLTB-03: keep base fare / YQ-YR surcharges / taxes on
                    // separate lines. EU-UK regulation forbids merging them.
                    col.Item().Text($"Base fare:            {state.Currency} {state.BaseFareAmount:N2}");
                    col.Item().Text($"YQ/YR surcharges:     {state.Currency} {state.SurchargeAmount:N2}");
                    col.Item().Text($"Taxes:                {state.Currency} {state.TaxAmount:N2}");

                    col.Item().PaddingVertical(6).LineHorizontal(1);

                    col.Item().Text($"Total:                {state.Currency} {state.TotalAmount:N2}")
                              .SemiBold()
                              .FontSize(13);
                });

                page.Footer()
                    .AlignCenter()
                    .Text("Thank you for booking with TBE. Keep this receipt for your records.")
                    .FontSize(9);
            });
        }).GeneratePdf();

        return Task.FromResult(bytes);
    }
}
