using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TBE.BackofficeService.Application.Entities;

/// <summary>
/// Plan 06-04 Task 3 (COMP-03 / D-57) — cross-schema read model mapped to
/// <c>crm.Customers</c> owned by CrmService. Mirrors the
/// <see cref="BookingReadRow"/> pattern from Plan 06-01 Task 7 so the
/// BackofficeService <c>ErasureController</c> can resolve the customer's
/// email (for typed-email match + SHA-256 hashing) without introducing a
/// synchronous HTTP dependency on CrmService.
///
/// <para>
/// Kept read-only by convention — the ErasureController only SELECTs from
/// this DbSet and publishes <c>CustomerErasureRequested</c>; the CRM
/// consumer owns all mutations of the underlying table.
/// </para>
/// </summary>
[Table("Customers", Schema = "crm")]
public sealed class CustomerReadRow
{
    [Key]
    public Guid Id { get; set; }

    [MaxLength(320)]
    public string? Email { get; set; }

    [MaxLength(200)]
    public string? Name { get; set; }

    [MaxLength(32)]
    public string? Phone { get; set; }

    public DateTime CreatedAt { get; set; }

    public int LifetimeBookingsCount { get; set; }

    public decimal LifetimeGross { get; set; }

    public DateTime? LastBookingAt { get; set; }

    /// <summary>Set by CRM erasure consumer; drives "Anonymised Customer" client label.</summary>
    public bool IsErased { get; set; }

    public DateTime? ErasedAt { get; set; }
}
