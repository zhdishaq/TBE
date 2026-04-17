namespace TBE.PaymentService.Application.Wallet;

/// <summary>
/// Plan 05-03 — application-level facade over the Stripe gateway for wallet top-ups.
/// Responsible for:
/// <list type="bullet">
///   <item>D-40 cap enforcement BEFORE any Stripe call.</item>
///   <item>Translating pounds → minor units (× 100) at the boundary.</item>
///   <item>Stamping <c>payment_mode=wallet_topup</c> metadata on the PaymentIntent.</item>
///   <item>Idempotent ledger commit on the corresponding webhook arrival.</item>
/// </list>
///
/// Admin agents call <see cref="CreateTopUpIntentAsync"/> from
/// <c>POST /api/wallet/top-up/intent</c>; <see cref="CommitTopUpAsync"/> runs inside the
/// Stripe webhook consumer after <c>payment_intent.succeeded</c>.
/// </summary>
public interface IWalletTopUpService
{
    /// <summary>
    /// Creates a Stripe PaymentIntent for a wallet top-up of <paramref name="amount"/>
    /// major-unit currency on the agency's wallet. Caps are re-read from
    /// <c>IOptionsMonitor&lt;WalletOptions&gt;.CurrentValue</c> on every call so admins
    /// can tighten/loosen them without a service restart.
    /// </summary>
    /// <param name="agencyId">Agency (and, by 1:1 mapping, wallet) id derived from the
    /// caller's JWT <c>agency_id</c> claim — NEVER from the request body (Pitfall 28).</param>
    /// <param name="amount">Requested top-up amount in major units (e.g. £250.00 → 250m).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Stripe client secret + PaymentIntent id for the browser's Stripe Elements.</returns>
    /// <exception cref="WalletTopUpOutOfRangeException">
    /// Thrown BEFORE any Stripe call when <paramref name="amount"/> falls outside the admin-configured range.
    /// </exception>
    Task<TopUpIntentResult> CreateTopUpIntentAsync(
        Guid agencyId,
        decimal amount,
        CancellationToken ct);

    /// <summary>
    /// Idempotently appends the <c>TopUp</c> ledger row for a succeeded Stripe PaymentIntent.
    /// Uses <paramref name="stripePaymentIntentId"/> as the idempotency key so MassTransit
    /// redelivery of the webhook never double-credits the wallet (Pitfall 20).
    /// </summary>
    Task CommitTopUpAsync(
        Guid agencyId,
        string stripePaymentIntentId,
        decimal amount,
        string currency,
        CancellationToken ct);
}

/// <summary>
/// Result of <see cref="IWalletTopUpService.CreateTopUpIntentAsync"/>. The controller
/// surfaces <see cref="ClientSecret"/> to the browser for the Stripe Elements confirm call.
/// </summary>
public sealed record TopUpIntentResult(
    string ClientSecret,
    string PaymentIntentId,
    decimal Amount,
    string Currency);
