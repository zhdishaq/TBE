using TBE.BookingService.Application.Keycloak;

namespace TBE.BookingService.Application.Consumers;

/// <summary>
/// Plan 05-04 Task 1 (B2B-09) — minimal e-mail delivery port used by
/// <c>TicketingDeadlineConsumer</c>. Mirrors the shape of
/// <c>TBE.PaymentService.Application.Wallet.IWalletLowBalanceEmailSender</c>
/// from Plan 05-03: a dedicated interface keeps the consumer testable with
/// a single NSubstitute stub and avoids a cross-service project reference
/// on NotificationService just for a log-only MVP sender.
/// </summary>
public enum TicketingDeadlineHorizon
{
    /// <summary>24-hour advisory — amber "Heads-up" copy.</summary>
    Warning,
    /// <summary>2-hour urgent — red "URGENT:" copy.</summary>
    Urgent,
}

/// <summary>
/// Sender port. The Phase-5 MVP implementation just logs each send;
/// Phase 3 (NotificationService template) will replace it with a real
/// SendGrid transport.
/// </summary>
public interface ITicketingDeadlineEmailSender
{
    Task SendDeadlineEmailAsync(
        IReadOnlyList<AgentContact> recipients,
        TicketingDeadlineHorizon horizon,
        Guid bookingId,
        Guid agencyId,
        string pnr,
        DateTime ticketingTimeLimit,
        decimal hoursRemaining,
        string? clientName,
        CancellationToken ct);
}
