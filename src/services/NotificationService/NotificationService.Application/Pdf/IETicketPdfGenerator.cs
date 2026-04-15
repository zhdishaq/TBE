namespace TBE.NotificationService.Application.Pdf;

/// <summary>
/// Generates an e-ticket PDF byte array. NOTF-01 attachment backbone.
/// </summary>
public interface IETicketPdfGenerator
{
    byte[] Generate(ETicketDocumentModel model);
}

public sealed record ETicketDocumentModel(
    string PassengerName,
    string ETicketNumber,
    string Pnr,
    string FlightNumber,
    string Origin,
    string Destination,
    DateTime DepartureUtc,
    DateTime ArrivalUtc,
    string FareClass,
    string SeatNumber);
