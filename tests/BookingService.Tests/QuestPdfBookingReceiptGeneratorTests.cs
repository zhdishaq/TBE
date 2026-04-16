using Xunit;

namespace TBE.BookingService.Tests;

/// <summary>
/// RED placeholders authored in Wave 0 (Plan 04-00 Task 3). Plan 04-01
/// implements QuestPdfBookingReceiptGenerator (server-side PDF receipt
/// with fare/YQ-YR/tax breakdown per FLTB-03) and turns these green.
/// </summary>
public class QuestPdfBookingReceiptGeneratorTests
{
    [Fact]
    [Trait("Category", "RedPlaceholder")]
    public void Generate_produces_nonempty_PDF_bytes()
    {
        Assert.Fail("Red placeholder — implemented by Plan 04-01");
    }

    [Fact]
    [Trait("Category", "RedPlaceholder")]
    public void Generate_includes_fare_yqyr_tax_breakdown()
    {
        Assert.Fail("Red placeholder — implemented by Plan 04-01");
    }

    [Fact]
    [Trait("Category", "RedPlaceholder")]
    public void Generate_includes_PNR_and_ticket_number()
    {
        Assert.Fail("Red placeholder — implemented by Plan 04-01");
    }
}
