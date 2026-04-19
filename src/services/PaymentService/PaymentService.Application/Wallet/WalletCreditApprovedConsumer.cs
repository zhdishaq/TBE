using MassTransit;
using Microsoft.Extensions.Logging;
using TBE.Contracts.Events;

namespace TBE.PaymentService.Application.Wallet;

/// <summary>
/// Plan 06-01 Task 6 (D-39) — sole consumer of <see cref="WalletCreditApproved"/>.
/// Reacts after BackofficeService has committed the 4-eyes approval row
/// flip + outbox publish (Plan 03-01 pattern), and writes a single
/// <c>payment.WalletTransactions</c> row of <see cref="WalletEntryType.ManualCredit"/>
/// with a positive <c>SignedAmount</c>.
///
/// <para>
/// Idempotency is double-guarded:
///   1. MassTransit InboxState dedup on MessageId (first line of defence
///      — a redelivery of the same envelope is short-circuited before
///      this method runs).
///   2. Unique IdempotencyKey <c>manual-credit-{requestId}</c> on the
///      ledger row (belt-and-braces if the outbox is ever bypassed or if
///      the InboxState table is truncated).
/// </para>
///
/// <para>
/// Per D-39, this is the ONLY path to a ManualCredit ledger entry. The
/// Stripe refund path is explicitly not implemented in v1 — post-ticket
/// refunds flow through this 4-eyes → wallet-credit surface so ops has
/// full control over counterparty risk and FX timing.
/// </para>
/// </summary>
public sealed class WalletCreditApprovedConsumer : IConsumer<WalletCreditApproved>
{
    private readonly IWalletRepository _wallet;
    private readonly ILogger<WalletCreditApprovedConsumer> _log;

    public WalletCreditApprovedConsumer(
        IWalletRepository wallet,
        ILogger<WalletCreditApprovedConsumer> log)
    {
        _wallet = wallet;
        _log = log;
    }

    public async Task Consume(ConsumeContext<WalletCreditApproved> ctx)
    {
        var e = ctx.Message;
        var idemKey = $"manual-credit-{e.RequestId}";

        var txId = await _wallet.ManualCreditAsync(
            walletId: e.AgencyId,
            amount: e.Amount,
            currency: e.Currency,
            idempotencyKey: idemKey,
            linkedBookingId: e.LinkedBookingId,
            approvedBy: e.ApprovedBy,
            approvalNotes: e.ApprovalNotes,
            ct: ctx.CancellationToken);

        _log.LogInformation(
            "wallet-credit-approved consumed requestId={RequestId} agency={AgencyId} amount={Amount} {Currency} txId={TxId} approvedBy={ApprovedBy}",
            e.RequestId, e.AgencyId, e.Amount, e.Currency, txId, e.ApprovedBy);
    }
}
