namespace TBE.Contracts.Events;

/// <summary>
/// Value object carried inside <see cref="HotelBookingInitiated"/> — the lead guest
/// supplied by the customer at booking time. Mirrors the B2C portal's guest form.
/// </summary>
public sealed record HotelGuestDto(
    string FullName,
    string Email,
    string? PhoneNumber);

/// <summary>
/// Published by the BookingService HotelBookingsController when a customer starts a
/// hotel booking from the B2C portal. Starts the HotelBookingSaga (Phase 4).
/// </summary>
public record HotelBookingInitiated(
    Guid BookingId,
    string UserId,
    Guid OfferId,
    HotelGuestDto Guest,
    DateTimeOffset At);

/// <summary>
/// Terminal success event published once the hotel supplier confirms the reservation and
/// payment has been captured. <c>EventId</c> is the notification idempotency key per D-19 —
/// NotificationService uses it as the second half of the <c>(EventId, EmailType)</c> unique
/// key on <c>EmailIdempotencyLog</c>. Carries the full voucher payload so the voucher email
/// + PDF can be rendered without a secondary BookingService lookup.
/// </summary>
public record HotelBookingConfirmed(
    Guid BookingId,
    Guid EventId,
    string BookingReference,
    string SupplierRef,
    string PropertyName,
    string AddressLine,
    DateOnly CheckInDate,
    DateOnly CheckOutDate,
    int Rooms,
    int Adults,
    int Children,
    decimal TotalAmount,
    string Currency,
    string GuestEmail,
    string GuestFullName,
    DateTimeOffset At);

/// <summary>
/// Terminal failure event for hotel bookings — supplier rejected the reservation or a
/// compensation step failed. Consumed by the saga for user-visible status transitions
/// and (future) operator alerting.
/// </summary>
public record HotelBookingFailed(
    Guid BookingId,
    string Cause,
    DateTimeOffset At);
