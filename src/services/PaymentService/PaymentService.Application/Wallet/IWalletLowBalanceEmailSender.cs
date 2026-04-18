namespace TBE.PaymentService.Application.Wallet;

/// <summary>
/// Plan 05-03 Task 2 — minimal e-mail delivery port used by
/// <c>WalletLowBalanceConsumer</c>. A dedicated interface (rather than leaning
/// on NotificationService's full <c>IEmailDelivery</c>) keeps the consumer
/// testable with a NSubstitute stub and avoids cross-service references that
/// would bring the whole notification Application project into PaymentService.
/// </summary>
public interface IWalletLowBalanceEmailSender
{
    /// <summary>Dispatch a low-balance advisory to <paramref name="recipients"/>.
    /// Implementations may batch into a single SendGrid call or fan out per
    /// recipient — test asserts only that every recipient is addressed.</summary>
    Task SendLowBalanceEmailAsync(
        IReadOnlyList<AgentAdminContact> recipients,
        Guid agencyId,
        decimal balance,
        decimal threshold,
        string currency,
        CancellationToken ct);
}
