namespace TBE.NotificationService.API.Templates.Models;

public sealed record FlightCancellationModel(
    string PassengerName,
    string Pnr,
    string Reason,
    decimal RefundAmount,
    string Currency);
