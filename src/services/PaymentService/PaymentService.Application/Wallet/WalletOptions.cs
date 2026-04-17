namespace TBE.PaymentService.Application.Wallet;

/// <summary>
/// Wallet runtime configuration bound from <c>appsettings.json</c> section <c>Wallet</c>.
/// Admins can flip any of these values at runtime (hot-reload via
/// <see cref="Microsoft.Extensions.Options.IOptionsMonitor{T}"/>) without restarting the
/// PaymentService — no compile-time caps per D-40.
/// </summary>
public sealed class WalletOptions
{
    /// <summary>
    /// DEPRECATED: legacy flat default threshold. Retained for
    /// backwards compatibility with earlier consumers. New code should read
    /// <see cref="LowBalance"/>.<see cref="WalletLowBalanceOptions.DefaultThreshold"/> instead.
    /// </summary>
    public decimal LowBalanceThreshold { get; set; } = 500m;

    /// <summary>
    /// DEPRECATED: legacy flat default currency. New code should read
    /// <see cref="TopUp"/>.<see cref="WalletTopUpOptions.Currency"/>.
    /// </summary>
    public string DefaultCurrency { get; set; } = "GBP";

    /// <summary>
    /// Top-up amount bounds (D-40). Enforced by <c>WalletTopUpService</c> BEFORE
    /// any Stripe PaymentIntent is created.
    /// </summary>
    public WalletTopUpOptions TopUp { get; set; } = new();

    /// <summary>
    /// Low-balance monitor cadence + cooldown (B2B-06).
    /// </summary>
    public WalletLowBalanceOptions LowBalance { get; set; } = new();
}

/// <summary>
/// D-40 top-up cap configuration. Bound from <c>Wallet:TopUp</c>.
/// </summary>
public sealed class WalletTopUpOptions
{
    /// <summary>Smallest top-up amount in major units (e.g. £10.00).</summary>
    public decimal MinAmount { get; set; } = 10m;

    /// <summary>Largest top-up amount in major units (e.g. £50 000.00).</summary>
    public decimal MaxAmount { get; set; } = 50_000m;

    /// <summary>ISO 4217 currency code (e.g. <c>GBP</c>).</summary>
    public string Currency { get; set; } = "GBP";
}

/// <summary>
/// Low-balance monitoring configuration. Bound from <c>Wallet:LowBalance</c>.
/// </summary>
public sealed class WalletLowBalanceOptions
{
    /// <summary>Default alert threshold for agencies that haven't configured a custom one.</summary>
    public decimal DefaultThreshold { get; set; } = 500m;

    /// <summary>Minimum hours between successive low-balance e-mails to the same agency.</summary>
    public int EmailCooldownHours { get; set; } = 24;

    /// <summary>How often the monitor background service polls for low balances.</summary>
    public int PollIntervalMinutes { get; set; } = 15;
}
