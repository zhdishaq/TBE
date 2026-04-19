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
///
/// <para>
/// Plan 06-02 Task 1 (BO-02) extends the enum with <see cref="Manual"/> =
/// 2 for staff-entered phone/walk-in bookings. Manual rows bypass the
/// saga entirely (<see cref="BookingSagaState.CurrentState"/> stays at
/// the terminal Confirmed value set by <c>ManualBookingCommand</c>) and
/// publish no <c>BookingInitiated</c> event, so neither the GDS PNR
/// consumer nor the payment-authorize consumer ever runs.
/// </para>
/// </remarks>
public enum Channel : int
{
    /// <summary>Direct customer booking via the B2C portal. Default value.</summary>
    B2C = 0,

    /// <summary>Travel-agent booking on behalf of a walk-in customer via the B2B portal.</summary>
    B2B = 1,

    /// <summary>
    /// Plan 06-02 Task 1 (BO-02) — staff-entered booking for phone or
    /// walk-in sales. No GDS API is hit (staff supplies the supplier
    /// reference / PNR directly); no saga runs and no payment
    /// authorization is performed — the booking is inserted in the
    /// terminal <c>Confirmed</c> state with the full fare breakdown
    /// captured from the portal wizard.
    /// </summary>
    Manual = 2,
}
