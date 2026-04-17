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
/// Plan 05-02 Task 2 — BookingSaga B2B branch at PnrCreated. Asserts:
/// - PnrCreated on a B2B saga publishes <see cref="WalletReserveCommand"/> with
///   IdempotencyKey = BookingId.ToString() (D-40 / T-05-02-04 double-spend guard)
/// - PnrCreated on a B2C saga publishes <see cref="AuthorizePaymentCommand"/>
///   (Plan 03-01 regression — Stripe authorize path is preserved)
/// - WalletReserved transitions the B2B saga into ticket-issuing with an
///   <see cref="IssueTicketCommand"/> (B2B does not hit Stripe authorize)
/// - WalletReserveFailed publishes VoidPnrCommand + BookingFailed (compensation)
///   and stamps saga.FailureReason for the portal
///
/// Uses MassTransit's in-memory test harness (MassTransit.TestFramework 9.1.0).
/// Written as the Task 2 RED phase — tests must FAIL until the saga's
/// BookingInitiated handler parses Channel and the IfElse at PnrCreated routes
/// to the B2B branch based on <see cref="BookingSagaState.Channel"/>.
/// </summary>
[Trait("Category", "Unit")]
public class BookingSagaB2BBranchTests
{
    private static readonly Guid AgencyIdSample = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid WalletIdSample = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

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
        new(bookingId, "flight", "b2b", "agent-sub-1", "TBE-260520-B2B00001",
            TotalAmount: 250m, Currency: "GBP", PaymentMethod: "wallet",
            WalletId: WalletIdSample, InitiatedAt: DateTimeOffset.UtcNow);

    private static BookingInitiated B2CInitiated(Guid bookingId) =>
        new(bookingId, "flight", "b2c", "user-1", "TBE-260520-B2C00001",
            TotalAmount: 250m, Currency: "GBP", PaymentMethod: "card",
            WalletId: null, InitiatedAt: DateTimeOffset.UtcNow);

    private static AgentBookingDetailsCaptured AgencyDetails(Guid bookingId) =>
        new(BookingId: bookingId, AgencyId: AgencyIdSample,
            AgencyNetFare: 200m, AgencyMarkupAmount: 50m,
            AgencyGrossAmount: 250m, AgencyCommissionAmount: 50m,
            AgencyMarkupOverride: null,
            CustomerName: "Jane Customer", CustomerEmail: "jane@example.com",
            CustomerPhone: "+441234567890", OfferId: "offer-001",
            At: DateTimeOffset.UtcNow);

    [Fact(DisplayName = "D-24 / T-05-02-04: PnrCreated on B2B saga publishes WalletReserveCommand with IdempotencyKey == BookingId")]
    public async Task PnrCreated_with_Channel_B2B_publishes_WalletReserveCommand_with_idempotency_key()
    {
        var (harness, saga) = await StartHarnessAsync();
        var bookingId = Guid.NewGuid();

        await harness.Bus.Publish(B2BInitiated(bookingId));
        (await saga.Created.Any(x => x.CorrelationId == bookingId)).Should().BeTrue();

        await harness.Bus.Publish(AgencyDetails(bookingId));
        await harness.Bus.Publish(new PriceReconfirmed(bookingId, 250m, "GBP", DateTimeOffset.UtcNow));
        await harness.Bus.Publish(new PnrCreated(bookingId, "ABC123", DateTime.UtcNow.AddHours(24), DateTimeOffset.UtcNow));

        var published = await harness.Published.Any<WalletReserveCommand>(x =>
            x.Context.Message.BookingId == bookingId
            && x.Context.Message.IdempotencyKey == bookingId.ToString()
            && x.Context.Message.AgencyId == AgencyIdSample
            && x.Context.Message.Amount == 200m
            && x.Context.Message.Currency == "GBP");
        published.Should().BeTrue("B2B branch must publish an idempotent WalletReserveCommand carrying the frozen AgencyNetFare");

        // Regression: must NOT publish AuthorizePaymentCommand on the B2B branch.
        (await harness.Published.Any<AuthorizePaymentCommand>(x => x.Context.Message.BookingId == bookingId))
            .Should().BeFalse("B2B must never hit Stripe authorize");

        await harness.Stop();
    }

    [Fact(DisplayName = "Plan 03-01 regression: PnrCreated on B2C saga still publishes AuthorizePaymentCommand")]
    public async Task PnrCreated_with_Channel_B2C_publishes_AuthorizePaymentCommand()
    {
        var (harness, _) = await StartHarnessAsync();
        var bookingId = Guid.NewGuid();

        await harness.Bus.Publish(B2CInitiated(bookingId));
        await harness.Bus.Publish(new PriceReconfirmed(bookingId, 250m, "GBP", DateTimeOffset.UtcNow));
        await harness.Bus.Publish(new PnrCreated(bookingId, "ABC123", DateTime.UtcNow.AddHours(24), DateTimeOffset.UtcNow));

        (await harness.Published.Any<AuthorizePaymentCommand>(x => x.Context.Message.BookingId == bookingId))
            .Should().BeTrue("B2C path must keep the Phase-3 Stripe authorize contract");

        // Regression: B2C never publishes WalletReserveCommand.
        (await harness.Published.Any<WalletReserveCommand>(x => x.Context.Message.BookingId == bookingId))
            .Should().BeFalse("B2C must not hit the wallet reserve path");

        await harness.Stop();
    }

    [Fact(DisplayName = "B2B saga: WalletReserved advances to ticket-issuing and publishes IssueTicketCommand")]
    public async Task WalletReserved_transitions_to_TicketIssuing_and_publishes_IssueTicketCommand()
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

        (await harness.Published.Any<IssueTicketCommand>(x => x.Context.Message.BookingId == bookingId))
            .Should().BeTrue("successful wallet reserve must trigger ticket issuance");

        await harness.Stop();
    }

    [Fact(DisplayName = "D-39: WalletReserveFailed compensates by VoidPnrCommand + BookingFailed and records FailureReason")]
    public async Task WalletReserveFailed_publishes_VoidPnrCommand_and_BookingFailed()
    {
        var (harness, saga) = await StartHarnessAsync();
        var bookingId = Guid.NewGuid();

        await harness.Bus.Publish(B2BInitiated(bookingId));
        await harness.Bus.Publish(AgencyDetails(bookingId));
        await harness.Bus.Publish(new PriceReconfirmed(bookingId, 250m, "GBP", DateTimeOffset.UtcNow));
        await harness.Bus.Publish(new PnrCreated(bookingId, "ABC123", DateTime.UtcNow.AddHours(24), DateTimeOffset.UtcNow));
        await harness.Bus.Publish(new WalletReserveFailed(
            CorrelationId: bookingId, BookingId: bookingId, Reason: "insufficient_funds"));

        (await harness.Published.Any<VoidPnrCommand>(x => x.Context.Message.BookingId == bookingId))
            .Should().BeTrue("pre-ticket wallet-reserve failure must void the PNR");
        (await harness.Published.Any<BookingFailed>(x =>
            x.Context.Message.BookingId == bookingId
            && x.Context.Message.Cause == "insufficient_funds")).Should().BeTrue();

        var instance = await saga.Exists(bookingId);
        instance.Should().NotBeNull();
        var state = saga.Sagas.Contains(bookingId);
        state.Should().NotBeNull();
        state!.FailureReason.Should().Be("insufficient_funds");

        await harness.Stop();
    }

    [Fact(DisplayName = "Typed Channel: BookingInitiated with Channel='b2b' lands Channel.B2B on saga state")]
    public async Task BookingInitiated_b2b_string_is_parsed_into_typed_Channel_B2B()
    {
        var (harness, saga) = await StartHarnessAsync();
        var bookingId = Guid.NewGuid();

        await harness.Bus.Publish(B2BInitiated(bookingId));
        (await saga.Created.Any(x => x.CorrelationId == bookingId)).Should().BeTrue();

        var state = saga.Sagas.Contains(bookingId);
        state.Should().NotBeNull();
        state!.Channel.Should().Be(Channel.B2B);
        state.ChannelText.Should().Be("b2b");

        await harness.Stop();
    }
}
