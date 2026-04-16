namespace TBE.NotificationService.API.Templates.Models;

/// <summary>
/// RazorLight model for <c>BasketConfirmation.cshtml</c>. A single template handles both
/// full-success (<see cref="TBE.Contracts.Events.BasketConfirmed"/>) and partial-failure
/// (<see cref="TBE.Contracts.Events.BasketPartiallyConfirmed"/>) flows per D-09 — the
/// partial variant renders a "one charge on your statement" disclosure block when
/// <see cref="IsPartial"/> is true.
/// </summary>
public sealed record BasketConfirmationModel(
    string BrandName,
    string SupportPhone,
    string GuestFullName,
    string GuestEmail,
    BasketConfirmationFlightSection? FlightSection,
    BasketConfirmationHotelSection? HotelSection,
    BasketConfirmationCarSection? CarSection,
    decimal TotalAmount,
    decimal ChargedAmount,
    decimal RefundedAmount,
    string Currency,
    bool IsPartial,
    string? PartialFailureCause);

/// <summary>Per-component flight section — populated only when a flight leg was booked.</summary>
public sealed record BasketConfirmationFlightSection(
    string BookingReference,
    string Pnr,
    string? ETicketNumber);

/// <summary>Per-component hotel section — populated only when a hotel leg was booked.</summary>
public sealed record BasketConfirmationHotelSection(
    string BookingReference,
    string SupplierRef,
    string PropertyName,
    DateOnly CheckInDate,
    DateOnly CheckOutDate);

/// <summary>Per-component car section — populated only when a car leg was booked.</summary>
public sealed record BasketConfirmationCarSection(
    string BookingReference,
    string SupplierRef,
    string VendorName,
    string PickupLocation,
    string DropoffLocation,
    DateTime PickupAtUtc,
    DateTime DropoffAtUtc);
