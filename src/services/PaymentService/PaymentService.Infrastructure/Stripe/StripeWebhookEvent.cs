namespace TBE.PaymentService.Infrastructure.Stripe;

/// <summary>
/// Replay-dedup row for Stripe webhook ingress. PK = Stripe event.Id so that replayed
/// deliveries short-circuit via a unique-violation (Pitfall 4).
///
/// <para>
/// Plan 06-02 Task 3 (BO-06 / D-55) — extended with two columns:
/// <list type="bullet">
///   <item><see cref="RawPayload"/>: full JSON body as delivered by Stripe.
///         Required by the nightly reconciliation job so we can compare
///         amounts / metadata against the wallet ledger after the fact.</item>
///   <item><see cref="Processed"/>: flipped to <c>true</c> only when a
///         typed consumer (e.g. StripeWebhookConsumer) has handled the
///         envelope. A <c>Processed=false</c> row older than 1h is a
///         discrepancy raised by <c>PaymentReconciliationService</c>.</item>
/// </list>
/// </para>
/// </summary>
public sealed class StripeWebhookEvent
{
    public string EventId { get; set; } = default!;
    public string EventType { get; set; } = default!;
    public DateTime ReceivedAtUtc { get; set; }

    /// <summary>
    /// Full Stripe event body as JSON. Schema is the generic Stripe
    /// envelope — <c>data.object</c> is the relevant PaymentIntent /
    /// Charge / BalanceTransaction depending on <see cref="EventType"/>.
    /// Never NULL; defaults to <c>"{}"</c> for pre-migration rows.
    /// </summary>
    public string RawPayload { get; set; } = "{}";

    /// <summary>
    /// True once a downstream consumer has successfully handled this event.
    /// Set by <c>StripeWebhookConsumer</c> (or the relevant domain handler)
    /// via a follow-up UPDATE; reconciliation job flags rows that stay
    /// <c>false</c> past the 1-hour handler SLA.
    /// </summary>
    public bool Processed { get; set; }
}
