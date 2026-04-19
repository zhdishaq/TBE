namespace TBE.PricingService.Application.Agency;

/// <summary>
/// Plan 06-03 / D-52 — immutable audit trail for every <see cref="AgencyMarkupRule"/>
/// mutation. Every Create/Update/Deactivate/Delete on a markup rule writes exactly
/// one row to this table inside the same DbTransaction as the rule change, so audit
/// and rule are atomic.
/// </summary>
/// <remarks>
/// <para>
/// <c>AgencyId</c> is denormalised (copied from the target rule) so the primary
/// "show me every change to Agency X" access path from the Backoffice UI does not
/// need to join back to <see cref="AgencyMarkupRule"/> (useful when the rule is
/// hard-deleted — we still want its history).
/// </para>
/// <para>
/// <c>BeforeJson</c> / <c>AfterJson</c> hold the full <see cref="AgencyMarkupRule"/>
/// serialised state. <c>BeforeJson</c> is NULL on Created; <c>AfterJson</c> is NULL
/// on Deleted. Both non-null on Updated / Deactivated.
/// </para>
/// </remarks>
public sealed class MarkupRuleAuditLogRow
{
    /// <summary>Surrogate bigint identity PK.</summary>
    public long Id { get; set; }

    /// <summary>The <see cref="AgencyMarkupRule.Id"/> this row audits.</summary>
    public Guid RuleId { get; set; }

    /// <summary>Agency this rule applied to (denormalised copy — preserves history after hard-delete).</summary>
    public Guid AgencyId { get; set; }

    /// <summary>One of <c>Created</c> / <c>Updated</c> / <c>Deactivated</c> / <c>Deleted</c>.</summary>
    public string Action { get; set; } = null!;

    /// <summary>Keycloak <c>preferred_username</c> of the operator who triggered the mutation.</summary>
    public string Actor { get; set; } = null!;

    /// <summary>JSON snapshot of the rule state BEFORE the mutation. NULL on Created.</summary>
    public string? BeforeJson { get; set; }

    /// <summary>JSON snapshot of the rule state AFTER the mutation. NULL on Deleted.</summary>
    public string? AfterJson { get; set; }

    /// <summary>Human-supplied justification for the change (D-52 required, max 500 chars).</summary>
    public string Reason { get; set; } = null!;

    /// <summary>UTC timestamp the mutation was committed.</summary>
    public DateTime ChangedAt { get; set; }
}
