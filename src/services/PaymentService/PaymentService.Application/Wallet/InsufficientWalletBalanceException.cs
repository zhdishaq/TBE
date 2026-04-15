namespace TBE.PaymentService.Application.Wallet;

public sealed class InsufficientWalletBalanceException : Exception
{
    public Guid WalletId { get; }
    public decimal AttemptedAmount { get; }
    public decimal AvailableBalance { get; }

    public InsufficientWalletBalanceException(Guid walletId, decimal attempted, decimal available)
        : base($"wallet {walletId} has {available} available but {attempted} was requested")
    {
        WalletId = walletId;
        AttemptedAmount = attempted;
        AvailableBalance = available;
    }
}
