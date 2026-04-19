using Xunit;

namespace TBE.BackofficeService.Tests;

/// <summary>
/// D-39 — manual wallet credit (post-ticket refund via wallet) must use
/// the 4-eyes state machine (ops-finance opens, ops-admin approves,
/// self-approval forbidden). On approve PaymentService consumes
/// <c>WalletCreditApproved</c> and writes a <c>payment.WalletTransactions</c>
/// row of Kind=ManualCredit atomically; duplicate delivery is idempotent
/// via MassTransit inbox dedup. VALIDATION.md Task 6-01-04.
/// </summary>
public sealed class ManualWalletCreditFourEyesTests
{
    [Fact]
    [Trait("Category", "Phase06")]
    [Trait("Category", "RedPlaceholder")]
    public void Self_approval_returns_403_different_admin_approves_atomic_write_D39()
    {
        Assert.Fail(
            "MISSING — Plan 06-01 Task 6 implements WalletCreditRequestsController + " +
            "PaymentService WalletCreditApprovedConsumer. Amount CHECK (0.01, 100000); " +
            "ReasonCode CHECK in D-53 enum; 4-eyes self-approval 403 problem+json; " +
            "consumer writes Kind=ManualCredit idempotently.");
    }
}
