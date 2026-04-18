namespace TBE.Contracts.Messages;

/// <summary>
/// Plan 05-03 Task 2 — published by PaymentService's
/// <c>WalletLowBalanceMonitor</c> BackgroundService when an agency's wallet
/// balance crosses below its configured <c>LowBalanceThresholdAmount</c>.
/// Consumed by <c>WalletLowBalanceConsumer</c> (PaymentService) which resolves
/// agent-admin contact(s) via Keycloak Admin API and dispatches the e-mail.
/// </summary>
/// <remarks>
/// <para>
/// This is distinct from the pre-existing
/// <c>TBE.Contracts.Events.WalletLowBalance</c> event (NotificationService owner,
/// published by Plan 03-02's <c>WalletReserveConsumer</c>). That older
/// event only carries <c>WalletId</c> and is consumed by the NotificationService
/// path. The new contract adds <c>AgencyId</c> + <c>Currency</c> + an explicit
/// <c>DetectedAt</c> timestamp (injected via <c>TimeProvider</c> for test
/// determinism) so the B2B monitor + consumer pair can resolve agent-admins by
/// agency without an indirect wallet-id → agency-id lookup.
/// </para>
/// <para>
/// <b>Hysteresis:</b> the consumer flips
/// <c>AgencyWallets.LowBalanceEmailSent = 1</c> after a successful send, and
/// <c>WalletTopUpService</c> resets it to <c>0</c> when a top-up pushes the
/// balance back above threshold — so a single cross-down fires exactly one
/// email per cross-up/cross-down cycle (T-05-03-07).
/// </para>
/// </remarks>
public sealed record WalletLowBalanceDetected(
    Guid AgencyId,
    decimal BalanceAmount,
    decimal ThresholdAmount,
    string Currency,
    DateTime DetectedAt);
