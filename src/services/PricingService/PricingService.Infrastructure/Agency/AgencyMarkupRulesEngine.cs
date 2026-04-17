using TBE.Contracts.Messages;
using TBE.PricingService.Application.Agency;

namespace TBE.PricingService.Infrastructure.Agency;

/// <summary>
/// Plan 05-02 / D-36 resolver. RED-phase skeleton — <see cref="ApplyMarkupAsync"/>
/// throws <see cref="NotImplementedException"/>; GREEN implementation lands in the
/// next commit.
/// </summary>
public sealed class AgencyMarkupRulesEngine(PricingDbContext db) : IAgencyMarkupRulesEngine
{
    private readonly PricingDbContext _db = db;

    public Task<AgencyPriceQuoted> ApplyMarkupAsync(
        Guid agencyId,
        decimal netFare,
        string? routeClass,
        string currency,
        string offerId,
        Guid correlationId,
        CancellationToken ct = default)
    {
        throw new NotImplementedException("Plan 05-02 Task 1 GREEN: implement override ?? base resolver.");
    }
}
