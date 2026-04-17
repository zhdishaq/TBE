using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TBE.PricingService.Application.Agency;
using TBE.PricingService.Infrastructure;
using TBE.PricingService.Infrastructure.Agency;
using Xunit;

namespace Pricing.Tests;

/// <summary>
/// Plan 05-02 Task 1 — D-36 resolver (<c>override ?? base</c>) + D-41
/// (<c>commission == markup</c> in v1).
/// </summary>
public class MarkupRulesEngineTests
{
    private static PricingDbContext NewDb()
    {
        var opts = new DbContextOptionsBuilder<PricingDbContext>()
            .UseInMemoryDatabase($"pricing-{Guid.NewGuid()}")
            .Options;
        return new PricingDbContext(opts);
    }

    private static IAgencyMarkupRulesEngine NewEngine(PricingDbContext db) =>
        new AgencyMarkupRulesEngine(db);

    [Fact]
    public async Task ApplyMarkupAsync_with_base_only_rule_applies_flat_plus_percent()
    {
        await using var db = NewDb();
        var agencyId = Guid.NewGuid();
        db.AgencyMarkupRules.Add(new AgencyMarkupRule
        {
            AgencyId = agencyId,
            RouteClass = null,
            FlatAmount = 5m,
            PercentOfNet = 0.10m,   // 10%
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var quoted = await NewEngine(db).ApplyMarkupAsync(
            agencyId, netFare: 200m, routeClass: "Y-ECONOMY",
            currency: "GBP", offerId: "OFR-1", correlationId: Guid.NewGuid());

        // markup = 5 + 200 * 0.10 = 25 ; gross = 225 ; commission == markup (D-41)
        quoted.MarkupAmount.Should().Be(25m);
        quoted.GrossPrice.Should().Be(225m);
        quoted.CommissionAmount.Should().Be(25m);
        quoted.NetFare.Should().Be(200m);
        quoted.Currency.Should().Be("GBP");
    }

    [Fact]
    public async Task ApplyMarkupAsync_with_override_matching_routeclass_prefers_override()
    {
        await using var db = NewDb();
        var agencyId = Guid.NewGuid();
        db.AgencyMarkupRules.AddRange(
            new AgencyMarkupRule
            {
                AgencyId = agencyId, RouteClass = null,
                FlatAmount = 5m, PercentOfNet = 0.10m, IsActive = true,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            },
            new AgencyMarkupRule
            {
                AgencyId = agencyId, RouteClass = "J-BUSINESS",
                FlatAmount = 20m, PercentOfNet = 0.05m, IsActive = true,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            });
        await db.SaveChangesAsync();

        var quoted = await NewEngine(db).ApplyMarkupAsync(
            agencyId, netFare: 1000m, routeClass: "J-BUSINESS",
            currency: "GBP", offerId: "OFR-2", correlationId: Guid.NewGuid());

        // override row wins: markup = 20 + 1000 * 0.05 = 70 ; NOT base (5 + 1000*0.10 = 105)
        quoted.MarkupAmount.Should().Be(70m);
        quoted.GrossPrice.Should().Be(1070m);
    }

    [Fact]
    public async Task ApplyMarkupAsync_no_rule_returns_zero_markup()
    {
        await using var db = NewDb();
        var quoted = await NewEngine(db).ApplyMarkupAsync(
            agencyId: Guid.NewGuid(), netFare: 123.45m, routeClass: "Y-ECONOMY",
            currency: "GBP", offerId: "OFR-3", correlationId: Guid.NewGuid());
        quoted.MarkupAmount.Should().Be(0m);
        quoted.GrossPrice.Should().Be(123.45m);
        quoted.CommissionAmount.Should().Be(0m);
    }

    [Fact]
    public async Task ApplyMarkupAsync_negative_netFare_throws_ArgumentOutOfRangeException()
    {
        await using var db = NewDb();
        var act = () => NewEngine(db).ApplyMarkupAsync(
            Guid.NewGuid(), netFare: -1m, routeClass: null,
            currency: "GBP", offerId: "OFR-4", correlationId: Guid.NewGuid());
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task ApplyMarkupAsync_flatAmount_only_with_zero_percent_returns_flat()
    {
        await using var db = NewDb();
        var agencyId = Guid.NewGuid();
        db.AgencyMarkupRules.Add(new AgencyMarkupRule
        {
            AgencyId = agencyId, RouteClass = null,
            FlatAmount = 15m, PercentOfNet = 0m, IsActive = true,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var quoted = await NewEngine(db).ApplyMarkupAsync(
            agencyId, netFare: 500m, routeClass: null,
            currency: "GBP", offerId: "OFR-5", correlationId: Guid.NewGuid());

        quoted.MarkupAmount.Should().Be(15m);
        quoted.GrossPrice.Should().Be(515m);
    }

    [Fact]
    public async Task ApplyMarkupAsync_inactive_rule_is_ignored_and_returns_zero_markup()
    {
        await using var db = NewDb();
        var agencyId = Guid.NewGuid();
        db.AgencyMarkupRules.Add(new AgencyMarkupRule
        {
            AgencyId = agencyId, RouteClass = null,
            FlatAmount = 100m, PercentOfNet = 0.50m, IsActive = false,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var quoted = await NewEngine(db).ApplyMarkupAsync(
            agencyId, netFare: 200m, routeClass: null,
            currency: "GBP", offerId: "OFR-6", correlationId: Guid.NewGuid());
        quoted.MarkupAmount.Should().Be(0m);
    }

    [Fact]
    public async Task ApplyMarkupAsync_override_row_present_but_different_routeclass_falls_back_to_base()
    {
        await using var db = NewDb();
        var agencyId = Guid.NewGuid();
        db.AgencyMarkupRules.AddRange(
            new AgencyMarkupRule
            {
                AgencyId = agencyId, RouteClass = null,
                FlatAmount = 10m, PercentOfNet = 0.10m, IsActive = true,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            },
            new AgencyMarkupRule
            {
                AgencyId = agencyId, RouteClass = "J-BUSINESS",
                FlatAmount = 50m, PercentOfNet = 0.03m, IsActive = true,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            });
        await db.SaveChangesAsync();

        var quoted = await NewEngine(db).ApplyMarkupAsync(
            agencyId, netFare: 200m, routeClass: "Y-ECONOMY",
            currency: "GBP", offerId: "OFR-7", correlationId: Guid.NewGuid());

        // base row used (Y-ECONOMY does not match J-BUSINESS override): 10 + 200*0.10 = 30
        quoted.MarkupAmount.Should().Be(30m);
    }
}
