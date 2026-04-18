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
/// <para>
/// The monitor deliberately does NOT e-mail. Email delivery + flag-flip
/// belong to <see cref="WalletLowBalanceConsumer"/> (T-05-03-07 separation
/// of concerns — retry semantics flow through MassTransit).
/// </para>
/// <para>
/// Cadence is read from <see cref="IOptionsMonitor{T}.CurrentValue"/> on
/// every tick, so admins can adjust <c>PollIntervalMinutes</c> at runtime
/// without restarting PaymentService.
/// </para>
/// </remarks>
public sealed class WalletLowBalanceMonitor : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IOptionsMonitor<WalletOptions> _options;
    private readonly TimeProvider _clock;
    private readonly ILogger<WalletLowBalanceMonitor> _log;

    public WalletLowBalanceMonitor(
        IServiceScopeFactory scopes,
        IOptionsMonitor<WalletOptions> options,
        TimeProvider clock,
        ILogger<WalletLowBalanceMonitor> log)
    {
        _scopes = scopes;
        _options = options;
        _clock = clock;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TickAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "WalletLowBalanceMonitor tick failed; will retry on next interval");
            }

            var interval = TimeSpan.FromMinutes(
                Math.Max(1, _options.CurrentValue.LowBalance.PollIntervalMinutes));
            try
            {
                await Task.Delay(interval, _clock, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }
    }

    /// <summary>Single poll tick — exposed public so tests can step the
    /// monitor deterministically without spinning up the
    /// <see cref="BackgroundService"/> host loop.</summary>
    public async Task TickAsync(CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IAgencyWalletRepository>();
        var publish = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

        var snapshots = await repo.ListAgenciesBelowThresholdAsync(ct).ConfigureAwait(false);
        if (snapshots.Count == 0)
        {
            return;
        }

        var detectedAt = _clock.GetUtcNow().UtcDateTime;
        foreach (var snap in snapshots)
        {
            await publish.Publish(
                new WalletLowBalanceDetected(
                    snap.AgencyId,
                    snap.Balance,
                    snap.Threshold,
                    snap.Currency,
                    detectedAt),
                ct).ConfigureAwait(false);

            _log.LogInformation(
                "wallet low-balance detected agency={AgencyId} balance={Balance} threshold={Threshold} currency={Currency}",
                snap.AgencyId, snap.Balance, snap.Threshold, snap.Currency);
        }
    }
}
