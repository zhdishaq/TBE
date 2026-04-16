using Xunit;

namespace TBE.BookingService.Tests;

/// <summary>
/// RED placeholders authored in Wave 0 (Plan 04-00 Task 3). Plan 04-01
/// implements ReceiptsController (GET /api/bookings/{id}/receipt.pdf per
/// CONTEXT D-15) and turns these green.
///
/// Filtered out of the baseline via Trait("Category","RedPlaceholder"):
/// <c>dotnet test --filter "Category!=RedPlaceholder"</c>.
/// </summary>
public class ReceiptsControllerTests
{
    [Fact]
    [Trait("Category", "RedPlaceholder")]
    public void GetReceipt_returns_PDF_for_owner()
    {
        Assert.Fail("Red placeholder — implemented by Plan 04-01");
    }

    [Fact]
    [Trait("Category", "RedPlaceholder")]
    public void GetReceipt_returns_403_for_other_user()
    {
        Assert.Fail("Red placeholder — implemented by Plan 04-01");
    }

    [Fact]
    [Trait("Category", "RedPlaceholder")]
    public void GetReceipt_returns_404_for_unknown_id()
    {
        Assert.Fail("Red placeholder — implemented by Plan 04-01");
    }
}
