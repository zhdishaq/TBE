using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TBE.Contracts.Messages;

namespace TBE.PaymentService.Application.Wallet;

/// <summary>
/// Plan 05-03 Task 2 — BackgroundService that polls every
/// <c>WalletOptions.LowBalance.PollIntervalMinutes</c> and publishes a
/// <see cref="WalletLowBalanceDetected"/> for every agency whose
/// <c>SUM(SignedAmount) &lt; LowBalanceThresholdAmount</c> AND whose
/// <see cref="AgencyWallet.LowBalanceEmailSent"/> flag is <c>false</c>.
/// </summary>
/// <remarks>
/// Stub — real implementation lands in the Task 2 GREEN commit. Keeping the
/// type surface here so the RED test file compiles against the same shape
/// (dependencies, <c>TickAsync</c> test hook, ctor signature).
/// </remarks>
public sealed class WalletLowBalanceMonitor : BackgroundService
{
    public WalletLowBalanceMonitor(
        IServiceScopeFactory scopes,
        IOptionsMonitor<WalletOptions> options,
        TimeProvider clock,
        ILogger<WalletLowBalanceMonitor> log)
    {
        _ = scopes;
        _ = options;
        _ = clock;
        _ = log;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
        => throw new NotImplementedException(
            "Plan 05-03 Task 2 RED: WalletLowBalanceMonitor.ExecuteAsync not yet implemented.");

    /// <summary>Single poll tick — exposed for deterministic tests.</summary>
    public Task TickAsync(CancellationToken ct)
        => throw new NotImplementedException(
            "Plan 05-03 Task 2 RED: WalletLowBalanceMonitor.TickAsync not yet implemented.");
}
