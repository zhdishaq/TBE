namespace TBE.CrmService.Application.Projections;

/// <summary>
/// Plan 06-04 Task 3 / COMP-03 / D-57 — GDPR "right to erasure" proof-of-work.
/// One row per completed erasure; UNIQUE(EmailHash) ensures a repeated
/// request for the same person resolves to the same tombstone (dedup).
/// </summary>
/// <remarks>
/// Email is hashed with SHA-256 of the normalised value
/// (<c>email.Trim().ToLowerInvariant()</c>). D-57 accepts the rainbow-table
/// risk in v1; a project-level HMAC pepper is reserved for v2.
/// </remarks>
public sealed class CustomerErasureTombstoneRow
{
    public Guid Id { get; set; }

    public Guid OriginalCustomerId { get; set; }

    /// <summary>SHA-256 hex (64 chars) of the normalised email.</summary>
    public string EmailHash { get; set; } = string.Empty;

    public DateTime ErasedAt { get; set; }

    public string ErasedBy { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;
}
