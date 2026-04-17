namespace TBE.PricingService.Application.Agency;

/// <summary>
/// Plan 05-02 / D-36 — per-agency markup configuration. The table enforces the
/// max-2-rows invariant via a filtered unique index on <c>(AgencyId, RouteClass)</c>
/// where <c>IsActive = 1</c>: one base row (<c>RouteClass = NULL</c>) plus at most
/// one override row keyed by <c>RouteClass</c> (e.g. <c>"J-BUSINESS"</c>).
/// </summary>
/// <remarks>
/// Resolver contract: <c>MarkupRulesEngine.ApplyMarkupAsync</c> evaluates
/// <c>override ?? base</c>. An active row with <c>RouteClass == requestedRouteClass</c>
/// wins; otherwise the active row with <c>RouteClass == NULL</c> is used.
/// </remarks>
public sealed class AgencyMarkupRule
{
    /// <summary>
    /// Surrogate primary key. The plan originally specified a composite PK of
    /// (AgencyId, RouteClass) but EF Core's InMemory provider cannot track
    /// entities whose PK contains a nullable property; we substitute a
    /// surrogate <see cref="Guid"/> and enforce the (AgencyId, RouteClass)
    /// uniqueness via a filtered unique index on <c>IsActive = 1</c> — this
    /// satisfies the D-36 max-2-active-rows invariant identically.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Agency tenant this rule applies to.</summary>
    public Guid AgencyId { get; set; }

    /// <summary>
    /// Route class this rule overrides (e.g. <c>"Y-ECONOMY"</c>, <c>"J-BUSINESS"</c>).
    /// <c>NULL</c> indicates the agency-wide base rule.
    /// </summary>
    public string? RouteClass { get; set; }

    /// <summary>Flat markup added regardless of fare. Stored as <c>decimal(18,4)</c>.</summary>
    public decimal FlatAmount { get; set; }

    /// <summary>Percentage of NET fare added to markup. <c>0.1000</c> = 10%. Stored as <c>decimal(5,4)</c>.</summary>
    public decimal PercentOfNet { get; set; }

    /// <summary>Inactive rules are ignored by the resolver (enables soft-delete).</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>UTC timestamp when the row was created.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>UTC timestamp of the most recent update to this rule.</summary>
    public DateTime UpdatedAt { get; set; }
}
