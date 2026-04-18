using FluentAssertions;
using TBE.BookingService.Application.Saga;
using TBE.BookingService.Infrastructure.Pdf;
using UglyToad.PdfPig;
using Xunit;

namespace TBE.BookingService.Tests;

/// <summary>
/// Plan 04-01 Task 1 — QuestPdfBookingReceiptGenerator produces a non-empty
/// PDF with fare / YQ-YR / tax separation per FLTB-03 (D-15 / CONTEXT).
/// Uses PdfPig to extract the rendered text so assertions survive QuestPDF's
/// FlateDecode compression of the content streams.
///
/// <para>
/// <b>Collection("QuestPDF")</b> — QuestPDF's license registration runs in a
/// static ctor; parallel text extraction across receipt + agency-invoice
/// generators has produced sporadic PdfPig text-stream races on Windows
/// (Plan 05-04 Wave B). Grouping the two generator test classes in a single
/// xUnit collection forces sequential execution and removes the flake without
/// costing measurable runtime (both fixtures are sub-second).
/// </para>
/// </summary>
[Collection("QuestPDF")]
public class QuestPdfBookingReceiptGeneratorTests
{
    [Fact]
    public async Task Generate_produces_nonempty_PDF_bytes()
    {
        var gen = new QuestPdfBookingReceiptGenerator();

        var bytes = await gen.GenerateAsync(SampleState(), CancellationToken.None);

        bytes.Should().NotBeNull();
        bytes.Length.Should().BeGreaterThan(500,
            "a real PDF with header/content/footer is at least a few hundred bytes");

        // PDF magic bytes: %PDF-
        bytes[0].Should().Be(0x25);
        bytes[1].Should().Be(0x50);
        bytes[2].Should().Be(0x44);
        bytes[3].Should().Be(0x46);
        bytes[4].Should().Be(0x2D);
    }

    [Fact]
    public async Task Generate_includes_fare_yqyr_tax_breakdown()
    {
        var gen = new QuestPdfBookingReceiptGenerator();

        var bytes = await gen.GenerateAsync(SampleState(), CancellationToken.None);
        var text = ExtractText(bytes);

        // FLTB-03: base fare, YQ/YR surcharges and taxes are rendered as
        // separate lines (not merged into a single "total taxes" figure).
        text.Should().Contain("Base fare", "receipt must show base fare line");
        text.Should().Contain("YQ", "receipt must show YQ/YR surcharges line");
        text.Should().Contain("Taxes", "receipt must show taxes line");
        text.Should().Contain("Total", "receipt must show total line");
        text.Should().Contain("GBP", "receipt must render the currency code from saga state");
    }

    [Fact]
    public async Task Generate_includes_PNR_and_ticket_number()
    {
        var gen = new QuestPdfBookingReceiptGenerator();

        var bytes = await gen.GenerateAsync(SampleState(), CancellationToken.None);
        var text = ExtractText(bytes);

        text.Should().Contain("PNR123", "receipt must include the GDS PNR");
        text.Should().Contain("125-1234567890", "receipt must include the airline ticket number");
        text.Should().Contain("TBE-260416-ABCDEF01",
            "receipt header must include the booking reference");
    }

    // ---- helpers ---------------------------------------------------------

    private static BookingSagaState SampleState() => new()
    {
        CorrelationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
        UserId = "user-owner-abc",
        BookingReference = "TBE-260416-ABCDEF01",
        ProductType = "flight",
        ChannelText = "b2c",
        Currency = "GBP",
        PaymentMethod = "card",
        TotalAmount = 150m,
        BaseFareAmount = 100m,
        SurchargeAmount = 30m,
        TaxAmount = 20m,
        GdsPnr = "PNR123",
        TicketNumber = "125-1234567890",
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
