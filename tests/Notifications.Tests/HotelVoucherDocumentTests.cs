using FluentAssertions;
using QuestPDF.Infrastructure;
using TBE.NotificationService.API.Templates.Models;
using TBE.NotificationService.Infrastructure.Pdf;
using Xunit;

namespace Notifications.Tests;

/// <summary>
/// NOTF-02 PDF generation contract tests. Replaces the Wave 0 red placeholders
/// authored in Plan 04-00 Task 3. QuestPDF Community license is asserted so the
/// TODO(prod) switch is tracked.
/// </summary>
[Trait("Category", "Unit")]
public sealed class HotelVoucherDocumentTests
{
    private static HotelVoucherModel SampleModel() => new(
        BookingReference: "HB-0001",
        SupplierRef: "SUP-ABC-123",
        PropertyName: "The Grand Sample",
        AddressLine: "1 Example Street, London",
        CheckInDate: new DateOnly(2026, 5, 1),
        CheckOutDate: new DateOnly(2026, 5, 4),
        Rooms: 1,
        Adults: 2,
        Children: 0,
        TotalAmount: 456.00m,
        Currency: "GBP",
        GuestEmail: "alice@example.com",
        GuestFullName: "Alice Example",
        BrandName: "TBE Travel",
        SupportPhone: "+44 20 0000 0000");

    [Fact]
    public void Generate_produces_nonempty_PDF()
    {
        var sut = new HotelVoucherDocument();

        var bytes = sut.Generate(SampleModel());

        bytes.Should().NotBeNull();
        bytes.Length.Should().BeGreaterThan(2048, "voucher PDFs carry property + stay + supplier sections");

        // "%PDF" header — bytes 0..3 MUST be 0x25 0x50 0x44 0x46
        bytes[0].Should().Be(0x25);
        bytes[1].Should().Be(0x50);
        bytes[2].Should().Be(0x44);
        bytes[3].Should().Be(0x46);
    }

    [Fact]
    public void Generate_uses_Community_license()
    {
        // Trigger the static constructor if the JIT hasn't already.
        _ = new HotelVoucherDocument();

        QuestPDF.Settings.License.Should().Be(LicenseType.Community);
    }
}
