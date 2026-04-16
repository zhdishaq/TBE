namespace TBE.Contracts.Events;

/// <summary>
/// Published when a B2C customer initiates a car-hire booking (CARB-01). Mirrors the
/// shape of <see cref="HotelBookingInitiated"/> — the saga downstream reserves supplier
/// inventory and either confirms (→ <see cref="CarBookingConfirmed"/>) or fails
/// (→ <see cref="CarBookingFailed"/>). Voucher email is keyed on the confirmation event
/// per NOTF-06.
/// </summary>
public record CarBookingInitiated(
    Guid BookingId,
    string UserId,
    Guid OfferId,
    string VendorName,
    string PickupLocation,
    string DropoffLocation,
    DateTime PickupAtUtc,
    DateTime DropoffAtUtc,
    int DriverAge,
    decimal TotalAmount,
    string Currency,
    string GuestEmail,
    string GuestFullName,
    DateTimeOffset At);

/// <summary>
/// Terminal success event — car hire reservation confirmed with supplier. Drives the
/// NOTF-02 car-voucher email (PKG-03 / CARB-03). <c>EventId</c> is the NOTF-06
/// idempotency key for the voucher delivery — one EventId → at most one email.
/// </summary>
public record CarBookingConfirmed(
    Guid BookingId,
    Guid EventId,
    string BookingReference,
    string SupplierRef,
    string VendorName,
    string PickupLocation,
    string DropoffLocation,
    DateTime PickupAtUtc,
    DateTime DropoffAtUtc,
    decimal TotalAmount,
    string Currency,
    string GuestEmail,
    string GuestFullName,
    DateTimeOffset At);

/// <summary>
/// Terminal failure event — vendor rejected the reservation (inventory gone, declined,
/// or policy mismatch). Customer sees a failure email; payment (if authorized) is voided
/// by the saga's compensation path.
/// </summary>
public record CarBookingFailed(
    Guid BookingId,
    string Cause,
    DateTimeOffset At);
