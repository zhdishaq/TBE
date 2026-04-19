using Xunit;

namespace TBE.BackofficeService.Tests;

/// <summary>
/// BO-05 — every BookingSaga state transition must write a BookingEvents
/// row with a non-empty Snapshot JSON envelope that includes the pricing
/// breakdown and supplier response. VALIDATION.md Task 6-01-02.
/// </summary>
public sealed class BookingEventsSnapshotTests
{
    [Fact]
    [Trait("Category", "Phase06")]
    [Trait("Category", "RedPlaceholder")]
    public void Every_event_persists_full_snapshot_json_BO05()
    {
        Assert.Fail(
            "MISSING — Plan 06-01 Task 5 wires BookingEventsWriter into BookingSaga " +
            "and records BookingInitiated / PriceReconfirmed / PNRCreated / " +
            "PaymentAuthorized / TicketIssued / PaymentCaptured / BookingConfirmed " +
            "with Snapshot { BookingId, Channel, Status, PricingBreakdown{...}, " +
            "SupplierResponse{...} } per D-50.");
    }
}
