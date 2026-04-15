using Microsoft.EntityFrameworkCore;
using TBE.Contracts.Inventory.Models;
using TBE.PricingService.Application.Rules;
using TBE.PricingService.Application.Rules.Models;
using TBE.PricingService.Infrastructure;
using TBE.PricingService.Infrastructure.Rules;
using FluentAssertions;
using Xunit;

namespace TBE.Tests.Unit.PricingService;

[Trait("Category", "Unit")]
public class MarkupRulesEngineTests
{
    private static PricingDbContext CreateDb(params MarkupRule[] rules)
    {
        var options = new DbContextOptionsBuilder<PricingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new PricingDbContext(options);
        db.MarkupRules.AddRange(rules);
        db.SaveChanges();
        return db;
    }

    private static UnifiedFlightOffer MakeOffer(decimal grandTotal, string carrier = "BA") =>
        new()
        {
            Source = "amadeus", SourceRef = "ref-1",
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1),
            CabinClass = "ECONOMY",
            Price = new PriceBreakdown { Currency = "GBP", Base = grandTotal, Surcharges = [], Taxes = [] },
            Segments = [new FlightSegment
            {
                DepartureAirport = "LHR", ArrivalAirport = "BKK",
                DepartureAt = DateTimeOffset.UtcNow, ArrivalAt = DateTimeOffset.UtcNow.AddHours(11),
                CarrierCode = carrier, FlightNumber = "9", DurationMinutes = 660
            }],
        };

    [Fact(DisplayName = "INV09: 5% percentage markup on £100 net fare returns £105 gross")]
    public async Task Apply_PercentageMarkup_ReturnsCorrectGross()
    {
        var rule = new MarkupRule
        {
            ProductType = "flight", Channel = "B2C",
            Type = MarkupType.Percentage, Value = 5m, IsActive = true
        };
        var engine = new MarkupRulesEngine(CreateDb(rule));

        var result = await engine.ApplyAsync(MakeOffer(100m), new PricingContext
        { Channel = "B2C", ProductType = "flight" });

        result.GrossSelling.Should().Be(105m);
        result.MarkupAmount.Should().Be(5m);
        result.NetFare.Should().Be(100m);
        result.AppliedRuleId.Should().Be(rule.Id);
    }

    [Fact(DisplayName = "INV09: £10 fixed markup on £100 fare returns £110 gross")]
    public async Task Apply_FixedMarkup_ReturnsCorrectGross()
    {
        var rule = new MarkupRule
        {
            ProductType = "flight", Channel = "B2C",
            Type = MarkupType.FixedAmount, Value = 10m, IsActive = true
        };
        var engine = new MarkupRulesEngine(CreateDb(rule));

        var result = await engine.ApplyAsync(MakeOffer(100m), new PricingContext
        { Channel = "B2C", ProductType = "flight" });

        result.GrossSelling.Should().Be(110m);
        result.MarkupAmount.Should().Be(10m);
    }

    [Fact(DisplayName = "INV09: 20% markup capped at £15 on £200 fare returns £215")]
    public async Task Apply_PercentageMarkupCapped_ReturnsCappedGross()
    {
        var rule = new MarkupRule
        {
            ProductType = "flight", Channel = "B2C",
            Type = MarkupType.Percentage, Value = 20m, MaxAmount = 15m, IsActive = true
        };
        var engine = new MarkupRulesEngine(CreateDb(rule));

        var result = await engine.ApplyAsync(MakeOffer(200m), new PricingContext
        { Channel = "B2C", ProductType = "flight" });

        result.GrossSelling.Should().Be(215m); // capped at 15, not 40
        result.MarkupAmount.Should().Be(15m);
    }

    [Fact(DisplayName = "INV09: No matching rule returns GrossSelling == NetFare")]
    public async Task Apply_NoMatchingRule_ReturnsUnchangedPrice()
    {
        var engine = new MarkupRulesEngine(CreateDb()); // empty rules

        var result = await engine.ApplyAsync(MakeOffer(100m), new PricingContext
        { Channel = "B2C", ProductType = "flight" });

        result.GrossSelling.Should().Be(100m);
        result.MarkupAmount.Should().Be(0m);
        result.AppliedRuleId.Should().BeNull();
    }

    [Fact(DisplayName = "INV09: Airline-specific rule applies only to matching carrier")]
    public async Task Apply_AirlineSpecificRule_OnlyAppliesForMatchingCarrier()
    {
        var rule = new MarkupRule
        {
            ProductType = "flight", Channel = "B2C",
            AirlineCode = "BA", Type = MarkupType.FixedAmount, Value = 20m, IsActive = true
        };
        var engine = new MarkupRulesEngine(CreateDb(rule));

        var baResult = await engine.ApplyAsync(MakeOffer(100m, "BA"),
            new PricingContext { Channel = "B2C", ProductType = "flight", CarrierCode = "BA" });
        var ekResult = await engine.ApplyAsync(MakeOffer(100m, "EK"),
            new PricingContext { Channel = "B2C", ProductType = "flight", CarrierCode = "EK" });

        baResult.GrossSelling.Should().Be(120m); // rule applies
        ekResult.GrossSelling.Should().Be(100m); // no matching rule
    }
}
