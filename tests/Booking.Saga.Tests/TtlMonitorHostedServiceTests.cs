using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TBE.BookingService.Application.Saga;
using TBE.BookingService.Application.Ttl;
using TBE.BookingService.Infrastructure;
using TBE.BookingService.Infrastructure.Ttl;
using TBE.Contracts.Events;
using Xunit;

namespace Booking.Saga.Tests;

/// <summary>
/// Exercises <see cref="TtlMonitorHostedService"/>.PollOnceAsync against an in-memory
/// EF Core DbContext plus MassTransit's in-memory test harness. We assert that the
/// 24-h / 2-h advisories are emitted exactly once (Warn flags guard republish) and that
/// deadlines outside the watch windows produce no events.
/// </summary>
public sealed class TtlMonitorHostedServiceTests : IAsyncLifetime
{
    private ServiceProvider _sp = default!;
    private ITestHarness _harness = default!;

    public async Task InitializeAsync()
    {
        var dbName = $"ttl-monitor-{Guid.NewGuid()}";
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<BookingDbContext>(o =>
            o.UseInMemoryDatabase(dbName));
        services.Configure<TtlMonitorOptions>(o => o.PollInterval = TimeSpan.FromMinutes(5));

        services.AddMassTransitTestHarness();

        _sp = services.BuildServiceProvider(validateScopes: true);
        _harness = _sp.GetRequiredService<ITestHarness>();
        await _harness.Start();
    }

    public async Task DisposeAsync()
    {
        await _harness.Stop();
        await _sp.DisposeAsync();
    }

    private BookingDbContext Db() => _sp.CreateScope().ServiceProvider.GetRequiredService<BookingDbContext>();

    private TtlMonitorHostedService NewService() =>
        new(
            _sp.GetRequiredService<IServiceScopeFactory>(),
            _sp.GetRequiredService<IOptionsMonitor<TtlMonitorOptions>>(),
            NullLogger<TtlMonitorHostedService>.Instance);

    [Fact]
    public async Task PollOnceAsync_emits_24h_advisory_and_flips_flag_only_once()
    {
        using (var db = Db())
        {
            db.BookingSagaStates.Add(new BookingSagaState
            {
                CorrelationId = Guid.NewGuid(),
                CurrentState = 2,
                TicketingDeadlineUtc = DateTime.UtcNow.AddHours(20), // inside 24h, outside 2h
            });
            await db.SaveChangesAsync();
        }

        var svc = NewService();
        await svc.PollOnceAsync(default);
        await _harness.InactivityTask;
        (await _harness.Published.Any<TicketingDeadlineApproaching>(
            x => x.Context.Message.Horizon == "24h")).Should().BeTrue();

        // Second poll — flag was flipped, no new event expected.
        var countAfterFirst = _harness.Published.Select<TicketingDeadlineApproaching>().Count();
        await svc.PollOnceAsync(default);
        _harness.Published.Select<TicketingDeadlineApproaching>().Count().Should().Be(countAfterFirst);
    }

    [Fact]
    public async Task PollOnceAsync_emits_2h_advisory_independently_of_24h()
    {
        using (var db = Db())
        {
            db.BookingSagaStates.Add(new BookingSagaState
            {
                CorrelationId = Guid.NewGuid(),
                CurrentState = 2,
                TicketingDeadlineUtc = DateTime.UtcNow.AddMinutes(90), // inside 2h window
                Warn24HSent = true, // already sent earlier
            });
            await db.SaveChangesAsync();
        }

        var svc = NewService();
        await svc.PollOnceAsync(default);
        await _harness.InactivityTask;

        (await _harness.Published.Any<TicketingDeadlineApproaching>(
            x => x.Context.Message.Horizon == "2h")).Should().BeTrue();
    }

    [Fact]
    public async Task PollOnceAsync_skips_deadlines_outside_both_windows()
    {
        using (var db = Db())
        {
            db.BookingSagaStates.Add(new BookingSagaState
            {
                CorrelationId = Guid.NewGuid(),
                CurrentState = 2,
                TicketingDeadlineUtc = DateTime.UtcNow.AddDays(3), // >24h out
            });
            db.BookingSagaStates.Add(new BookingSagaState
            {
                CorrelationId = Guid.NewGuid(),
                CurrentState = 2,
                TicketingDeadlineUtc = DateTime.UtcNow.AddHours(-1), // already past
            });
            await db.SaveChangesAsync();
        }

        var svc = NewService();
        await svc.PollOnceAsync(default);
        await _harness.InactivityTask;
        _harness.Published.Select<TicketingDeadlineApproaching>().Count().Should().Be(0);
    }
}
