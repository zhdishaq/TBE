using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TBE.Contracts.Messages;
using TBE.PricingService.Application.Agency;

namespace TBE.PricingService.Infrastructure.Agency;

/// <summary>
/// Plan 05-02 / D-36 resolver: evaluates the per-agency markup using the
/// <c>override ?? base</c> rule and produces an <see cref="AgencyPriceQuoted"/>
/// 4-tuple (Net / Markup / Gross / Commission).
/// </summary>
/// <remarks>
/// <para>
/// Per D-41, in v1 <c>CommissionAmount == MarkupAmount</c> — commission is
/// display-only; settlement is deferred to Phase 6.
/// </para>
/// <para>
/// The resolver loads every active rule for the agency in a single query and
/// then selects in-memory: first looks for an active override row matching
/// <paramref name="routeClass"/>, then falls back to the active base row
/// (<c>RouteClass == null</c>). When no rule matches, returns a zero-markup
/// quote and logs at Warning (Pitfall 23 — traceable default for new agencies).
/// </para>
/// </remarks>
public sealed class AgencyMarkupRulesEngine(
    PricingDbContext db,
    ILogger<AgencyMarkupRulesEngine>? log = null) : IAgencyMarkupRulesEngine
{
    private readonly PricingDbContext _db = db;
    private readonly ILogger<AgencyMarkupRulesEngine>? _log = log;

    public async Task<AgencyPriceQuoted> ApplyMarkupAsync(
        Guid agencyId,
        decimal netFare,
        string? routeClass,
        string currency,
        string offerId,
        Guid correlationId,
        CancellationToken ct = default)
    {
        if (netFare < 0m)
            throw new ArgumentOutOfRangeException(nameof(netFare), "netFare must be non-negative");

        // D-36: load all active rules for the agency; pick override else base.
        var rules = await _db.AgencyMarkupRules
            .Where(r => r.AgencyId == agencyId && r.IsActive)
            .ToListAsync(ct);

        var rule = rules.FirstOrDefault(r => r.RouteClass == routeClass && r.RouteClass != null)
                ?? rules.FirstOrDefault(r => r.RouteClass == null);

        if (rule is null)
        {
            _log?.LogWarning(
                "No active markup rule for agency {AgencyId}; emitting zero-markup quote",
                agencyId);
            return new AgencyPriceQuoted(
                correlationId, agencyId, offerId,
                NetFare: netFare, MarkupAmount: 0m, GrossPrice: netFare,
                CommissionAmount: 0m, Currency: currency);
        }

        var markup = rule.FlatAmount + netFare * rule.PercentOfNet;
        var gross = netFare + markup;
        // D-41: commission == markup in v1 (settlement deferred to Phase 6).
        var commission = markup;

        return new AgencyPriceQuoted(
            correlationId, agencyId, offerId,
            NetFare: netFare, MarkupAmount: markup, GrossPrice: gross,
            CommissionAmount: commission, Currency: currency);
    }
}
