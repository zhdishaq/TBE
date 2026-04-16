namespace TBE.BookingService.Application.Cars;

/// <summary>
/// Plan 04-04 Task 3a — minimal car-hire aggregate persisted by BookingService while
/// the car saga is orchestrated downstream. Mirrors the shape of
/// <see cref="TBE.BookingService.Application.Saga.HotelBookingSagaState"/> but carries
/// only the columns the controller + NotificationService need (no saga machinery yet —
/// Phase 5 adds the full saga). Money is <c>decimal(18,4)</c>; Currency is ISO 4217.
/// </summary>
public sealed class CarBooking
{
    public Guid BookingId { get; set; }
    public string UserId { get; set; } = string.Empty;

    public Guid OfferId { get; set; }
    public string? SupplierRef { get; set; }
    public string BookingReference { get; set; } = string.Empty;

    public string VendorName { get; set; } = string.Empty;
    public string PickupLocation { get; set; } = string.Empty;
    public string DropoffLocation { get; set; } = string.Empty;
    public DateTime PickupAtUtc { get; set; }
    public DateTime DropoffAtUtc { get; set; }
    public int DriverAge { get; set; }

    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "GBP";

    public string GuestEmail { get; set; } = string.Empty;
    public string GuestFullName { get; set; } = string.Empty;

    /// <summary>"Pending", "Confirmed", "Failed".</summary>
    public string Status { get; set; } = "Pending";

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;

    public byte[]? Version { get; set; }
}
