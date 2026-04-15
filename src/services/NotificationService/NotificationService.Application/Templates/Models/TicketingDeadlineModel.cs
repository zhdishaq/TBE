namespace TBE.NotificationService.API.Templates.Models;

public sealed record TicketingDeadlineModel(
    string PassengerName,
    string Pnr,
    string Horizon,
    DateTime DeadlineUtc);
