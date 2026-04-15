namespace TBE.PaymentService.Infrastructure.Stripe;

/// <summary>
/// Replay-dedup row for Stripe webhook ingress. PK = Stripe event.Id so that replayed
/// deliveries short-circuit via a unique-violation (Pitfall 4).
/// </summary>
public sealed class StripeWebhookEvent
{
    public string EventId { get; set; } = default!;
    public string EventType { get; set; } = default!;
    public DateTime ReceivedAtUtc { get; set; }
}
