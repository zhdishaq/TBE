using Cronos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TBE.PaymentService.Application.Reconciliation;

namespace TBE.PaymentService.Infrastructure.Reconciliation;

/// <summary>
/// Plan 06-02 Task 3 (BO-06) — nightly driver for
/// <see cref="IPaymentReconciliationService"/>.
///
/// <para>
/// Schedule: 02:00 UTC daily (cron <c>0 2 * * *</c>) via
/// <see cref="Cronos.CronExpression"/>. Overlap prevention is enforced
/// by a <see cref="SemaphoreSlim"/> with capacity 1 — if a previous
/// scan is still running we log a warning and skip the tick rather
/// than queuing, because the next day's run will pick up any rows
/// missed this cycle.
/// </para>
/// </summary>
public sealed class ReconciliationJob : BackgroundService
{
    private static readonly CronExpression Schedule =
        CronExpression.Parse("0 2 * * *", CronFormat.Standard);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _clock;
    private readonly ILogger<ReconciliationJob> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public ReconciliationJob(
        IServiceScopeFactory scopeFactory,
        TimeProvider clock,
        ILogger<ReconciliationJob> logger)
    {
        _scopeFactory = scopeFactory;
        _clock = clock;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("reconciliation-job-started schedule=\"0 2 * * * (UTC)\"");

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = _clock.GetUtcNow();
            var next = Schedule.GetNextOccurrence(now.UtcDateTime, TimeZoneInfo.Utc);
            if (next is null)
            {
                _logger.LogWarning("reconciliation-job-no-next-occurrence; sleeping 1h");
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
                continue;
            }

            var delay = next.Value - now.UtcDateTime;
            if (delay < TimeSpan.Zero) delay = TimeSpan.FromSeconds(1);

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }

            if (stoppingToken.IsCancellationRequested) break;

            if (!await _gate.WaitAsync(0, stoppingToken))
            {
                _logger.LogWarning("reconciliation-job-tick-skipped; previous scan still running");
                continue;
            }

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IPaymentReconciliationService>();
                await service.ScanAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // host is shutting down
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "reconciliation-job-tick-failed");
            }
            finally
            {
                _gate.Release();
            }
        }
    }
}
