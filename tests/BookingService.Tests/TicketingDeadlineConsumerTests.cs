using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TBE.BookingService.Application.Consumers;
using TBE.BookingService.Application.Keycloak;
using TBE.Contracts.Messages;
using Xunit;

namespace TBE.BookingService.Tests;

/// <summary>
/// Plan 05-04 Task 1 (B2B-09) — TicketingDeadlineConsumer fan-out asserts.
///
/// <para>
/// <b>What's covered:</b>
/// <list type="bullet">
///   <item>24h Warning event → IKeycloakB2BAdminClient resolves recipients,
///         ITicketingDeadlineEmailSender receives them with
///         <see cref="TicketingDeadlineHorizon.Warning"/>.</item>
///   <item>2h Urgent event → same path but with
///         <see cref="TicketingDeadlineHorizon.Urgent"/>.</item>
///   <item>Empty-recipients fallback — consumer ACKs without throwing, sender
///         is NEVER called (T-05-04-07 defence-in-depth).</item>
///   <item>Anti-spoofing — recipients resolved via AgencyId from message, NOT
///         from any pre-supplied list.</item>
/// </list>
/// </para>
/// </summary>
public class TicketingDeadlineConsumerTests
{
    private static readonly Guid BookingId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid AgencyId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    [Fact]
    public async Task Consume_Warning_fans_out_with_warning_horizon()
    {
        var keycloak = Substitute.For<IKeycloakB2BAdminClient>();
        keycloak.GetAgentContactsForAgencyAsync(AgencyId, Arg.Any<CancellationToken>())
            .Returns(new List<AgentContact>
            {
                new("admin@acme-travel.com", "Alice Admin"),
                new("agent@acme-travel.com", "Bob Agent"),
            });
        var sender = Substitute.For<ITicketingDeadlineEmailSender>();

        await using var provider = new ServiceCollection()
            .AddSingleton(keycloak)
            .AddSingleton(sender)
            .AddSingleton<TicketingDeadlineConsumer>()
            .AddSingleton(NullLogger<TicketingDeadlineConsumer>.Instance)
            .AddMassTransitTestHarness(x => x.AddConsumer<TicketingDeadlineConsumer>())
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        try
        {
            var msg = new TicketingDeadlineWarning(
                BookingId: BookingId,
                AgencyId: AgencyId,
                Pnr: "PNR777",
                TicketingTimeLimit: DateTime.UtcNow.AddHours(23),
                HoursRemaining: 23m,
                ClientName: "Jane Doe");

            await harness.Bus.Publish(msg);
            (await harness.Consumed.Any<TicketingDeadlineWarning>()).Should().BeTrue();

            await keycloak.Received(1)
                .GetAgentContactsForAgencyAsync(AgencyId, Arg.Any<CancellationToken>());
            await sender.Received(1).SendDeadlineEmailAsync(
                Arg.Is<IReadOnlyList<AgentContact>>(r => r.Count == 2),
                TicketingDeadlineHorizon.Warning,
                BookingId,
                AgencyId,
                "PNR777",
                Arg.Any<DateTime>(),
                23m,
                "Jane Doe",
                Arg.Any<CancellationToken>());
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task Consume_Urgent_fans_out_with_urgent_horizon()
    {
        var keycloak = Substitute.For<IKeycloakB2BAdminClient>();
        keycloak.GetAgentContactsForAgencyAsync(AgencyId, Arg.Any<CancellationToken>())
            .Returns(new List<AgentContact>
            {
                new("admin@acme-travel.com", "Alice Admin"),
            });
        var sender = Substitute.For<ITicketingDeadlineEmailSender>();

        await using var provider = new ServiceCollection()
            .AddSingleton(keycloak)
            .AddSingleton(sender)
            .AddSingleton<TicketingDeadlineConsumer>()
            .AddSingleton(NullLogger<TicketingDeadlineConsumer>.Instance)
            .AddMassTransitTestHarness(x => x.AddConsumer<TicketingDeadlineConsumer>())
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        try
        {
            var msg = new TicketingDeadlineUrgent(
                BookingId: BookingId,
                AgencyId: AgencyId,
                Pnr: "PNR777",
                TicketingTimeLimit: DateTime.UtcNow.AddHours(1.5),
                HoursRemaining: 1.5m,
                ClientName: null);

            await harness.Bus.Publish(msg);
            (await harness.Consumed.Any<TicketingDeadlineUrgent>()).Should().BeTrue();

            await sender.Received(1).SendDeadlineEmailAsync(
                Arg.Any<IReadOnlyList<AgentContact>>(),
                TicketingDeadlineHorizon.Urgent,
                BookingId,
                AgencyId,
                "PNR777",
                Arg.Any<DateTime>(),
                1.5m,
                null,
                Arg.Any<CancellationToken>());
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task Consume_without_eligible_recipients_does_not_call_sender()
    {
        var keycloak = Substitute.For<IKeycloakB2BAdminClient>();
        keycloak.GetAgentContactsForAgencyAsync(AgencyId, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<AgentContact>());
        var sender = Substitute.For<ITicketingDeadlineEmailSender>();

        await using var provider = new ServiceCollection()
            .AddSingleton(keycloak)
            .AddSingleton(sender)
            .AddSingleton<TicketingDeadlineConsumer>()
            .AddSingleton(NullLogger<TicketingDeadlineConsumer>.Instance)
            .AddMassTransitTestHarness(x => x.AddConsumer<TicketingDeadlineConsumer>())
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        try
        {
            var msg = new TicketingDeadlineWarning(
                BookingId: BookingId,
                AgencyId: AgencyId,
                Pnr: "PNR777",
                TicketingTimeLimit: DateTime.UtcNow.AddHours(23),
                HoursRemaining: 23m,
                ClientName: null);

            await harness.Bus.Publish(msg);
            (await harness.Consumed.Any<TicketingDeadlineWarning>()).Should().BeTrue();

            await sender.DidNotReceive().SendDeadlineEmailAsync(
                Arg.Any<IReadOnlyList<AgentContact>>(),
                Arg.Any<TicketingDeadlineHorizon>(),
                Arg.Any<Guid>(),
                Arg.Any<Guid>(),
                Arg.Any<string>(),
                Arg.Any<DateTime>(),
                Arg.Any<decimal>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>());
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task Consume_resolves_recipients_via_agency_id_from_message_not_body()
    {
        // T-05-04-07 analog — if the message body carried a recipient list
        // the consumer MUST ignore it and use AgencyId to re-resolve from
        // Keycloak. Our message contract doesn't expose a recipient-list
        // field at all, but this test documents the invariant by showing
        // the consumer calls the Keycloak client with the exact AgencyId
        // from the message (and nothing else).
        var keycloak = Substitute.For<IKeycloakB2BAdminClient>();
        keycloak.GetAgentContactsForAgencyAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new List<AgentContact> { new("admin@acme.com", "A") });
        var sender = Substitute.For<ITicketingDeadlineEmailSender>();

        await using var provider = new ServiceCollection()
            .AddSingleton(keycloak)
            .AddSingleton(sender)
            .AddSingleton<TicketingDeadlineConsumer>()
            .AddSingleton(NullLogger<TicketingDeadlineConsumer>.Instance)
            .AddMassTransitTestHarness(x => x.AddConsumer<TicketingDeadlineConsumer>())
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        try
        {
            var msg = new TicketingDeadlineWarning(
                BookingId: BookingId,
                AgencyId: AgencyId,
                Pnr: "PNR777",
                TicketingTimeLimit: DateTime.UtcNow.AddHours(23),
                HoursRemaining: 23m,
                ClientName: null);

            await harness.Bus.Publish(msg);
            (await harness.Consumed.Any<TicketingDeadlineWarning>()).Should().BeTrue();

            await keycloak.Received(1)
                .GetAgentContactsForAgencyAsync(AgencyId, Arg.Any<CancellationToken>());
        }
        finally
        {
            await harness.Stop();
        }
    }
}
