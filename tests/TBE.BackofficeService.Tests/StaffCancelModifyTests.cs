using Xunit;

namespace TBE.BackofficeService.Tests;

/// <summary>
/// BO-03 — staff cancel/modify must use the 4-eyes state machine
/// (ops-cs opens, ops-admin approves, self-approval forbidden, expiry
/// enforced). Approval writes one BookingEvents row and publishes
/// <c>BookingCancellationApproved</c> via the EF outbox. VALIDATION.md
/// Task 6-01-03.
/// </summary>
public sealed class StaffCancelModifyTests
{
    [Fact]
    [Trait("Category", "Phase06")]
    [Trait("Category", "RedPlaceholder")]
    public void OpsCs_can_open_request_OpsAdmin_approves_logged_in_BookingEvents_BO03()
    {
        Assert.Fail(
            "MISSING — Plan 06-01 Task 6 implements StaffBookingActionsController " +
            "with 4-eyes POST /cancel (ops-cs) + POST /cancel/approve (ops-admin). " +
            "Self-approval must return 403 problem+json four-eyes-self-approval; " +
            "expiry past ExpiresAt must return 409 four-eyes-expired.");
    }
}
