namespace TBE.NotificationService.API.Templates.Models;

public sealed record TicketIssuedModel(
    string PassengerName,
    string Pnr,
    string ETicketNumber,
    string FlightNumber);
