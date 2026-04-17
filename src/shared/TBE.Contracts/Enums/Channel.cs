namespace TBE.Contracts.Enums;

/// <summary>
/// Booking distribution channel. Persisted on <c>BookingSagaState</c> per
/// Plan 05-02 / 05-CONTEXT.md D-24 and D-36. Drives the saga's IfElse branch
/// at <c>PnrCreated</c>: B2C → <c>AuthorizePaymentCommand</c> (Stripe card
/// authorize), B2B → <c>WalletReserveCommand</c> (hold funds on the agency
/// wallet without a Stripe charge).
/// </summary>
/// <remarks>
/// Backing type is <c>int</c> so the enum round-trips cleanly through the
/// <c>BookingSagaState.Channel</c> column migration in Plan 05-02. Default
/// value <c>B2C = 0</c> is intentional — existing rows that predate the
/// migration default to direct-customer semantics.
/// </remarks>
public enum Channel : int
{
    /// <summary>Direct customer booking via the B2C portal. Default value.</summary>
    B2C = 0,

    /// <summary>Travel-agent booking on behalf of a walk-in customer via the B2B portal.</summary>
    B2B = 1,
}
