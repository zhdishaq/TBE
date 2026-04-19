namespace TBE.CrmService.Application.Projections;

/// <summary>
/// Plan 06-04 Task 1 — local projection of a booking in terminal state
/// (Confirmed / Cancelled). Built from saga integration events
/// (<c>BookingConfirmed</c>, <c>BookingCancelled</c>, <c>TicketIssued</c>).
/// Used by Customer 360 / Agency 360 / Upcoming Trips / Global Search.
/// </summary>
public sealed class BookingProjection
{
    public Guid Id { get; set; }

    public string? BookingReference { get; set; }

    public string? Pnr { get; set; }

    public Guid? CustomerId { get; set; }

    public Guid? AgencyId { get; set; }

    /// <summary>"b2c" | "b2b" | "manual" — mirrors <c>BookingSagaState.ChannelText</c>.</summary>
    public string Channel { get; set; } = string.Empty;

    /// <summary>"Confirmed" | "Cancelled" | "Failed".</summary>
    public string Status { get; set; } = "Confirmed";

    public decimal GrossAmount { get; set; }

    public decimal CommissionAmount { get; set; }

    public string Currency { get; set; } = "GBP";

    public DateTime ConfirmedAt { get; set; }

    public DateTime? CancelledAt { get; set; }

    public string? TicketNumber { get; set; }

    /// <summary>Populated lazily from the itinerary JSON when available (Phase 7 enrichment).</summary>
    public DateTime? TravelDate { get; set; }

    public string? OriginIata { get; set; }

    public string? DestinationIata { get; set; }

    /// <summary>Snapshot of customer name at booking time; NULL after erasure.</summary>
    public string? CustomerName { get; set; }
}
