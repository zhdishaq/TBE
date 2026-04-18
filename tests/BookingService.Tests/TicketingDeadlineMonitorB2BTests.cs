using FluentAssertions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using TBE.BookingService.Application.Saga;
using TBE.BookingService.Application.Ttl;
using TBE.BookingService.Infrastructure;
using TBE.BookingService.Infrastructure.Ttl;
using TBE.Contracts.Enums;
using TBE.Contracts.Events;
using TBE.Contracts.Messages;
using Xunit;

namespace TBE.BookingService.Tests;

/// <summary>
/// Plan 05-04 Task 1 — TTL monitor B2B publish extension.
///
/// Asserts the <see cref="TtlMonitorHostedService"/> publishes the Plan 05-04
/// <see cref="TicketingDeadlineWarning"/> / <see cref="TicketingDeadlineUrgent"/>
/// contracts ALONGSIDE the existing Phase-3
/// <see cref="TicketingDeadlineApproaching"/> contract whenever the underlying
/// saga carries <see cref="Channel.B2B"/> and <see cref="BookingSagaState.AgencyId"/>
/// is non-null. This satisfies B2B-09 (TTL email alerts to agency admins) while
/// preserving the Phase-3 B2C B2C-customer-advisory flow unchanged.
///
/// Crash-safety (T-05-04-07): the monitor flips the <c>Warn24HSent</c> /
/// <c>Warn2HSent</c> flag on the saga state and persists in the same
/// DbContext save-changes cycle as the publish queue. EF + MassTransit outbox
/// (Plan 03-01 infrastructure) ensures either both commit or neither.
/// </summary>
public class TicketingDeadlineMonitorB2BTests
{
    private static readonly Guid AgencyIdSample = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private static ServiceProvider BuildProvider(BookingDbContext db, IPublishEndpoint publish)
    {
        var services = new ServiceCollection();
        services.AddSingleton(db);
        services.AddSingleton(publish);
        return services.BuildServiceProvider();
    }

    private static TtlMonitorHostedService NewMonitor(ServiceProvider provider)
    {
        var opts = Options.Create(new TtlMonitorOptions { PollInterval = TimeSpan.FromMinutes(5) });
        var monitor = new TtlMonitorHostedService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new OptionsMonitorStub<TtlMonitorOptions>(opts.Value),
            NullLogger<TtlMonitorHostedService>.Instance);
        return monitor;
    }

    private static BookingSagaState B2BSaga(DateTime deadline, string pnr = "ABC123") => new()
    {
        CorrelationId = Guid.NewGuid(),
        AgencyId = AgencyIdSample,
        Channel = Channel.B2B,
        ChannelText = "b2b",
        BookingReference = "TBE-260530-B2B",
        ProductType = "flight",
        Currency = "GBP",
        GdsPnr = pnr,
        PaymentMethod = "wallet",
        TicketingDeadlineUtc = deadline,
        InitiatedAtUtc = DateTime.UtcNow.AddMinutes(-30),
        CustomerName = "Jane Customer",
    };

    private static BookingDbContext NewDb() =>
        new(new DbContextOptionsBuilder<BookingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task B2B_saga_at_23h_publishes_TicketingDeadlineWarning_with_agency_id()
    {
        await using var db = NewDb();
        db.BookingSagaStates.Add(B2BSaga(DateTime.UtcNow.AddHours(23)));
        await db.SaveChangesAsync();

        var publish = Substitute.For<IPublishEndpoint>();
        using var provider = BuildProvider(db, publish);

        var monitor = NewMonitor(provider);
        await MonitorHelpers.PollOnceAsync(monitor, CancellationToken.None);

        await publish.Received(1).Publish(
            Arg.Is<TicketingDeadlineWarning>(m => m.AgencyId == AgencyIdSample && m.Pnr == "ABC123"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task B2B_saga_at_90min_publishes_TicketingDeadlineUrgent_with_agency_id()
    {
        await using var db = NewDb();
        db.BookingSagaStates.Add(B2BSaga(DateTime.UtcNow.AddMinutes(90)));
        await db.SaveChangesAsync();

        var publish = Substitute.For<IPublishEndpoint>();
        using var provider = BuildProvider(db, publish);

        var monitor = NewMonitor(provider);
        await MonitorHelpers.PollOnceAsync(monitor, CancellationToken.None);

        await publish.Received(1).Publish(
            Arg.Is<TicketingDeadlineUrgent>(m => m.AgencyId == AgencyIdSample),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task B2C_saga_does_not_publish_B2B_specific_warning_contract()
    {
        await using var db = NewDb();
        var b2cSaga = B2BSaga(DateTime.UtcNow.AddHours(23));
        b2cSaga.Channel = Channel.B2C;
        b2cSaga.ChannelText = "b2c";
        b2cSaga.AgencyId = null;
        db.BookingSagaStates.Add(b2cSaga);
        await db.SaveChangesAsync();

        var publish = Substitute.For<IPublishEndpoint>();
        using var provider = BuildProvider(db, publish);

        var monitor = NewMonitor(provider);
        await MonitorHelpers.PollOnceAsync(monitor, CancellationToken.None);

        // Phase 3 contract still fires for B2C
        await publish.Received().Publish(Arg.Any<TicketingDeadlineApproaching>(), Arg.Any<CancellationToken>());
        // But the Plan 05-04 B2B-flavoured contracts must NOT
        await publish.DidNotReceive().Publish(Arg.Any<TicketingDeadlineWarning>(), Arg.Any<CancellationToken>());
        await publish.DidNotReceive().Publish(Arg.Any<TicketingDeadlineUrgent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task B2B_saga_with_flag_already_set_does_not_republish_warning()
    {
        await using var db = NewDb();
        var saga = B2BSaga(DateTime.UtcNow.AddHours(23));
        saga.Warn24HSent = true;
        db.BookingSagaStates.Add(saga);
        await db.SaveChangesAsync();

        var publish = Substitute.For<IPublishEndpoint>();
        using var provider = BuildProvider(db, publish);

        var monitor = NewMonitor(provider);
        await MonitorHelpers.PollOnceAsync(monitor, CancellationToken.None);

        await publish.DidNotReceive().Publish(Arg.Any<TicketingDeadlineWarning>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Contracts_differ_in_record_type()
    {
        // Guardrail — the two contracts must NOT collapse into a single record in a future refactor.
        // This ensures MassTransit routes warn emails and urgent emails to distinct consumers.
        var warningType = typeof(TicketingDeadlineWarning);
        var urgentType = typeof(TicketingDeadlineUrgent);
        warningType.Should().NotBe(urgentType);
        warningType.FullName.Should().NotBe(urgentType.FullName);
    }
}

/// <summary>
/// <see cref="IOptionsMonitor{T}"/> stub returning a fixed value and ignoring change events.
/// </summary>
internal sealed class OptionsMonitorStub<T> : IOptionsMonitor<T>
{
    public OptionsMonitorStub(T value) => CurrentValue = value;
    public T CurrentValue { get; }
    public T Get(string? name) => CurrentValue;
    public IDisposable OnChange(Action<T, string?> listener) => NullDisposable.Instance;
    private sealed class NullDisposable : IDisposable
    {
        public static readonly NullDisposable Instance = new();
        public void Dispose() { }
    }
}

internal static class MonitorHelpers
{
    /// <summary>
    /// Reflectively invokes <c>PollOnceAsync</c> which is declared <c>internal</c> on the
    /// production monitor so tests can drive one iteration without running the loop.
    /// Kept as reflection to preserve the production API surface.
    /// </summary>
    public static Task PollOnceAsync(TtlMonitorHostedService monitor, CancellationToken ct)
    {
        var method = typeof(TtlMonitorHostedService).GetMethod(
            "PollOnceAsync",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Public);
        if (method is null)
            throw new InvalidOperationException("PollOnceAsync not found on TtlMonitorHostedService");
        return (Task)method.Invoke(monitor, new object[] { ct })!;
    }
}
