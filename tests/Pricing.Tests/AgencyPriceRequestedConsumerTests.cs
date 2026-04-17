using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using TBE.Contracts.Messages;
using TBE.PricingService.Application.Agency;
using Xunit;

namespace Pricing.Tests;

/// <summary>
/// Plan 05-02 Task 1 — D-36 AgencyPriceRequestedConsumer contract test via
/// MassTransit in-memory test harness (Pitfall 23 server-side markup).
/// </summary>
public class AgencyPriceRequestedConsumerTests
{
    [Fact]
    public async Task Consume_publishes_AgencyPriceQuoted_with_engine_output()
    {
        var correlationId = Guid.NewGuid();
        var agencyId = Guid.NewGuid();

        var engine = Substitute.For<IAgencyMarkupRulesEngine>();
        engine.ApplyMarkupAsync(
                Arg.Is(agencyId), Arg.Is(200m), Arg.Is<string?>("Y-ECONOMY"),
                Arg.Is("GBP"), Arg.Is("OFR-42"), Arg.Is(correlationId),
                Arg.Any<CancellationToken>())
            .Returns(new AgencyPriceQuoted(
                correlationId, agencyId, "OFR-42",
                NetFare: 200m, MarkupAmount: 25m, GrossPrice: 225m,
                CommissionAmount: 25m, Currency: "GBP"));

        await using var provider = new ServiceCollection()
            .AddSingleton(engine)
            .AddMassTransitTestHarness(cfg =>
            {
                cfg.AddConsumer<AgencyPriceRequestedConsumer>();
            })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        try
        {
            await harness.Bus.Publish(new AgencyPriceRequested(
                correlationId, agencyId, "OFR-42",
                NetFare: 200m, Currency: "GBP", RouteClass: "Y-ECONOMY"));

            (await harness.Consumed.Any<AgencyPriceRequested>()).Should().BeTrue();
            (await harness.Published.Any<AgencyPriceQuoted>()).Should().BeTrue();

            var published = harness.Published.Select<AgencyPriceQuoted>().Single();
            published.Context.Message.MarkupAmount.Should().Be(25m);
            published.Context.Message.GrossPrice.Should().Be(225m);
            published.Context.Message.CommissionAmount.Should().Be(25m);
            published.Context.Message.AgencyId.Should().Be(agencyId);
            published.Context.Message.OfferId.Should().Be("OFR-42");
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task Consume_with_no_rules_publishes_zero_markup_quote()
    {
        var correlationId = Guid.NewGuid();
        var agencyId = Guid.NewGuid();

        var engine = Substitute.For<IAgencyMarkupRulesEngine>();
        engine.ApplyMarkupAsync(
                agencyId, 100m, null, "GBP", "OFR-ZERO", correlationId,
                Arg.Any<CancellationToken>())
            .Returns(new AgencyPriceQuoted(
                correlationId, agencyId, "OFR-ZERO",
                NetFare: 100m, MarkupAmount: 0m, GrossPrice: 100m,
                CommissionAmount: 0m, Currency: "GBP"));

        await using var provider = new ServiceCollection()
            .AddSingleton(engine)
            .AddMassTransitTestHarness(cfg => cfg.AddConsumer<AgencyPriceRequestedConsumer>())
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        try
        {
            await harness.Bus.Publish(new AgencyPriceRequested(
                correlationId, agencyId, "OFR-ZERO", 100m, "GBP", null));

            (await harness.Published.Any<AgencyPriceQuoted>()).Should().BeTrue();
            var msg = harness.Published.Select<AgencyPriceQuoted>().Single().Context.Message;
            msg.MarkupAmount.Should().Be(0m);
            msg.GrossPrice.Should().Be(100m);
        }
        finally
        {
            await harness.Stop();
        }
    }
}
