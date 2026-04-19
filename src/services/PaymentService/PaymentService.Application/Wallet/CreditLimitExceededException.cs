namespace TBE.PaymentService.Application.Wallet;

/// <summary>
/// Plan 06-04 / CRM-02 / D-61 — raised by
/// <see cref="IWalletRepository.ReserveAsync"/> when the attempted
/// reserve exceeds <c>(balance + CreditLimit)</c>. Distinct from
/// <see cref="InsufficientWalletBalanceException"/> so the consumer
/// can surface a <c>/errors/wallet-credit-over-limit</c> (402)
/// problem+json rather than the classic
/// <c>/errors/wallet-insufficient-funds</c>.
/// </summary>
public sealed class CreditLimitExceededException : Exception
{
    public Guid WalletId { get; }
    public decimal AttemptedAmount { get; }
    public decimal Balance { get; }
    public decimal CreditLimit { get; }
    public decimal Available => Balance + CreditLimit;

    public CreditLimitExceededException(
        Guid walletId,
        decimal attempted,
        decimal balance,
        decimal creditLimit)
        : base($"wallet {walletId} has balance {balance:0.00} + creditLimit {creditLimit:0.00} = {balance + creditLimit:0.00} available; reserve of {attempted:0.00} would exceed the credit limit")
    {
        WalletId = walletId;
        AttemptedAmount = attempted;
        Balance = balance;
        CreditLimit = creditLimit;
    }
}
