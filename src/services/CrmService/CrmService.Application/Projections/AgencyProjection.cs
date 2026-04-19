namespace TBE.CrmService.Application.Projections;

/// <summary>
/// Plan 06-04 Task 1 — local projection of a B2B agency. Initial rows
/// can be seeded manually by <c>POST /api/crm/agencies</c>
/// (BackofficeAdminPolicy) or lazily on the first booking that carries
/// an <c>AgencyId</c> (future hardening). Lifetime counters are
/// maintained by <c>BookingConfirmedConsumer</c> + <c>WalletTopUpConsumer</c>.
/// </summary>
public sealed class AgencyProjection
{
    /// <summary>Equals the Keycloak <c>agency_id</c> attribute (D-33 single-valued).</summary>
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? ContactEmail { get; set; }

    public string? ContactPhone { get; set; }

    public DateTime CreatedAt { get; set; }

    public int LifetimeBookingsCount { get; set; }

    public decimal LifetimeGross { get; set; }

    public decimal LifetimeCommission { get; set; }

    public DateTime? LastBookingAt { get; set; }

    public bool IsActive { get; set; } = true;
}
