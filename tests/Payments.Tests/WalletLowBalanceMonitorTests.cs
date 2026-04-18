using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using TBE.Contracts.Messages;
using TBE.PaymentService.Application.Wallet;
using Xunit;

namespace Payments.Tests;

/// <summary>
/// Plan 05-03 Task 2 — low-balance monitor + consumer hysteresis.
///
/// RED-phase tests written against the (currently stubbed) WalletLowBalanceMonitor
/// and WalletLowBalanceConsumer types. Green-phase implementation lands in
/// the follow-up feat commit. The five facts cover:
///   - Monitor publishes WalletLowBalanceDetected for every snapshot returned
///     by IAgencyWalletRepository.ListAgenciesBelowThresholdAsync
///     (T-05-03-07 single-cross semantics — repo query already gates on
///      LowBalanceEmailSent = 0, so monitor publishes unconditionally per
///      snapshot returned).
///   - Monitor publishes nothing when the repo returns an empty list (balance
///     above threshold, or flag already set).
///   - Consumer resolves agent-admin contacts via IKeycloakB2BAdminClient,
///     dispatches e-mail via IWalletLowBalanceEmailSender, and flips
///     LowBalanceEmailSent + timestamps LastLowBalanceEmailAt on success.
///   - Consumer honours cooldown window (T-05-03-07 defence-in-depth): when a
///     second detected event arrives inside EmailCooldownHours of the last
///     send, the consumer skips the send but does NOT throw (MassTransit ack).
///   - Consumer only emails admins whose agency_id matches (T-05-03-11) —
///     the Keycloak client is the only source of recipients, never the
///     detected event's AgencyId fanout.
/// </summary>
public class WalletLowBalanceMonitorTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 05, 01, 12, 00, 00, TimeSpan.Zero);

    private static IOptionsMonitor<WalletOptions> Options(int cooldownHours = 24, int pollMinutes = 15)
    {
        var opts = new WalletOptions
        {
            LowBalance = new WalletLowBalanceOptions
            {
                DefaultThreshold = 500m,
                EmailCooldownHours = cooldownHours,
                PollIntervalMinutes = pollMinutes,
            },
        };
        var monitor = Substitute.For<IOptionsMonitor<WalletOptions>>();
        monitor.CurrentValue.Returns(opts);
        return monitor;
    }

    private static (WalletLowBalanceMonitor sut, IAgencyWalletRepository repo, IPublishEndpoint publish, TimeProvider clock)
        BuildMonitor(IReadOnlyList<AgencyBalanceSnapshot> snapshots)
    {
        var repo = Substitute.For<IAgencyWalletRepository>();
        repo.ListAgenciesBelowThresholdAsync(Arg.Any<CancellationToken>()).Returns(snapshots);

        var publish = Substitute.For<IPublishEndpoint>();

        var clock = Substitute.For<TimeProvider>();
        clock.GetUtcNow().Returns(FixedNow);

        var services = new ServiceCollection();
        services.AddScoped(_ => repo);
        services.AddScoped(_ => publish);
        var scopes = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

        var sut = new WalletLowBalanceMonitor(
            scopes,
            Options(),
            clock,
            NullLogger<WalletLowBalanceMonitor>.Instance);

        return (sut, repo, publish, clock);
    }

    [Fact(DisplayName = "T-05-03-07: Monitor publishes WalletLowBalanceDetected when balance below threshold and flag off")]
    public async Task Monitor_publishes_when_balance_below_threshold_and_flag_off()
    {
        var agencyId = Guid.NewGuid();
        var snap = new AgencyBalanceSnapshot(agencyId, Balance: 120.50m, Threshold: 500m, Currency: "GBP");
        var (sut, _, publish, _) = BuildMonitor(new[] { snap });

        await sut.TickAsync(CancellationToken.None);

        await publish.Received(1).Publish(
            Arg.Is<WalletLowBalanceDetected>(m =>
                m.AgencyId == agencyId &&
                m.BalanceAmount == 120.50m &&
                m.ThresholdAmount == 500m &&
                m.Currency == "GBP"),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "T-05-03-07: Monitor skips publish when repo returns no snapshots (flag on OR balance ok)")]
    public async Task Monitor_skips_when_repo_returns_empty()
    {
        var (sut, _, publish, _) = BuildMonitor(Array.Empty<AgencyBalanceSnapshot>());

        await sut.TickAsync(CancellationToken.None);

        await publish.DidNotReceiveWithAnyArgs().Publish<WalletLowBalanceDetected>(
            default!, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "T-05-03-07: Monitor fans out one WalletLowBalanceDetected per snapshot (multi-agency tick)")]
    public async Task Monitor_publishes_one_per_snapshot()
    {
        var a1 = Guid.NewGuid();
        var a2 = Guid.NewGuid();
        var snaps = new[]
        {
            new AgencyBalanceSnapshot(a1, 10m, 500m, "GBP"),
            new AgencyBalanceSnapshot(a2, 50m, 500m, "GBP"),
        };
        var (sut, _, publish, _) = BuildMonitor(snaps);

        await sut.TickAsync(CancellationToken.None);

        await publish.Received(1).Publish(
            Arg.Is<WalletLowBalanceDetected>(m => m.AgencyId == a1), Arg.Any<CancellationToken>());
        await publish.Received(1).Publish(
            Arg.Is<WalletLowBalanceDetected>(m => m.AgencyId == a2), Arg.Any<CancellationToken>());
    }

    // --------------------- Consumer tests ----------------------------------

    private static (WalletLowBalanceConsumer sut,
                    IAgencyWalletRepository repo,
                    IKeycloakB2BAdminClient keycloak,
                    IWalletLowBalanceEmailSender email,
                    TimeProvider clock)
        BuildConsumer(AgencyWallet? wallet, DateTimeOffset now, int cooldownHours = 24)
    {
        var repo = Substitute.For<IAgencyWalletRepository>();
        repo.GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(wallet);

        var keycloak = Substitute.For<IKeycloakB2BAdminClient>();
        keycloak.GetAgentAdminsForAgencyAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new[] { new AgentAdminContact("admin@agency.example", "Jane Admin") });

        var email = Substitute.For<IWalletLowBalanceEmailSender>();

        var clock = Substitute.For<TimeProvider>();
        clock.GetUtcNow().Returns(now);

        var sut = new WalletLowBalanceConsumer(
            repo,
            keycloak,
            email,
            Options(cooldownHours: cooldownHours),
            clock,
            NullLogger<WalletLowBalanceConsumer>.Instance);

        return (sut, repo, keycloak, email, clock);
    }

    private static ConsumeContext<WalletLowBalanceDetected> ConsumeCtx(WalletLowBalanceDetected msg)
    {
        var ctx = Substitute.For<ConsumeContext<WalletLowBalanceDetected>>();
        ctx.Message.Returns(msg);
        ctx.CancellationToken.Returns(CancellationToken.None);
        return ctx;
    }

    [Fact(DisplayName = "T-05-03-07: Consumer sends email, flips LowBalanceEmailSent, stamps LastLowBalanceEmailAt")]
    public async Task Consumer_sends_email_and_sets_flag()
    {
        var agencyId = Guid.NewGuid();
        var wallet = new AgencyWallet
        {
            Id = agencyId,
            AgencyId = agencyId,
            Currency = "GBP",
            LowBalanceThresholdAmount = 500m,
            LowBalanceEmailSent = false,
            LastLowBalanceEmailAtUtc = null,
        };
        var (sut, repo, keycloak, email, clock) = BuildConsumer(wallet, FixedNow);

        var msg = new WalletLowBalanceDetected(agencyId, 120m, 500m, "GBP", FixedNow.UtcDateTime);
        await sut.Consume(ConsumeCtx(msg));

        await keycloak.Received(1).GetAgentAdminsForAgencyAsync(agencyId, Arg.Any<CancellationToken>());
        await email.Received(1).SendLowBalanceEmailAsync(
            Arg.Is<IReadOnlyList<AgentAdminContact>>(l => l.Count == 1 && l[0].Email == "admin@agency.example"),
            agencyId,
            120m,
            500m,
            "GBP",
            Arg.Any<CancellationToken>());
        await repo.Received(1).MarkLowBalanceEmailSentAsync(
            agencyId,
            FixedNow.UtcDateTime,
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "T-05-03-07: Consumer respects cooldown — within cooldown window no email fires")]
    public async Task Consumer_respects_cooldown_window()
    {
        var agencyId = Guid.NewGuid();
        // Flag was flipped back to 0 (e.g. admin lowered threshold) but last email was only 2h ago;
        // cooldown is 24h so consumer MUST skip the send but not throw.
        var wallet = new AgencyWallet
        {
            Id = agencyId,
            AgencyId = agencyId,
            Currency = "GBP",
            LowBalanceThresholdAmount = 500m,
            LowBalanceEmailSent = false,
            LastLowBalanceEmailAtUtc = FixedNow.UtcDateTime.AddHours(-2),
        };
        var (sut, repo, _, email, _) = BuildConsumer(wallet, FixedNow, cooldownHours: 24);

        var msg = new WalletLowBalanceDetected(agencyId, 120m, 500m, "GBP", FixedNow.UtcDateTime);
        await sut.Consume(ConsumeCtx(msg));

        await email.DidNotReceiveWithAnyArgs().SendLowBalanceEmailAsync(
            default!, default, default, default, default!, default);
        await repo.DidNotReceiveWithAnyArgs().MarkLowBalanceEmailSentAsync(
            default, default, default);
    }

    [Fact(DisplayName = "T-05-03-11: Consumer only emails admins from the detected event's agency_id")]
    public async Task Consumer_only_emails_users_whose_agency_id_matches()
    {
        var agencyA = Guid.NewGuid();
        var walletA = new AgencyWallet
        {
            Id = agencyA,
            AgencyId = agencyA,
            Currency = "GBP",
            LowBalanceThresholdAmount = 500m,
            LowBalanceEmailSent = false,
            LastLowBalanceEmailAtUtc = null,
        };
        var (sut, _, keycloak, email, _) = BuildConsumer(walletA, FixedNow);

        var msg = new WalletLowBalanceDetected(agencyA, 100m, 500m, "GBP", FixedNow.UtcDateTime);
        await sut.Consume(ConsumeCtx(msg));

        // Keycloak lookup must use the event's agency id, never a hardcoded or
        // attacker-supplied value, and the email sender must only be called
        // with the list returned by that lookup.
        await keycloak.Received(1).GetAgentAdminsForAgencyAsync(agencyA, Arg.Any<CancellationToken>());
        await keycloak.DidNotReceive().GetAgentAdminsForAgencyAsync(
            Arg.Is<Guid>(g => g != agencyA),
            Arg.Any<CancellationToken>());
        await email.Received(1).SendLowBalanceEmailAsync(
            Arg.Any<IReadOnlyList<AgentAdminContact>>(),
            agencyA,
            Arg.Any<decimal>(),
            Arg.Any<decimal>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "T-05-03-07: Consumer publishes WalletLowBalanceDetected contract carries balance + threshold + currency")]
    public void Detected_contract_shape()
    {
        // Guardrail — TBE.Contracts.Messages.WalletLowBalanceDetected must remain
        // distinct from the pre-existing TBE.Contracts.Events.WalletLowBalance
        // (Phase 03-04, WalletId-only). This property-shape assertion fails the
        // build if someone collapses the two records in a refactor.
        var msg = new WalletLowBalanceDetected(Guid.NewGuid(), 100m, 500m, "GBP", FixedNow.UtcDateTime);
        msg.AgencyId.Should().NotBe(Guid.Empty);
        msg.BalanceAmount.Should().Be(100m);
        msg.ThresholdAmount.Should().Be(500m);
        msg.Currency.Should().Be("GBP");
        msg.DetectedAt.Kind.Should().Be(DateTimeKind.Utc);
    }
}
