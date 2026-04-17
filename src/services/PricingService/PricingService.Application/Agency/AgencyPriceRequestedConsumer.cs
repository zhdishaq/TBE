using MassTransit;
using Microsoft.Extensions.Logging;
using TBE.Contracts.Messages;

namespace TBE.PricingService.Application.Agency;

/// <summary>
/// Plan 05-02 / D-36 — consumes <see cref="AgencyPriceRequested"/>, delegates to
/// <see cref="IAgencyMarkupRulesEngine"/>, publishes the resulting
/// <see cref="AgencyPriceQuoted"/>. RED-phase skeleton.
/// </summary>
public sealed class AgencyPriceRequestedConsumer(
    IAgencyMarkupRulesEngine engine,
    ILogger<AgencyPriceRequestedConsumer> log) : IConsumer<AgencyPriceRequested>
{
    private readonly IAgencyMarkupRulesEngine _engine = engine;
    private readonly ILogger<AgencyPriceRequestedConsumer> _log = log;

    public async Task Consume(ConsumeContext<AgencyPriceRequested> ctx)
    {
        var m = ctx.Message;
        _log.LogDebug("AgencyPriceRequested received agency={AgencyId} offer={OfferId} net={NetFare} {Currency}",
            m.AgencyId, m.OfferId, m.NetFare, m.Currency);

        var quoted = await _engine.ApplyMarkupAsync(
            agencyId: m.AgencyId,
            netFare: m.NetFare,
            routeClass: m.RouteClass,
            currency: m.Currency,
            offerId: m.OfferId,
            correlationId: m.CorrelationId,
            ct: ctx.CancellationToken);

        await ctx.Publish(quoted);
    }
}
