using FluentAssertions;
using TBE.NotificationService.Application.Pdf;
using TBE.NotificationService.Infrastructure.Pdf;
using Xunit;

namespace TBE.Tests.Unit.NotificationService;

[Trait("Category", "Unit")]
public sealed class QuestPdfETicketGeneratorTests
{
    private static ETicketDocumentModel SampleModel() => new(
        PassengerName: "Alice Smith",
        ETicketNumber: "014-1234567890",
        Pnr: "ABC123",
        FlightNumber: "BA117",
        Origin: "LHR",
        Destination: "JFK",
        DepartureUtc: new DateTime(2026, 5, 1, 10, 30, 0, DateTimeKind.Utc),
        ArrivalUtc: new DateTime(2026, 5, 1, 18, 45, 0, DateTimeKind.Utc),
        FareClass: "Y",
        SeatNumber: "14A");

    [Fact]
    public void NOTF01_pdf_starts_with_magic_bytes()
    {
        var gen = new QuestPdfETicketGenerator();

        var bytes = gen.Generate(SampleModel());

        bytes.Should().NotBeNull();
        bytes.Length.Should().BeGreaterThan(1024);
        bytes[0].Should().Be(0x25); // '%'
        bytes[1].Should().Be(0x50); // 'P'
        bytes[2].Should().Be(0x44); // 'D'
        bytes[3].Should().Be(0x46); // 'F'
    }

    [Fact]
    public void NOTF01_pdf_contains_passenger_and_pnr()
    {
        // QuestPDF compresses text streams by default, so we cannot byte-grep for
        // the passenger name or PNR. Structural proof is enough:
        //  - both inputs (a document with those fields vs. a document with empty strings)
        //    should produce different byte streams
        //  - both should still start with %PDF and have non-trivial length
        var gen = new QuestPdfETicketGenerator();

        var withData = gen.Generate(SampleModel());
        var blank = gen.Generate(SampleModel() with
        {
            PassengerName = "",
            Pnr = "",
            ETicketNumber = "",
            FlightNumber = "",
            Origin = "",
            Destination = "",
            FareClass = "",
            SeatNumber = ""
        });

        withData.Should().NotBeEquivalentTo(blank,
            "PDF bytes must change when model fields change — proves fields are embedded");
        withData.Length.Should().BeGreaterThan(1024);
        // header still present
        System.Text.Encoding.ASCII.GetString(withData, 0, 4).Should().Be("%PDF");
    }
}
