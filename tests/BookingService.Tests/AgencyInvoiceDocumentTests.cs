using FluentAssertions;
using TBE.BookingService.Application.Saga;
using TBE.BookingService.Infrastructure.Pdf;
using UglyToad.PdfPig;
using Xunit;

namespace TBE.BookingService.Tests;

/// <summary>
/// Plan 05-04 Task 2 (B2B-08) — QuestPdfAgencyInvoiceGenerator D-43 GROSS-only
/// assertions.
///
/// <para>
/// <b>D-43 contract:</b> the agency-issued invoice PDF renders only customer-facing
/// figures — the string literals <c>"NET"</c>, <c>"Markup"</c>, and
/// <c>"Commission"</c> MUST NEVER appear in the PDF text stream. This test
/// decompresses the FlateDecode content streams via PdfPig and performs a
/// negative-substring assertion on the resulting text.
/// </para>
///
/// <para>
/// Replaces the RED placeholder in
/// <c>tests/Notifications.Tests/AgencyInvoiceDocumentTests.cs</c> — during Wave
/// B the invoice generator was pivoted into BookingService (where BookingSagaState
/// already holds every required field) instead of a new DocumentService, so the
/// real tests live alongside the code. See 05-04-SUMMARY.md Deviations.
/// </para>
///
/// <para>
/// <b>Collection("QuestPDF")</b> — shared with
/// <see cref="QuestPdfBookingReceiptGeneratorTests"/> to serialize access to
/// QuestPDF's static license state on Windows (sporadic PdfPig flake observed
/// during Plan 05-04 Wave B parallel runs).
/// </para>
/// </summary>
[Collection("QuestPDF")]
public class AgencyInvoiceDocumentTests
{
    /// <summary>D-43 — invoice renders the customer-facing GROSS figure and total only.</summary>
    [Fact]
    public async Task Invoice_renders_gross_total_only()
    {
        var gen = new QuestPdfAgencyInvoiceGenerator();

        var bytes = await gen.GenerateAsync(SampleState(grossAmount: 480.00m), CancellationToken.None);
        var text = ExtractText(bytes);

        // The GROSS amount must be visible (formatted N2 = "480.00").
        text.Should().Contain("480.00", "invoice must render the agency's GROSS amount");
        text.Should().Contain("Total payable", "invoice must render a customer-facing 'Total payable' line");
        text.Should().Contain("Travel services", "invoice must render a single 'Travel services' line per D-43");
        text.Should().Contain("GBP", "invoice must render the currency code from saga state");
    }

    /// <summary>
    /// D-43 — negative assertion: extracted PDF text must NOT contain the
    /// strings <c>NET</c>, <c>Markup</c>, or <c>Commission</c>. A visual
    /// regression that leaked agency-internal margin would be a trust
    /// incident, so this is the canonical ship-time gate.
    /// </summary>
    [Fact]
    public async Task Invoice_never_renders_NET_markup_or_commission_strings()
    {
        var gen = new QuestPdfAgencyInvoiceGenerator();

        var bytes = await gen.GenerateAsync(SampleState(grossAmount: 480.00m), CancellationToken.None);
        var text = ExtractText(bytes);

        text.Should().NotContain("NET",
            "D-43 — the agency's NET line MUST NEVER appear on the customer-facing invoice");
        text.Should().NotContain("Markup",
            "D-43 — the agency's markup MUST NEVER appear on the customer-facing invoice");
        text.Should().NotContain("Commission",
            "D-43 — agency commission MUST NEVER appear on the customer-facing invoice");
    }

    /// <summary>
    /// Shape check — the invoice number format is
    /// <c>INV-{agencyId[..8]}-{yyyyMMdd}-{bookingId[..6]}</c>. The renderer
    /// must be deterministic for a given (agency, booking, issue-date) triple
    /// so re-downloads return the same invoice number without a DB sequence.
    /// </summary>
    [Fact]
    public async Task Invoice_renders_invoice_number_with_deterministic_format()
    {
        var gen = new QuestPdfAgencyInvoiceGenerator();

        var bytes = await gen.GenerateAsync(SampleState(grossAmount: 480.00m), CancellationToken.None);
        var text = ExtractText(bytes);

        // agencyId 44444444-4444-... → "44444444" (first 8 hex chars upper).
        // bookingId 11111111-1111-... → "111111" (first 6 hex chars upper).
        text.Should().Contain("INV-44444444",
            "invoice number must prefix with agency-id first 8 hex characters");
        text.Should().Contain("-111111",
            "invoice number must suffix with booking-id first 6 hex characters");
    }

    // ---- helpers ---------------------------------------------------------

    private static BookingSagaState SampleState(decimal grossAmount) => new()
    {
        CorrelationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
        AgencyId = Guid.Parse("44444444-4444-4444-4444-444444444444"),
        UserId = "agent-bob",
        BookingReference = "TBE-260416-ABCDEF01",
        ProductType = "flight",
        ChannelText = "b2b",
        Currency = "GBP",
        PaymentMethod = "wallet",
        TotalAmount = grossAmount,
        AgencyGrossAmount = grossAmount,
        GdsPnr = "PNR777",
        TicketNumber = "125-9876543210",
        CustomerName = "Jane Doe",
        CustomerEmail = "jane@example.com",
        InitiatedAtUtc = DateTime.UtcNow,
        TicketingDeadlineUtc = DateTime.UtcNow.AddHours(24),
    };

    private static string ExtractText(byte[] bytes)
    {
        using var doc = PdfDocument.Open(bytes);
        var sb = new System.Text.StringBuilder();
        foreach (var page in doc.GetPages())
        {
            sb.AppendLine(page.Text);
        }
        return sb.ToString();
    }
}
