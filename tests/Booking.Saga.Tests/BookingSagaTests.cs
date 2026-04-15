using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using TBE.BookingService.Application.Saga;
using TBE.Contracts.Commands;
using TBE.Contracts.Events;
using Xunit;

namespace Booking.Saga.Tests;

/// <summary>
/// BookingSaga state-machine unit tests using MassTransit's in-memory test harness.
/// Covers D-05 happy-path ordering and the D-03 compensation matrix.
/// Capture-failure path asserts SagaDeadLetterRequested is raised instead of voiding PNR.
/// </summary>
[Trait("Category", "Unit")]
public class BookingSagaTests
{
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

    private static BookingInitiated SampleInitiated(Guid bookingId) =>
        new(bookingId, "flight", "b2c", "user-1", "TBE-260416-ABC", 100m, "USD", "card", null, DateTimeOffset.UtcNow);

    [Fact(DisplayName = "FLTB04: happy path transitions to Confirmed")]
    public async Task FLTB04_happy_path_transitions_to_Confirmed()
    {
        var (harness, saga) = await StartHarnessAsync();
        var bookingId = Guid.NewGuid();

        await harness.Bus.Publish(SampleInitiated(bookingId));
        (await saga.Created.Any(x => x.CorrelationId == bookingId)).Should().BeTrue();

        await harness.Bus.Publish(new PriceReconfirmed(bookingId, 100m, "USD", DateTimeOffset.UtcNow));
        await harness.Bus.Publish(new PnrCreated(bookingId, "PNR123", DateTime.UtcNow.AddHours(24), DateTimeOffset.UtcNow));
        await harness.Bus.Publish(new PaymentAuthorized(bookingId, "pi_1", DateTimeOffset.UtcNow));
        await harness.Bus.Publish(new TicketIssued(bookingId, "TKT-1", DateTimeOffset.UtcNow));
        await harness.Bus.Publish(new PaymentCaptured(bookingId, "pi_1", DateTimeOffset.UtcNow));

        (await harness.Published.Any<BookingConfirmed>(x => x.Context.Message.BookingId == bookingId))
            .Should().BeTrue();

        await harness.Stop();
    }

    [Fact(DisplayName = "FLTB07: payment auth failure publishes VoidPnrCommand")]
    public async Task FLTB07_payment_auth_failure_publishes_VoidPnrCommand()
    {
        var (harness, saga) = await StartHarnessAsync();
        var bookingId = Guid.NewGuid();

        await harness.Bus.Publish(SampleInitiated(bookingId));
        await harness.Bus.Publish(new PriceReconfirmed(bookingId, 100m, "USD", DateTimeOffset.UtcNow));
        await harness.Bus.Publish(new PnrCreated(bookingId, "PNR123", DateTime.UtcNow.AddHours(24), DateTimeOffset.UtcNow));
        await harness.Bus.Publish(new PaymentAuthorizationFailed(bookingId, "card_declined"));

        (await harness.Published.Any<VoidPnrCommand>(x => x.Context.Message.BookingId == bookingId))
            .Should().BeTrue();
        (await harness.Published.Any<BookingFailed>(x => x.Context.Message.BookingId == bookingId))
            .Should().BeTrue();

        await harness.Stop();
    }

    [Fact(DisplayName = "FLTB07: ticket failure compensates in reverse order (CancelAuth before VoidPnr)")]
    public async Task FLTB07_ticket_failure_compensates_in_reverse_order()
    {
        var (harness, saga) = await StartHarnessAsync();
        var bookingId = Guid.NewGuid();

        await harness.Bus.Publish(SampleInitiated(bookingId));
        await harness.Bus.Publish(new PriceReconfirmed(bookingId, 100m, "USD", DateTimeOffset.UtcNow));
        await harness.Bus.Publish(new PnrCreated(bookingId, "PNR123", DateTime.UtcNow.AddHours(24), DateTimeOffset.UtcNow));
        await harness.Bus.Publish(new PaymentAuthorized(bookingId, "pi_1", DateTimeOffset.UtcNow));
        await harness.Bus.Publish(new TicketIssuanceFailed(bookingId, "gds_error"));

        (await harness.Published.Any<CancelAuthorizationCommand>(x => x.Context.Message.BookingId == bookingId))
            .Should().BeTrue();
        (await harness.Published.Any<VoidPnrCommand>(x => x.Context.Message.BookingId == bookingId))
            .Should().BeTrue();

        var cancelSentTime = harness.Published
            .Select<CancelAuthorizationCommand>(x => x.Context.Message.BookingId == bookingId)
            .First().Context.SentTime ?? DateTime.MinValue;
        var voidSentTime = harness.Published
            .Select<VoidPnrCommand>(x => x.Context.Message.BookingId == bookingId)
            .First().Context.SentTime ?? DateTime.MaxValue;

        cancelSentTime.Should().BeOnOrBefore(voidSentTime,
            "CancelAuthorization must precede VoidPnr per D-03 reverse-order compensation");

        await harness.Stop();
    }

    [Fact(DisplayName = "FLTB07: capture failure publishes SagaDeadLetterRequested without voiding PNR")]
    public async Task FLTB07_capture_failure_publishes_SagaDeadLetterRequested_without_void()
    {
        var (harness, saga) = await StartHarnessAsync();
        var bookingId = Guid.NewGuid();

        await harness.Bus.Publish(SampleInitiated(bookingId));
        await harness.Bus.Publish(new PriceReconfirmed(bookingId, 100m, "USD", DateTimeOffset.UtcNow));
        await harness.Bus.Publish(new PnrCreated(bookingId, "PNR123", DateTime.UtcNow.AddHours(24), DateTimeOffset.UtcNow));
        await harness.Bus.Publish(new PaymentAuthorized(bookingId, "pi_1", DateTimeOffset.UtcNow));
        await harness.Bus.Publish(new TicketIssued(bookingId, "TKT-1", DateTimeOffset.UtcNow));
        await harness.Bus.Publish(new PaymentCaptureFailed(bookingId, "capture_error"));

        (await harness.Published.Any<SagaDeadLetterRequested>(x => x.Context.Message.CorrelationId == bookingId))
            .Should().BeTrue();

        // Critical: capture failure must NOT void the PNR (tickets issued, so refund path owned by ops)
        (await harness.Published.Any<VoidPnrCommand>(x => x.Context.Message.BookingId == bookingId))
            .Should().BeFalse();
        (await harness.Published.Any<RefundPaymentCommand>(x => x.Context.Message.BookingId == bookingId))
            .Should().BeFalse();

        await harness.Stop();
    }
}
