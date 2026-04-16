using MassTransit;

namespace TBE.BookingService.Application.Saga;

/// <summary>
/// Persisted saga state for hotel bookings (HOTB-01..05). Separate from
/// <see cref="BookingSagaState"/> (flight saga) per PKG-04 — hotel supplier_ref
/// is preserved independently from any flight PNR/ticket. <c>Version</c> is
/// mapped as a row-version concurrency token to enforce optimistic locking.
/// The plan's 04-PATTERNS § BasketMap rules apply: all money in
/// <c>decimal(18,4)</c>, currency 3-letter ISO, <c>IsRowVersion</c> on Version,
/// HasIndex on UserId / SupplierRef / Status.
/// </summary>
public class HotelBookingSagaState : SagaStateMachineInstance, ISagaVersion
{
    public Guid CorrelationId { get; set; }
    public int Version { get; set; }

    public string UserId { get; set; } = string.Empty;
    public string BookingReference { get; set; } = string.Empty;

    /// <summary>Supplier-assigned confirmation number — HOTB-05. Null until hotel confirms.</summary>
    public string? SupplierRef { get; set; }

    public string PropertyName { get; set; } = string.Empty;
    public string AddressLine { get; set; } = string.Empty;
    public DateOnly CheckInDate { get; set; }
    public DateOnly CheckOutDate { get; set; }
    public int Rooms { get; set; }
    public int Adults { get; set; }
    public int Children { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = string.Empty;

    public string GuestEmail { get; set; } = string.Empty;
    public string GuestFullName { get; set; } = string.Empty;

    /// <summary>One of: Pending, PendingPayment, Confirmed, Failed, Cancelled.</summary>
    public string Status { get; set; } = "Pending";
    public string? FailureCause { get; set; }
    public string? StripePaymentIntentId { get; set; }

    public DateTime InitiatedAtUtc { get; set; }
    public DateTime? ConfirmedAtUtc { get; set; }
}
