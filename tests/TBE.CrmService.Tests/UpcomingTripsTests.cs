using Xunit;

namespace TBE.CrmService.Tests;

/// <summary>
/// Plan 06-04 / CRM-05 — Upcoming Trips read surface. Default window is
/// today..today+30d; callers can override <c>from</c> / <c>to</c> and
/// filter by <c>status</c> and <c>agencyId</c>. The projection row is
/// seeded by the TicketIssuedConsumer and cancelled by the
/// BookingCancelledConsumer.
///
/// <para>
/// Status: RED placeholder. Requires a live MSSQL Testcontainer +
/// WebApplicationFactory so the consumer chain (TicketIssued → row
/// creation → controller read) can be observed end-to-end. Tagged
/// <c>Category=RedPlaceholder</c>.
/// </para>
/// </summary>
public sealed class UpcomingTripsTests
{
    [Fact]
    [Trait("Category", "RedPlaceholder")]
    [Trait("Category", "Phase06")]
    public void TicketIssued_with_future_TravelDate_seeds_UpcomingTripRow()
    {
        Assert.Fail("MISSING — requires MSSQL Testcontainer + MassTransitHarness (CRM-05).");
    }

    [Fact]
    [Trait("Category", "RedPlaceholder")]
    [Trait("Category", "Phase06")]
    public void Default_window_returns_today_through_today_plus_30d()
    {
        Assert.Fail("MISSING — requires WebApplicationFactory + MSSQL Testcontainer (CRM-05).");
    }

    [Fact]
    [Trait("Category", "RedPlaceholder")]
    [Trait("Category", "Phase06")]
    public void Status_filter_narrows_to_Confirmed_only()
    {
        Assert.Fail("MISSING — requires WebApplicationFactory + MSSQL Testcontainer (CRM-05).");
    }

    [Fact]
    [Trait("Category", "RedPlaceholder")]
    [Trait("Category", "Phase06")]
    public void AgencyId_filter_restricts_to_one_agency()
    {
        Assert.Fail("MISSING — requires WebApplicationFactory + MSSQL Testcontainer (CRM-05).");
    }

    [Fact]
    [Trait("Category", "RedPlaceholder")]
    [Trait("Category", "Phase06")]
    public void BookingCancelled_cascades_Status_to_UpcomingTripRow()
    {
        Assert.Fail("MISSING — requires MSSQL Testcontainer + MassTransitHarness (cancel cascade).");
    }

    [Fact]
    [Trait("Category", "RedPlaceholder")]
    [Trait("Category", "Phase06")]
    public void Pagination_respects_pageSize_and_returns_total_count()
    {
        Assert.Fail("MISSING — requires WebApplicationFactory + MSSQL Testcontainer (CRM-05).");
    }
}
