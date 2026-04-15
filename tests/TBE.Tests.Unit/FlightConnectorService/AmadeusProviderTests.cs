using Xunit;

namespace TBE.Tests.Unit.FlightConnectorService;

/// <summary>
/// Tests for Amadeus adapter — INV01 and INV02 requirements.
/// Full implementation added in Task 2 when AmadeusFlightProvider is available.
/// </summary>
[Trait("Category", "Unit")]
public class AmadeusProviderTests
{
    [Fact(DisplayName = "INV02-stub: AmadeusFlightProvider separates YQ/YR surcharges from government taxes")]
    public void MapOffer_SeparatesYqYrSurchargesFromGovernmentTaxes_Stub() { Assert.True(true); }

    [Fact(DisplayName = "INV02-stub: GrandTotal equals Base plus Surcharges plus Taxes")]
    public void MapOffer_GrandTotalIsCorrect_Stub() { Assert.True(true); }

    [Fact(DisplayName = "INV02_Auth-stub: AmadeusAuthHandler extends DelegatingHandler")]
    public void AmadeusAuthHandler_IsDelegatingHandler_Stub() { Assert.True(true); }
}
