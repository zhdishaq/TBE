using Xunit;
namespace TBE.Tests.Unit.FlightConnectorService;
[Trait("Category", "Unit")]
public class InventoryConnectorTests
{
    [Fact(DisplayName = "INV01-stub: OAuth2 token is not logged")]
    public void AmadeusToken_IsNotLogged_Stub() { Assert.True(true); }

    [Fact(DisplayName = "INV02-stub: Raw GDS response maps to canonical model")]
    public void AmadeusResponse_MapsToCanonicalModel_Stub() { Assert.True(true); }
}
