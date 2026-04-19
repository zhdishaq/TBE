using Xunit;

namespace TBE.CrmService.Tests;

/// <summary>
/// Plan 06-04 / CRM-01 / D-51 — Customer 360 projection must build from
/// the 6 inbound events (BookingConfirmed, BookingCancelled, TicketIssued,
/// UserRegistered, WalletToppedUp, CustomerCommunicationLogged) with
/// InboxState-deduped MessageId handling so a replay of the same envelope
/// is idempotent.
///
/// <para>
/// Status: RED placeholder. Requires a live MSSQL Testcontainer + a
/// RabbitMQ harness to exercise the EF outbox InboxState row. Tagged
/// <c>Category=RedPlaceholder</c> so the CI baseline filter
/// (<c>--filter Category!=RedPlaceholder</c>) drops it. Removing the
/// attribute flips it to green once Docker is present in the CI image.
/// </para>
/// </summary>
public sealed class Customer360ProjectionTests
{
    [Fact]
    [Trait("Category", "RedPlaceholder")]
    [Trait("Category", "Phase06")]
    public void UserRegistered_seeds_CustomerProjection_with_email_and_name()
    {
        Assert.Fail("MISSING — requires MSSQL Testcontainer + MassTransitHarness infrastructure (CRM-01).");
    }

    [Fact]
    [Trait("Category", "RedPlaceholder")]
    [Trait("Category", "Phase06")]
    public void BookingConfirmed_bumps_LifetimeBookings_and_updates_LastBookingAt()
    {
        Assert.Fail("MISSING — requires MSSQL Testcontainer + MassTransitHarness infrastructure (CRM-01).");
    }

    [Fact]
    [Trait("Category", "RedPlaceholder")]
    [Trait("Category", "Phase06")]
    public void WalletToppedUp_updates_AgencyProjection_LastBookingAt_when_AgencyId_present()
    {
        Assert.Fail("MISSING — requires MSSQL Testcontainer + MassTransitHarness infrastructure (CRM-01).");
    }

    [Fact]
    [Trait("Category", "RedPlaceholder")]
    [Trait("Category", "Phase06")]
    public void Duplicate_MessageId_on_BookingConfirmed_is_deduped_via_InboxState()
    {
        Assert.Fail("MISSING — requires MSSQL Testcontainer + InboxState dedup path (D-51).");
    }

    [Fact]
    [Trait("Category", "RedPlaceholder")]
    [Trait("Category", "Phase06")]
    public void Customer_detail_endpoint_returns_bookings_list_and_lifetime_stats()
    {
        Assert.Fail("MISSING — requires MSSQL Testcontainer + WebApplicationFactory (CRM-02).");
    }
}
