namespace TBE.NotificationService.API.Templates.Models;

public sealed record WalletLowBalanceModel(
    string AgencyName,
    decimal CurrentBalance,
    decimal Threshold,
    string Currency);
