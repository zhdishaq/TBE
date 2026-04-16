namespace TBE.BookingService.Application.Baskets;

/// <summary>
/// Per-basket inbox-pattern audit used by <see cref="BasketPaymentOrchestrator"/> to
/// guarantee idempotency across MassTransit retries and Stripe webhook replays
/// (T-04-04-04). Keyed uniquely on <c>(BasketId, EventId)</c>; a duplicate insert raises
/// a SQL unique-index violation that the orchestrator treats as "already handled, skip".
/// </summary>
public sealed class BasketEventLog
{
    public long Id { get; set; }
    public Guid BasketId { get; set; }
    public Guid EventId { get; set; }

    /// <summary>Discriminator — e.g. "FlightTicketed", "HotelBookingConfirmed", "HotelBookingFailed".</summary>
    public string EventType { get; set; } = string.Empty;

    public DateTime HandledAtUtc { get; set; }
}
