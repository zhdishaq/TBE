namespace TBE.NotificationService.API.Templates.Models;

public sealed record FlightConfirmationModel(
    string PassengerName,
    string Pnr,
    string ETicketNumber,
    decimal Total,
    string Currency);
