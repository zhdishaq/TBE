namespace TBE.CrmService.Application.Projections;

/// <summary>
/// Plan 06-04 Task 1 / CRM-05 — future-dated bookings (TravelDate &gt;= today)
/// surfaced in the backoffice "Upcoming Trips" page. Separate table from
/// <see cref="BookingProjection"/> because rows with TravelDate in the past
/// are purged here (but kept in BookingProjection for historical audit).
/// </summary>
public sealed class UpcomingTripRow
{
    /// <summary>Primary key; equals <see cref="BookingProjection.Id"/>.</summary>
    public Guid BookingId { get; set; }

    public Guid? CustomerId { get; set; }

    public Guid? AgencyId { get; set; }

    public string? BookingReference { get; set; }

    public string? Pnr { get; set; }

    /// <summary>"Confirmed" | "Cancelled" (filter target).</summary>
    public string Status { get; set; } = "Confirmed";

    public DateTime TravelDate { get; set; }

    public decimal GrossAmount { get; set; }

    public string Currency { get; set; } = "GBP";

    public string? OriginIata { get; set; }

    public string? DestinationIata { get; set; }
}
