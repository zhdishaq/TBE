using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TBE.BackofficeService.Application.Entities;

/// <summary>
/// Plan 06-02 Task 2 (BO-07) — supplier negotiated-rate contract.
/// Governs the net-rate + commission a supplier (airline, hotel, car)
/// charges the agency for a given product type during a validity window.
///
/// <para>
/// Status is NOT a stored column — it is computed on read as a
/// function of <c>TimeProvider.GetUtcNow()</c> against
/// <see cref="ValidFrom"/> / <see cref="ValidTo"/>:
/// <list type="bullet">
///   <item><c>Upcoming</c>: today &lt; ValidFrom</item>
///   <item><c>Active</c>: ValidFrom ≤ today ≤ ValidTo</item>
///   <item><c>Expired</c>: today &gt; ValidTo</item>
/// </list>
/// Rows are NEVER hard-deleted — <see cref="IsDeleted"/> soft-deletion
/// preserves audit history per finance retention requirements.
/// </para>
/// </summary>
[Table("SupplierContracts", Schema = "backoffice")]
public sealed class SupplierContract
{
    [Key]
    public Guid Id { get; set; }

    [MaxLength(256)]
    public string SupplierName { get; set; } = string.Empty;

    /// <summary>Flight | Hotel | Car | Package — enforced by CHECK constraint.</summary>
    [MaxLength(32)]
    public string ProductType { get; set; } = string.Empty;

    /// <summary>Net rate the supplier charges per unit (seat / night / day).</summary>
    public decimal NetRate { get; set; }

    /// <summary>Agency margin percent on top of NetRate; range [0, 100].</summary>
    public decimal CommissionPercent { get; set; }

    [MaxLength(3)]
    public string Currency { get; set; } = "GBP";

    public DateTime ValidFrom { get; set; }

    public DateTime ValidTo { get; set; }

    [MaxLength(2000)]
    public string Notes { get; set; } = string.Empty;

    [MaxLength(128)]
    public string CreatedBy { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    [MaxLength(128)]
    public string? UpdatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    /// <summary>Soft-delete flag. Default false; List endpoint excludes true.</summary>
    public bool IsDeleted { get; set; }
}
