using Xunit;

namespace TBE.BookingService.Tests;

/// <summary>
/// Red placeholders for Plan 05-02 Task 2 (saga IfElse branch at PnrCreated)
/// and Plan 05-02 Task 3 (pre-ticket compensation). Covers D-24 (wallet
/// reserve on B2B) and D-39 (refund release only applies pre-TicketIssued).
/// </summary>
public class BookingSagaB2BBranchTests
{
    /// <summary>D-24 / 05-RESEARCH Example 1 — B2B branch must publish WalletReserveCommand, not AuthorizePaymentCommand.</summary>
    [Fact]
    [Trait("Category", "RedPlaceholder")]
    public void PnrCreated_publishes_WalletReserveCommand_when_Channel_is_B2B()
    {
        Assert.Fail("MISSING — Plan 05-02 Task 2 (B2B branch at PnrCreated; WalletReserveCommand).");
    }

    /// <summary>B2C flow is unchanged — still publishes AuthorizePaymentCommand at PnrCreated.</summary>
    [Fact]
    [Trait("Category", "RedPlaceholder")]
    public void PnrCreated_publishes_AuthorizePaymentCommand_when_Channel_is_B2C()
    {
        Assert.Fail("MISSING — Plan 05-02 Task 2 (B2C branch unchanged; AuthorizePaymentCommand).");
    }

    /// <summary>D-39 — compensation publishes WalletReleaseCommand on pre-ticket failure only; post-TicketIssued is manual Phase 6.</summary>
    [Fact]
    [Trait("Category", "RedPlaceholder")]
    public void Compensation_publishes_WalletReleaseCommand_on_pre_ticket_failure_and_not_after_TicketIssued()
    {
        Assert.Fail("MISSING — Plan 05-02 Task 3 (D-39 pre-ticket-only WalletReleaseCommand).");
    }
}
