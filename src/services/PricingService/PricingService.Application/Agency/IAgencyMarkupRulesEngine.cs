using TBE.Contracts.Messages;

namespace TBE.PricingService.Application.Agency;

/// <summary>
/// Plan 05-02 / D-36 — resolves the (NET, markup, gross, commission) 4-tuple for an
/// agency's view of a GDS offer. Implementation looks up the agency's base and
/// optional RouteClass-override row in <c>pricing.AgencyMarkupRules</c>, applying
/// the <c>override ?? base</c> rule.
/// </summary>
public interface IAgencyMarkupRulesEngine
{
    /// <summary>
    /// Apply the agency's markup rule to a GDS-sourced NET fare.
    /// </summary>
    /// <param name="agencyId">Agency tenant (never from request body — comes from JWT upstream).</param>
    /// <param name="netFare">GDS NET fare. Must be &gt;= 0.</param>
    /// <param name="routeClass">Route class for override lookup (e.g. <c>"J-BUSINESS"</c>); may be <c>null</c>.</param>
    /// <param name="currency">ISO-4217 currency; echoed onto the quote.</param>
    /// <param name="offerId">GDS offer identifier; echoed onto the quote.</param>
    /// <param name="correlationId">Saga / request correlation id; echoed onto the quote.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An <see cref="AgencyPriceQuoted"/> with the computed 4-tuple.</returns>
    /// <exception cref="System.ArgumentOutOfRangeException">Thrown when <paramref name="netFare"/> is negative.</exception>
    Task<AgencyPriceQuoted> ApplyMarkupAsync(
        Guid agencyId,
        decimal netFare,
        string? routeClass,
        string currency,
        string offerId,
        Guid correlationId,
        CancellationToken ct = default);
}
