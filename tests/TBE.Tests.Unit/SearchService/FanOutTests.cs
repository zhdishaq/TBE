using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TBE.Contracts.Inventory;
using TBE.Contracts.Inventory.Models;
using TBE.SearchService.Application.FlightSearch;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Xunit;

namespace TBE.Tests.Unit.SearchService;

[Trait("Category", "Unit")]
public class FanOutTests
{
    private static readonly FlightSearchRequest DefaultRequest = new()
    {
        Origin = "LHR", Destination = "BKK",
        DepartureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
        Adults = 1
    };

    private static UnifiedFlightOffer MakeOffer(string source, string sourceRef, decimal total) =>
        new()
        {
            Source = source, SourceRef = sourceRef, ExpiresAt = DateTimeOffset.UtcNow.AddHours(1),
            CabinClass = "ECONOMY",
            Price = new PriceBreakdown { Currency = "GBP", Base = total, Surcharges = [], Taxes = [] },
            Segments = [], FareRules = []
        };

    [Fact(DisplayName = "INV06_Happy: returns combined results from all providers")]
    public async Task SearchAsync_ReturnsCombinedResults_WhenBothProviderSucceed()
    {
        var amadeus = Substitute.For<IFlightAvailabilityProvider>();
        amadeus.Name.Returns("amadeus");
        amadeus.SearchAsync(Arg.Any<FlightSearchRequest>(), Arg.Any<CancellationToken>())
               .Returns([MakeOffer("amadeus", "am-1", 500m)]);
        var sabre = Substitute.For<IFlightAvailabilityProvider>();
        sabre.Name.Returns("sabre");
        sabre.SearchAsync(Arg.Any<FlightSearchRequest>(), Arg.Any<CancellationToken>())
             .Returns([MakeOffer("sabre", "sa-1", 490m)]);

        var orch = new FlightSearchOrchestrator([amadeus, sabre],
            Substitute.For<ILogger<FlightSearchOrchestrator>>());
        var results = await orch.SearchAsync(DefaultRequest);

        results.Should().HaveCount(2);
        results[0].Price.GrandTotal.Should().Be(490m); // ordered by price
    }

    [Fact(DisplayName = "INV06_Degraded: returns partial results when one provider fails")]
    public async Task SearchAsync_ReturnPartialResults_WhenOneProviderFails()
    {
        var failing = Substitute.For<IFlightAvailabilityProvider>();
        failing.Name.Returns("amadeus");
        failing.SearchAsync(Arg.Any<FlightSearchRequest>(), Arg.Any<CancellationToken>())
               .ThrowsAsync(new HttpRequestException("GDS timeout"));
        var ok = Substitute.For<IFlightAvailabilityProvider>();
        ok.Name.Returns("sabre");
        ok.SearchAsync(Arg.Any<FlightSearchRequest>(), Arg.Any<CancellationToken>())
          .Returns([MakeOffer("sabre", "sa-1", 490m)]);

        var orch = new FlightSearchOrchestrator([failing, ok],
            Substitute.For<ILogger<FlightSearchOrchestrator>>());
        var results = await orch.SearchAsync(DefaultRequest);

        results.Should().HaveCount(1);
        results[0].Source.Should().Be("sabre");
    }

    [Fact(DisplayName = "INV06: deduplicates offers with same SourceRef")]
    public async Task SearchAsync_DeduplicatesIdenticalSourceRefs()
    {
        var p1 = Substitute.For<IFlightAvailabilityProvider>();
        p1.Name.Returns("amadeus");
        p1.SearchAsync(Arg.Any<FlightSearchRequest>(), Arg.Any<CancellationToken>())
          .Returns([MakeOffer("amadeus", "SAME-REF", 500m)]);
        var p2 = Substitute.For<IFlightAvailabilityProvider>();
        p2.Name.Returns("sabre");
        p2.SearchAsync(Arg.Any<FlightSearchRequest>(), Arg.Any<CancellationToken>())
          .Returns([MakeOffer("sabre", "SAME-REF", 500m)]);

        var orch = new FlightSearchOrchestrator([p1, p2],
            Substitute.For<ILogger<FlightSearchOrchestrator>>());
        var results = await orch.SearchAsync(DefaultRequest);

        results.Should().HaveCount(1);
    }
}
