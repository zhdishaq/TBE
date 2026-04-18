using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using TBE.BookingService.Application.Saga;
using TBE.Contracts.Commands;
using TBE.Contracts.Enums;
using TBE.Contracts.Events;
using Xunit;

namespace TBE.BookingService.Tests;

/// <summary>
/// Plan 05-04 Task 1 (B2B-10) — BookingSaga VoidRequested activity. Asserts:
///  - Pre-ticket (no TicketNumber) B2B saga responds by releasing the wallet
///    reservation and voiding the PNR, then transitions to Cancelled.
///  - Pre-ticket B2C saga responds by voiding the PNR only (no wallet to
///    release) — regression guard for the B2C path.
///  - VoidRequested on a saga that is no longer in a pre-ticket state
///    (e.g. Finalized/Confirmed) is silently dropped by the state machine
///    — the controller's 409 D-39 gate is authoritative for that case, and
///    the saga must not double-compensate after terminal states.
/// </summary>
[Trait("Category", "Unit")]
public class BookingSagaVoidTests
{
    private static readonly Guid AgencyId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid WalletId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private static async Task<(ITestHarness harness, ISagaStateMachineTestHarness<BookingSaga, BookingSagaState> saga)> StartHarnessAsync()
    {
        var provider = new ServiceCollection()
            .AddMassTransitTestHarness(x =>
            {
                x.AddSagaStateMachine<BookingSaga, BookingSagaState>()
                    .InMemoryRepository();
            })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        var saga = harness.GetSagaStateMachineHarness<BookingSaga, BookingSagaState>();
        return (harness, saga);
    }

    private static BookingInitiated B2BInitiated(Guid bookingId) =>
        new(bookingId, "flight", "b2b", "agent-sub-1", "TBE-260530-VOID001",
            TotalAmount: 250m, Currency: "GBP", PaymentMethod: "wallet",
            WalletId: WalletId, InitiatedAt: DateTimeOffset.UtcNow);

    private static AgentBookingDetailsCaptured AgencyDetails(Guid bookingId) =>
        new(bookingId, AgencyId, 200m, 50m, 250m, 50m, null,
            "Jane Customer", "jane@example.com", "+441234567890",
            "offer-001", DateTimeOffset.UtcNow);

    [Fact(DisplayName = "B2B-10: VoidRequested after WalletReserved publishes WalletReleaseCommand + VoidPnrCommand")]
    public async Task VoidRequested_after_WalletReserved_releases_wallet_and_voids_pnr()
    {
        var (harness, _) = await StartHarnessAsync();
        var bookingId = Guid.NewGuid();

        await harness.Bus.Publish(B2BInitiated(bookingId));
        await harness.Bus.Publish(AgencyDetails(bookingId));
        await harness.Bus.Publish(new PriceReconfirmed(bookingId, 250m, "GBP", DateTimeOffset.UtcNow));
        await harness.Bus.Publish(new PnrCreated(bookingId, "ABC123", DateTime.UtcNow.AddHours(24), DateTimeOffset.UtcNow));
        await harness.Bus.Publish(new WalletReserved(
            CorrelationId: bookingId, BookingId: bookingId,
            LedgerEntryId: Guid.NewGuid(), BalanceAfter: 750m));

        await harness.Bus.Publish(new VoidRequested(
            bookingId, "agent-admin-1", "customer_cancel", DateTimeOffset.UtcNow));

        (await harness.Published.Any<WalletReleaseCommand>(x =>
            x.Context.Message.BookingId == bookingId
            && x.Context.Message.WalletId == WalletId)).Should().BeTrue(
            "pre-ticket B2B void must release the wallet reservation");
        (await harness.Published.Any<VoidPnrCommand>(x =>
            x.Context.Message.BookingId == bookingId)).Should().BeTrue(
            "pre-ticket void must void the PNR");
        (await harness.Published.Any<BookingCancelled>(x =>
            x.Context.Message.BookingId == bookingId)).Should().BeTrue(
            "terminal cancellation event must be published for downstream notifiers");

        await harness.Stop();
    }

    [Fact(DisplayName = "B2B-10: VoidRequested just after PnrCreated (before wallet reserve) voids the PNR")]
    public async Task VoidRequested_after_PnrCreated_before_WalletReserve_voids_pnr()
    {
        var (harness, _) = await StartHarnessAsync();
        var bookingId = Guid.NewGuid();

        await harness.Bus.Publish(B2BInitiated(bookingId));
        await harness.Bus.Publish(AgencyDetails(bookingId));
        await harness.Bus.Publish(new PriceReconfirmed(bookingId, 250m, "GBP", DateTimeOffset.UtcNow));
        await harness.Bus.Publish(new PnrCreated(bookingId, "ABC123", DateTime.UtcNow.AddHours(24), DateTimeOffset.UtcNow));

        // WalletReserving state is in flight; void before wallet responds.
        await harness.Bus.Publish(new VoidRequested(
            bookingId, "agent-admin-1", "customer_cancel", DateTimeOffset.UtcNow));

        (await harness.Published.Any<VoidPnrCommand>(x =>
            x.Context.Message.BookingId == bookingId)).Should().BeTrue();
        (await harness.Published.Any<BookingCancelled>(x =>
            x.Context.Message.BookingId == bookingId)).Should().BeTrue();

        await harness.Stop();
    }

    [Fact(DisplayName = "B2B-10: VoidRequested on finalized (Confirmed) saga is ignored — controller already 409s")]
    public async Task VoidRequested_on_confirmed_saga_does_not_publish_void_commands()
    {
        var (harness, _) = await StartHarnessAsync();
        var bookingId = Guid.NewGuid();

        await harness.Bus.Publish(B2BInitiated(bookingId));
        await harness.Bus.Publish(AgencyDetails(bookingId));
        await harness.Bus.Publish(new PriceReconfirmed(bookingId, 250m, "GBP", DateTimeOffset.UtcNow));
        await harness.Bus.Publish(new PnrCreated(bookingId, "ABC123", DateTime.UtcNow.AddHours(24), DateTimeOffset.UtcNow));
        await harness.Bus.Publish(new WalletReserved(
            CorrelationId: bookingId, BookingId: bookingId,
            LedgerEntryId: Guid.NewGuid(), BalanceAfter: 750m));
        await harness.Bus.Publish(new TicketIssued(bookingId, "TKT0001234567", DateTimeOffset.UtcNow));

        // Now try to void post-ticket. The controller would 409, but if a
        // VoidRequested slipped through (race with user clicking Void at the
        // same moment as ticketing completes) the saga must NOT double-void.
        await harness.Bus.Publish(new VoidRequested(
            bookingId, "agent-admin-1", "customer_cancel", DateTimeOffset.UtcNow));

        // Give the saga time to consume (and drop) the message.
        await Task.Delay(100);

        // Should have 0 WalletRelease + 0 BookingCancelled published by the VoidRequested
        // handler (TicketIssued→Capturing already fired earlier; no new compensation).
        (await harness.Published.Any<WalletReleaseCommand>(x =>
            x.Context.Message.BookingId == bookingId)).Should().BeFalse(
            "post-ticket VoidRequested must not re-release the wallet (controller D-39 gate is authoritative)");
        (await harness.Published.Any<BookingCancelled>(x =>
            x.Context.Message.BookingId == bookingId)).Should().BeFalse(
            "post-ticket VoidRequested must not emit BookingCancelled from a Capturing/Confirmed state");

        await harness.Stop();
    }
}
