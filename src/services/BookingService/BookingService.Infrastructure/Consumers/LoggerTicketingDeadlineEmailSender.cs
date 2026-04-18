using Microsoft.Extensions.Logging;
using TBE.BookingService.Application.Consumers;
using TBE.BookingService.Application.Keycloak;

namespace TBE.BookingService.Infrastructure.Consumers;

/// <summary>
/// Plan 05-04 Task 1 (B2B-09) — log-only MVP implementation of
/// <see cref="ITicketingDeadlineEmailSender"/>. Mirrors Plan 05-03's
/// <c>WalletLowBalanceEmailSender</c> stub pattern: emit a structured log
/// line per recipient, return. The SendGrid transport sits behind the
/// NotificationService queue and will be wired by a follow-up plan once
/// the cross-service template contract is approved.
///
/// <para>
/// Operators grep for the stub marker
/// <c>"ticketing-deadline advisory (stub)"</c> to surface send events
/// until the real template ships.
/// </para>
/// </summary>
public sealed class LoggerTicketingDeadlineEmailSender : ITicketingDeadlineEmailSender
{
    private readonly ILogger<LoggerTicketingDeadlineEmailSender> _log;

    public LoggerTicketingDeadlineEmailSender(ILogger<LoggerTicketingDeadlineEmailSender> log)
    {
        _log = log;
    }

    public Task SendDeadlineEmailAsync(
        IReadOnlyList<AgentContact> recipients,
        TicketingDeadlineHorizon horizon,
        Guid bookingId,
        Guid agencyId,
        string pnr,
        DateTime ticketingTimeLimit,
        decimal hoursRemaining,
        string? clientName,
        CancellationToken ct)
    {
        foreach (var r in recipients)
        {
            _log.LogInformation(
                "ticketing-deadline advisory (stub) horizon={Horizon} to={Email} booking={BookingId} agency={AgencyId} pnr={Pnr} deadline={Deadline} hoursRemaining={Hours} client={Client}",
                horizon, r.Email, bookingId, agencyId, pnr, ticketingTimeLimit, hoursRemaining, clientName ?? "-");
        }
        return Task.CompletedTask;
    }
}
