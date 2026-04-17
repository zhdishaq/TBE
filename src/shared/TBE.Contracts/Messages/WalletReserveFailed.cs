namespace TBE.Contracts.Events;

/// <summary>
/// Plan 05-02 Task 2 — PaymentService response to
/// <see cref="TBE.Contracts.Commands.WalletReserveCommand"/> on failure.
/// Consumed by <c>BookingSaga</c> which compensates by voiding the PNR.
/// </summary>
/// <remarks>
/// <see cref="Reason"/> is one of the canonical strings
/// <c>"insufficient_funds"</c>, <c>"agency_locked"</c>, <c>"duplicate"</c>
/// (D-40). Saga uses it to populate <c>LastSuccessfulStep</c> / BookingFailed
/// cause without branching on typed enums over the bus.
/// </remarks>
public sealed record WalletReserveFailed(
    Guid CorrelationId,
    Guid BookingId,
    string Reason);
