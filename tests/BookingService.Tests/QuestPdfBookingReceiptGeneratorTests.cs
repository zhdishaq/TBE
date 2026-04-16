using System.Text;
using FluentAssertions;
using TBE.BookingService.Application.Saga;
using TBE.BookingService.Infrastructure.Pdf;
using Xunit;

namespace TBE.BookingService.Tests;

/// <summary>
/// Plan 04-01 Task 1 — QuestPdfBookingReceiptGenerator produces a non-empty
/// PDF with fare / YQ-YR / tax separation per FLTB-03 (D-15 / CONTEXT).
/// Uses lightweight byte-search assertions against the rendered PDF stream
/// — QuestPDF emits text as Win1252/ASCII by default so the reference
/// strings ("Base fare", "PNR", etc.) appear verbatim in the byte array.
/// </summary>
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

        // FLTB-03: base fare, YQ/YR surcharges and taxes are rendered as
        // separate lines (not merged into a single "total taxes" figure).
        Contains(bytes, "Base fare").Should().BeTrue("receipt must show base fare line");
        Contains(bytes, "YQ").Should().BeTrue("receipt must show YQ/YR surcharges line");
        Contains(bytes, "Taxes").Should().BeTrue("receipt must show taxes line");
        Contains(bytes, "Total").Should().BeTrue("receipt must show total line");
        Contains(bytes, "GBP").Should().BeTrue("receipt must render the currency code from saga state");
    }

    [Fact]
    public async Task Generate_includes_PNR_and_ticket_number()
    {
        var gen = new QuestPdfBookingReceiptGenerator();

        var bytes = await gen.GenerateAsync(SampleState(), CancellationToken.None);

        Contains(bytes, "PNR123").Should().BeTrue("receipt must include the GDS PNR");
        Contains(bytes, "125-1234567890").Should().BeTrue("receipt must include the airline ticket number");
        Contains(bytes, "TBE-260416-ABCDEF01").Should().BeTrue(
            "receipt header must include the booking reference");
    }

    // ---- helpers ---------------------------------------------------------

    private static BookingSagaState SampleState() => new()
    {
        CorrelationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
        UserId = "user-owner-abc",
        BookingReference = "TBE-260416-ABCDEF01",
        ProductType = "flight",
        Channel = "b2c",
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

    /// <summary>Naive substring search over ASCII-text segments of a PDF byte array.</summary>
    private static bool Contains(byte[] haystack, string needle)
    {
        var needleBytes = Encoding.ASCII.GetBytes(needle);
        for (var i = 0; i <= haystack.Length - needleBytes.Length; i++)
        {
            var match = true;
            for (var j = 0; j < needleBytes.Length; j++)
            {
                if (haystack[i + j] != needleBytes[j]) { match = false; break; }
            }
            if (match) return true;
        }
        return false;
    }
}
