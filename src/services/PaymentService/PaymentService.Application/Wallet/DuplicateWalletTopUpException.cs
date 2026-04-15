namespace TBE.PaymentService.Application.Wallet;

public sealed class DuplicateWalletTopUpException : Exception
{
    public Guid ExistingTxId { get; }

    public DuplicateWalletTopUpException(Guid existingTxId, Exception inner)
        : base("top-up idempotency key already applied", inner)
    {
        ExistingTxId = existingTxId;
    }
}
