using Xunit;

namespace Pricing.Tests;

/// <summary>
/// Red placeholder for Plan 05-02 (AgencyPriceRequested consumer). Pitfall 23
/// mandates that the markup is computed server-side only and never echoed
/// back to the portal — the consumer publishes AgencyPriceResponse with the
/// final net/markup/gross breakdown derived from the authoritative
/// pricing.AgencyMarkupRules table.
/// </summary>
public class AgencyPriceRequestedConsumerTests
{
    [Fact]
    [Trait("Category", "RedPlaceholder")]
    public void Consumer_publishes_AgencyPriceResponse_with_computed_markup_for_agency()
    {
        Assert.Fail("MISSING — Plan 05-02 Task 5 (AgencyPriceRequested consumer; Pitfall 23 server-side markup).");
    }
}
