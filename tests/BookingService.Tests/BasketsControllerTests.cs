using Xunit;

namespace TBE.BookingService.Tests;

/// <summary>
/// RED placeholders authored in Wave 0 (Plan 04-00 Task 3). Plan 04-04
/// implements BasketsController (POST /baskets, GET /baskets/{id}/status,
/// POST /baskets/{id}/payment-intent per CONTEXT D-08/D-10) and turns
/// these green.
/// </summary>
public class BasketsControllerTests
{
    [Fact]
    [Trait("Category", "RedPlaceholder")]
    public void PostBaskets_with_flight_and_hotel_publishes_BasketInitiated()
    {
        Assert.Fail("Red placeholder — implemented by Plan 04-04");
    }

    [Fact]
    [Trait("Category", "RedPlaceholder")]
    public void PostBaskets_returns_401_for_unauthenticated()
    {
        Assert.Fail("Red placeholder — implemented by Plan 04-04");
    }

    [Fact]
    [Trait("Category", "RedPlaceholder")]
    public void GetBasketStatus_returns_current_state()
    {
        Assert.Fail("Red placeholder — implemented by Plan 04-04");
    }

    [Fact]
    [Trait("Category", "RedPlaceholder")]
    public void PostPaymentIntent_returns_client_secret_with_idempotency_key()
    {
        Assert.Fail("Red placeholder — implemented by Plan 04-04");
    }
}
