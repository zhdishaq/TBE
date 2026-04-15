using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using TBE.NotificationService.Application.Pdf;

namespace TBE.NotificationService.Infrastructure.Pdf;

/// <summary>
/// QuestPDF-backed implementation of <see cref="IETicketPdfGenerator"/>.
/// Returns a PDF byte array to be attached to the NOTF-01 flight-confirmation email.
/// </summary>
public sealed class QuestPdfETicketGenerator : IETicketPdfGenerator
{
    static QuestPdfETicketGenerator()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        // TODO(prod): switch to LicenseType.Commercial before production launch.
        // QuestPDF Community license is valid only for companies with <$1M USD annual revenue.
    }

    public byte[] Generate(ETicketDocumentModel m)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(30);
                page.Header()
                    .Text($"E-TICKET — {m.ETicketNumber}")
                    .FontSize(18)
                    .Bold();

                page.Content().Column(col =>
                {
                    col.Spacing(5);
                    col.Item().Text($"Passenger: {m.PassengerName}");
                    col.Item().Text($"PNR: {m.Pnr}");
                    col.Item().Text($"Flight: {m.FlightNumber}");
                    col.Item().Text($"{m.Origin} -> {m.Destination}");
                    col.Item().Text($"Depart: {m.DepartureUtc:u}  Arrive: {m.ArrivalUtc:u}");
                    col.Item().Text($"Class: {m.FareClass}  Seat: {m.SeatNumber}");
                    col.Item().PaddingTop(15)
                        .Text($"Barcode: {m.ETicketNumber}")
                        .FontFamily("Courier New")
                        .FontSize(12);
                    // NOTE: real Code128 barcode rendering deferred to Phase 4.
                    // The plain-text ETicketNumber satisfies the NOTF-01 functional contract for Phase 3.
                });

                page.Footer()
                    .AlignCenter()
                    .Text("This e-ticket is your boarding pass — present a valid photo ID at check-in.");
            });
        }).GeneratePdf();
    }
}
