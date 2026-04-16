namespace TBE.NotificationService.API.Templates.Models;

/// <summary>
/// RazorLight model for <c>HotelVoucher.cshtml</c> (NOTF-02). Every field on
/// <see cref="TBE.Contracts.Events.HotelBookingConfirmed"/> is copied 1:1 so the
/// template + PDF render without a secondary BookingService lookup. Brand copy
/// lines come from the consumer (sourced from IOptions&lt;BrandOptions&gt;).
/// </summary>
public sealed record HotelVoucherModel(
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
    string BrandName,
    string SupportPhone);
