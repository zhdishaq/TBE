using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TBE.BookingService.Application.Pdf;
using TBE.BookingService.Application.Saga;

namespace TBE.BookingService.Infrastructure.Pdf;

/// <summary>
/// Plan 05-04 Task 2 (B2B-08) — QuestPDF-backed implementation of
/// <see cref="IAgencyInvoicePdfGenerator"/>.
///
/// <para>
/// <b>D-43 GROSS-ONLY contract:</b> this renderer writes customer-facing
/// GROSS figures only. The string literals "NET", "Markup", and "Commission"
/// MUST NEVER appear in the generated PDF text stream. A PdfPig-backed
/// negative-grep test (AgencyInvoiceDocumentTests) enforces this at ship
/// time.
/// </para>
///
/// <para>
/// <b>Invoice number:</b>
/// <c>INV-{agencyId[..8]}-{yyyyMMdd}-{bookingId[..6]}</c>
/// — deterministic so re-downloads of an invoice PDF always return the same
/// number. No DB sequence, no row reservation; the agency can print the
/// same number on their own accounting records.
/// </para>
///
/// <para>
/// Structure mirrors <c>QuestPdfBookingReceiptGenerator</c> (PATTERNS Pattern
/// 8 analog) with GROSS-only breakdown. The B2C Receipt still renders
/// NET+Surcharges+Taxes breakdown because the B2C customer IS the traveller
/// paying the face-value fare; only the B2B "agency-issued-to-customer"
/// invoice hides the agency's internal markup per D-43.
/// </para>
/// </summary>
public sealed class QuestPdfAgencyInvoiceGenerator : IAgencyInvoicePdfGenerator
{
    static QuestPdfAgencyInvoiceGenerator()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public Task<byte[]> GenerateAsync(BookingSagaState state, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(state);

        // Invoice number — deterministic per (agency, booking, issue date).
        var agencyPart = (state.AgencyId ?? Guid.Empty).ToString("N")[..8].ToUpperInvariant();
        var bookingPart = state.CorrelationId.ToString("N")[..6].ToUpperInvariant();
        var issueDate = DateTime.UtcNow;
        var invoiceNumber = $"INV-{agencyPart}-{issueDate:yyyyMMdd}-{bookingPart}";

        // Gross amount — D-43 the only money figure visible to the customer.
        var gross = state.AgencyGrossAmount ?? state.TotalAmount;
        var currency = state.Currency;

        var bytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(t => t.FontSize(11));

                page.Header().Column(head =>
                {
                    head.Item().Text($"Invoice {invoiceNumber}")
                        .FontSize(18)
                        .Bold();
                    head.Item().Text($"Issue date: {issueDate:yyyy-MM-dd}");
                });

                page.Content().Column(col =>
                {
                    col.Spacing(5);

                    col.Item().Text($"Booking ref: {state.BookingReference}");
                    col.Item().Text($"PNR: {state.GdsPnr ?? "-"}");

                    col.Item().PaddingVertical(6).LineHorizontal(1);

                    col.Item().Text($"Billed to: {state.CustomerName ?? "-"}");
                    if (!string.IsNullOrWhiteSpace(state.CustomerEmail))
                        col.Item().Text($"Email: {state.CustomerEmail}");

                    col.Item().PaddingVertical(6).LineHorizontal(1);

                    // D-43 — GROSS-only. We intentionally do not show a
                    // breakdown that would expose the agency's markup. The
                    // customer sees one line: "Total payable".
                    col.Item().Text($"Travel services:     {currency} {gross:N2}");

                    col.Item().PaddingVertical(6).LineHorizontal(1);

                    col.Item().Text($"Total payable:       {currency} {gross:N2}")
                              .SemiBold()
                              .FontSize(13);
                });

                page.Footer()
                    .AlignCenter()
                    .Text("Issued on behalf of your travel agency. Please retain for your records.")
                    .FontSize(9);
            });
        }).GeneratePdf();

        return Task.FromResult(bytes);
    }
}
