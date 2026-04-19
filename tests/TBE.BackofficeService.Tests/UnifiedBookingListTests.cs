using Xunit;

namespace TBE.BackofficeService.Tests;

/// <summary>
/// BO-01 — unified booking list must return bookings across B2C, B2B,
/// and Manual channels with RBAC allowing all 4 ops-* roles read access.
/// Backoffice staff are not agency-scoped (cross-tenant read is the
/// intended behaviour). Filter by channel / status / free-text query.
/// VALIDATION.md Task 6-01-06.
/// </summary>
public sealed class UnifiedBookingListTests
{
    [Fact]
    [Trait("Category", "Phase06")]
    [Trait("Category", "RedPlaceholder")]
    public void Returns_B2C_B2B_Manual_channels_with_role_based_filter_BO01()
    {
        Assert.Fail(
            "MISSING — Plan 06-01 Task 7 implements BookingsController.GetList. " +
            "All 4 ops-* roles get 200. No agency_id filter (staff see everything). " +
            "Channel filter narrows to B2C/B2B/Manual; free-text matches PNR / name / " +
            "email / booking reference. Detail endpoint returns BookingEvents timeline.");
    }
}
