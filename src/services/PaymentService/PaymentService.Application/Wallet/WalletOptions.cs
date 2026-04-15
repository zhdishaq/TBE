namespace TBE.PaymentService.Application.Wallet;

public sealed class WalletOptions
{
    public decimal LowBalanceThreshold { get; set; } = 500m;
    public string DefaultCurrency { get; set; } = "GBP";
}
