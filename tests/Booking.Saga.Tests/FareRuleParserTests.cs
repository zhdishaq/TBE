using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TBE.BookingService.Application.Ttl;
using TBE.BookingService.Application.Ttl.Adapters;
using Xunit;

namespace Booking.Saga.Tests;

[Trait("Category", "Unit")]
public class FareRuleParserTests
{
    private static readonly string FixturesDir = Path.Combine(
        AppContext.BaseDirectory, "fixtures", "fare-rules");

    private static IFareRuleParser BuildParser()
    {
        var services = new ServiceCollection();
        services.AddKeyedSingleton<IFareRuleAdapter, AmadeusFareRuleAdapter>("amadeus");
        services.AddKeyedSingleton<IFareRuleAdapter, SabreFareRuleAdapter>("sabre");
        services.AddKeyedSingleton<IFareRuleAdapter, GalileoFareRuleAdapter>("galileo");
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        var sp = services.BuildServiceProvider();
        return new FareRuleParser(sp, NullLogger<FareRuleParser>.Instance);
    }

    private static string ReadFixture(string file) =>
        File.ReadAllText(Path.Combine(FixturesDir, file));

    [Fact(DisplayName = "FLTB06: amadeus structured lastTicketingDate parses ok")]
    public void FLTB06_amadeus_structured_lastTicketingDate_parses_ok()
    {
        var parser = BuildParser();
        var raw = ReadFixture("amadeus_sample1.json");

        parser.TryParse("amadeus", raw, out var deadline).Should().BeTrue();
        deadline.Kind.Should().Be(DateTimeKind.Utc);
        deadline.Year.Should().Be(2099);
        deadline.Month.Should().Be(5);
        deadline.Day.Should().Be(10);
    }

    [Fact(DisplayName = "FLTB06: amadeus free-text TICKET BY regex parses ok")]
    public void FLTB06_amadeus_free_text_TICKET_BY_regex_parses_ok()
    {
        var parser = BuildParser();
        // Free text only — no valid lastTicketingDate JSON structure.
        var raw = "[junk] TICKET BY 10MAY99 23:59 [more junk]";

        parser.TryParse("amadeus", raw, out var deadline).Should().BeTrue();
        deadline.Year.Should().Be(2099);
    }

    [Fact(DisplayName = "FLTB06: sabre TKT TL parses ok")]
    public void FLTB06_sabre_TKT_TL_parses_ok()
    {
        var parser = BuildParser();
        var raw = ReadFixture("sabre_sample1.xml");

        parser.TryParse("sabre", raw, out var deadline).Should().BeTrue();
        deadline.Year.Should().Be(2099);
    }

    [Fact(DisplayName = "FLTB06: galileo T.TAU parses ok")]
    public void FLTB06_galileo_T_TAU_parses_ok()
    {
        var parser = BuildParser();
        var raw = ReadFixture("galileo_sample1.txt");

        parser.TryParse("galileo", raw, out var deadline).Should().BeTrue();
        deadline.Year.Should().Be(2099);
    }

    [Fact(DisplayName = "FLTB06: past deadline returns false so saga applies 2h fallback (Pitfall 5)")]
    public void FLTB06_past_deadline_returns_false_so_saga_applies_2h_fallback()
    {
        var parser = BuildParser();
        // Clearly past year (2000) — Pitfall 5: must not return a past deadline.
        var raw = "TICKET BY 01JAN00 00:00";

        parser.TryParse("amadeus", raw, out _).Should().BeFalse();
    }

    [Fact(DisplayName = "FLTB06: unknown GDS returns false")]
    public void FLTB06_unknown_gds_returns_false()
    {
        var parser = BuildParser();
        parser.TryParse("worldspan", "anything", out _).Should().BeFalse();
    }
}
