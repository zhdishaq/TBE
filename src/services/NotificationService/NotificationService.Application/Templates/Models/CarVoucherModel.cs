namespace TBE.NotificationService.API.Templates.Models;

/// <summary>
/// RazorLight model for <c>CarVoucher.cshtml</c> (NOTF-02 family / CARB-03). Every field
/// on <see cref="TBE.Contracts.Events.CarBookingConfirmed"/> is copied 1:1 so the
/// template + PDF render without a secondary BookingService lookup.
/// </summary>
public sealed record CarVoucherModel(
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
    string BrandName,
    string SupportPhone);
