using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TBE.BookingService.Application.Saga;
using TBE.BookingService.Application.Ttl;
using TBE.Contracts.Events;

namespace TBE.BookingService.Infrastructure.Ttl;

/// <summary>
/// Background service that polls open sagas every <see cref="TtlMonitorOptions.PollInterval"/>
/// and emits advisory <see cref="TicketingDeadlineApproaching"/> events at the 24-hour and 2-hour
/// horizons. The Warn24HSent / Warn2HSent flags on <see cref="BookingSagaState"/> (owned by 03-01)
/// are flipped inside the same DB transaction as the publish so republish is idempotent even if
/// the poll iteration is retried.
///
/// **Advisory-only**: this service does NOT fire the hard-timeout compensation — that's driven by
/// the saga's MassTransit <c>Schedule</c> API against <c>TicketingDeadlineUtc - 2 minutes</c>
/// (D-04 / RESEARCH §"Pattern 6"). Keeping the two responsibilities split avoids race conditions
/// between the poll tick and the schedule token.
///
/// Registered as a hosted singleton; scope is created per iteration via
/// <see cref="IServiceScopeFactory"/> so <see cref="BookingDbContext"/> + <see cref="IPublishEndpoint"/>
/// are resolved at their correct (scoped) lifetime.
/// </summary>
public sealed class TtlMonitorHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<TtlMonitorOptions> _opts;
    private readonly ILogger<TtlMonitorHostedService> _log;

    public TtlMonitorHostedService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<TtlMonitorOptions> opts,
        ILogger<TtlMonitorHostedService> log)
    {
        _scopeFactory = scopeFactory;
        _opts = opts;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "TtlMonitor poll iteration failed");
            }

            try
            {
                await Task.Delay(_opts.CurrentValue.PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>
    /// One poll iteration. Exposed <c>internal</c> so unit tests can drive the loop deterministically
    /// without standing up a <see cref="BackgroundService"/> host.
    /// </summary>
    internal async Task PollOnceAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        var publish = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();
        var now = DateTime.UtcNow;
        var twoHoursOut = now.AddHours(2);
        var twentyFourHoursOut = now.AddHours(24);

        // 24h window: within the next 24h but still more than 2h away, and we haven't warned yet.
        var due24 = await db.BookingSagaStates
            .Where(s => s.TicketingDeadlineUtc > now
                     && s.TicketingDeadlineUtc <= twentyFourHoursOut
                     && s.TicketingDeadlineUtc > twoHoursOut
                     && !s.Warn24HSent)
            .ToListAsync(ct);

        foreach (var s in due24)
        {
            await publish.Publish(
                new TicketingDeadlineApproaching(s.CorrelationId, "24h", s.TicketingDeadlineUtc, DateTimeOffset.UtcNow),
                ct);
            s.Warn24HSent = true;
        }

        // 2h window: deadline within the next 2h and we haven't warned yet.
        var due2 = await db.BookingSagaStates
            .Where(s => s.TicketingDeadlineUtc > now
                     && s.TicketingDeadlineUtc <= twoHoursOut
                     && !s.Warn2HSent)
            .ToListAsync(ct);

        foreach (var s in due2)
        {
            await publish.Publish(
                new TicketingDeadlineApproaching(s.CorrelationId, "2h", s.TicketingDeadlineUtc, DateTimeOffset.UtcNow),
                ct);
            s.Warn2HSent = true;
        }

        if (due24.Count > 0 || due2.Count > 0)
        {
            await db.SaveChangesAsync(ct);
        }
    }
}
