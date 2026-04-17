namespace TBE.PaymentService.Application.Wallet;

/// <summary>
/// Raised by <c>WalletTopUpService.CreateTopUpIntentAsync</c> when the requested amount
/// falls outside the admin-configured <c>[MinAmount, MaxAmount]</c> range (D-40).
///
/// The controller layer maps this to <c>application/problem+json</c> with
/// type <c>/errors/wallet-topup-out-of-range</c> and HTTP 400 (T-05-03-03).
/// </summary>
public sealed class WalletTopUpOutOfRangeException : Exception
{
    public decimal Min { get; }
    public decimal Max { get; }
    public decimal Requested { get; }
    public string Currency { get; }

    public WalletTopUpOutOfRangeException(decimal min, decimal max, decimal requested, string currency)
        : base($"Top-up amount {requested:N2} {currency} is outside allowed range [{min:N2}, {max:N2}].")
    {
        Min = min;
        Max = max;
        Requested = requested;
        Currency = currency;
    }
}
