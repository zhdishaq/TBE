using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using TBE.Contracts.Inventory.Models;
using TBE.SearchService.Application.Cache;
using FluentAssertions;
using Xunit;

namespace TBE.Tests.Unit.SearchService;

[Trait("Category", "Unit")]
public class RedisCacheTests
{
    private static SearchCacheService CreateService()
    {
        // Use in-memory implementations for unit tests
        var services = new ServiceCollection();
        services.AddHybridCache();
        services.AddMemoryCache();
        services.AddDistributedMemoryCache();
        var sp = services.BuildServiceProvider();
        return new SearchCacheService(
            sp.GetRequiredService<HybridCache>(),
            sp.GetRequiredService<IDistributedCache>());
    }

    private static UnifiedFlightOffer MakeOffer(string sourceRef) => new()
    {
        Source = "amadeus", SourceRef = sourceRef,
        ExpiresAt = DateTimeOffset.UtcNow.AddHours(1),
        CabinClass = "ECONOMY",
        Price = new PriceBreakdown { Currency = "GBP", Base = 100m, Surcharges = [], Taxes = [] },
        Segments = [], FareRules = []
    };

    [Fact(DisplayName = "INV07: Factory called once for two identical cache key requests")]
    public async Task GetOrSearch_CacheHit_FactoryCalledOnce()
    {
        var svc = CreateService();
        int factoryCallCount = 0;
        var key = "search:LHR:BKK:2024-12-01:1:ECO";

        var offers1 = await svc.GetOrSearchAsync(key, async ct =>
        {
            factoryCallCount++;
            await Task.Delay(1, ct);
            return (IReadOnlyList<UnifiedFlightOffer>)[MakeOffer("ref-1")];
        });

        var offers2 = await svc.GetOrSearchAsync(key, ct =>
        {
            factoryCallCount++;
            return ValueTask.FromResult<IReadOnlyList<UnifiedFlightOffer>>([MakeOffer("ref-2")]);
        });

        factoryCallCount.Should().Be(1);
        offers1.Should().HaveCount(1);
        offers2.Should().HaveCount(1); // served from cache
    }

    [Fact(DisplayName = "INV08: StoreBookingToken stores and GetBookingToken retrieves offer")]
    public async Task BookingToken_StoreAndRetrieve_Roundtrip()
    {
        var svc = CreateService();
        var sessionId = Guid.NewGuid().ToString();
        var offer = MakeOffer("booking-ref-1");

        await svc.StoreBookingTokenAsync(sessionId, offer);
        var retrieved = await svc.GetBookingTokenAsync(sessionId);

        retrieved.Should().NotBeNull();
        retrieved!.SourceRef.Should().Be("booking-ref-1");
        retrieved.Source.Should().Be("amadeus");
    }

    [Fact(DisplayName = "INV08: GetBookingToken returns null for unknown session")]
    public async Task GetBookingToken_UnknownSession_ReturnsNull()
    {
        var svc = CreateService();
        var result = await svc.GetBookingTokenAsync(Guid.NewGuid().ToString());
        result.Should().BeNull();
    }

    [Fact(DisplayName = "INV08: StoreBookingToken with empty session ID throws ArgumentException")]
    public async Task StoreBookingToken_EmptySessionId_Throws()
    {
        var svc = CreateService();
        var act = async () => await svc.StoreBookingTokenAsync("", MakeOffer("ref"));
        await act.Should().ThrowAsync<ArgumentException>();
    }
}
