namespace TBE.CrmService.Application.Projections;

/// <summary>
/// Plan 06-04 Task 1 — local projection of a B2C customer built from
/// <c>UserRegistered</c> + <c>BookingConfirmed</c> events (D-51). PII
/// fields are nullable so <c>CustomerErasureRequestedConsumer</c>
/// (Task 3) can NULL them in place without violating the NOT NULL
/// schema — per COMP-03 / D-57 the anonymised row remains for
/// linking historic bookings but carries no personal data.
/// </summary>
public sealed class CustomerProjection
{
    /// <summary>Equals the Keycloak user id (<c>UserRegistered.UserId</c>).</summary>
    public Guid Id { get; set; }

    /// <summary>NULL after erasure (COMP-03).</summary>
    public string? Email { get; set; }

    /// <summary>NULL after erasure.</summary>
    public string? Name { get; set; }

    /// <summary>NULL after erasure.</summary>
    public string? Phone { get; set; }

    public DateTime CreatedAt { get; set; }

    public int LifetimeBookingsCount { get; set; }

    public decimal LifetimeGross { get; set; }

    public DateTime? LastBookingAt { get; set; }

    /// <summary>Flipped to true by <c>CustomerErasureRequestedConsumer</c>; drives "Anonymised Customer" client-side label.</summary>
    public bool IsErased { get; set; }

    public DateTime? ErasedAt { get; set; }
}
