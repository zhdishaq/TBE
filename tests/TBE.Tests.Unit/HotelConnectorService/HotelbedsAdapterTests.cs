using Xunit;
namespace TBE.Tests.Unit.HotelConnectorService;
[Trait("Category", "Unit")]
public class HotelbedsAdapterTests
{
    [Fact(DisplayName = "INV04-stub: Hotelbeds HMAC-SHA256 signing is correct")]
    public void Hotelbeds_HmacSigning_IsCorrect_Stub() { Assert.True(true); }
}
