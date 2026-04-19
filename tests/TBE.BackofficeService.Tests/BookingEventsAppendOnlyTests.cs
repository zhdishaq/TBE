using Xunit;

namespace TBE.BackofficeService.Tests;

/// <summary>
/// BO-04 / D-49 — dbo.BookingEvents must reject UPDATE and DELETE at the
/// SQL Server engine level via the <c>booking_events_writer</c> DENY role
/// grant. VALIDATION.md Task 6-01-01.
/// </summary>
public sealed class BookingEventsAppendOnlyTests
{
    [Fact]
    [Trait("Category", "Phase06")]
    [Trait("Category", "RedPlaceholder")]
    public void UpdateAndDelete_are_rejected_at_engine_level_D49()
    {
        Assert.Fail(
            "MISSING — Plan 06-01 Task 5 implements DENY role grant migration " +
            "(20260601100001_AddAppendOnlyRoleGrants) and validates that " +
            "UPDATE/DELETE on dbo.BookingEvents as tbe_booking_app throws " +
            "SqlException with error number 229 (permission denied).");
    }
}
