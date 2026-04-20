using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TBE.BackofficeService.Application.Entities;

/// <summary>
/// Plan 06-04 Task 3 (COMP-03 / D-57) — cross-schema read model mapped to
/// <c>crm.CustomerErasureTombstones</c> owned by CrmService. Used by the
/// BackofficeService <c>ErasureController</c> to enforce "same person
/// returns" dedup: a second erasure request for the same email resolves
/// to the existing tombstone with HTTP 409.
///
/// <para>
/// Kept read-only by convention — the tombstone table is owned / mutated
/// by <c>CrmService.Infrastructure.Consumers.CustomerErasureRequestedConsumer</c>.
/// </para>
/// </summary>
[Table("CustomerErasureTombstones", Schema = "crm")]
public sealed class CustomerErasureTombstoneReadRow
{
    [Key]
    public Guid Id { get; set; }

    public Guid OriginalCustomerId { get; set; }

    /// <summary>SHA-256 hex (64 chars) of the normalised email.</summary>
    [MaxLength(64)]
    public string EmailHash { get; set; } = string.Empty;

    public DateTime ErasedAt { get; set; }

    [MaxLength(128)]
    public string ErasedBy { get; set; } = string.Empty;

    [MaxLength(500)]
    public string Reason { get; set; } = string.Empty;
}
