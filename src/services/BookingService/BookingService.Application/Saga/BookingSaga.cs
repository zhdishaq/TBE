using MassTransit;
using TBE.Contracts.Commands;
using TBE.Contracts.Events;

namespace TBE.BookingService.Application.Saga;

/// <summary>
/// Orchestration saga for the B2C flight booking lifecycle (D-05 canonical ordering):
/// BookingInitiated → PriceReconfirmed → PnrCreated → PaymentAuthorized →
/// TicketIssued → PaymentCaptured → BookingConfirmed.
///
/// Compensation chain (D-03, reverse order):
///  - PaymentAuthorizationFailed after PnrCreated → VoidPnrCommand → BookingFailed.
///  - TicketIssuanceFailed after PaymentAuthorized → CancelAuthorizationCommand THEN
///    VoidPnrCommand (reverse of success order) → BookingFailed.
///  - PaymentCaptureFailed is RETRIED 3x (2s/4s/8s) via <c>BookingSagaDefinition</c>.
///    After retry exhaustion the saga publishes <c>SagaDeadLetterRequested</c> and
///    transitions to Failed — it does NOT void the PNR or refund (ops reconciliation).
///
/// State is persisted with optimistic concurrency (ISagaVersion + IsRowVersion) so concurrent
/// message deliveries surface as concurrency exceptions that MassTransit retries cleanly (D-01).
///
/// No DbContext is injected here — consumers that need SQL (SagaDeadLetterSink, TTL monitor)
/// keep that separation (Pitfall 2).
/// </summary>
public class BookingSaga : MassTransitStateMachine<BookingSagaState>
{
    // Forward states
    public State PriceReconfirming { get; private set; } = null!;
    public State PnrCreating { get; private set; } = null!;
    public State Authorizing { get; private set; } = null!;
    public State TicketIssuing { get; private set; } = null!;
    public State Capturing { get; private set; } = null!;
    public State Confirmed { get; private set; } = null!;

    // Terminal/compensation
    public State Compensating { get; private set; } = null!;
    public State Failed { get; private set; } = null!;

    // Forward events
    public Event<BookingInitiated> BookingInitiated { get; private set; } = null!;
    public Event<PriceReconfirmed> PriceReconfirmed { get; private set; } = null!;
    public Event<PnrCreated> PnrCreated { get; private set; } = null!;
    public Event<PaymentAuthorized> PaymentAuthorized { get; private set; } = null!;
    public Event<TicketIssued> TicketIssued { get; private set; } = null!;
    public Event<PaymentCaptured> PaymentCaptured { get; private set; } = null!;

    // Failure callbacks
    public Event<PriceReconfirmationFailed> PriceReconfirmationFailed { get; private set; } = null!;
    public Event<PnrCreationFailed> PnrCreationFailed { get; private set; } = null!;
    public Event<PaymentAuthorizationFailed> PaymentAuthorizationFailed { get; private set; } = null!;
    public Event<TicketIssuanceFailed> TicketIssuanceFailed { get; private set; } = null!;
    public Event<PaymentCaptureFailed> PaymentCaptureFailed { get; private set; } = null!;

    // Timeout (hard-cap) schedule
    public Schedule<BookingSagaState, BookingTimeoutExpired> HardTimeout { get; private set; } = null!;

    public BookingSaga()
    {
        InstanceState(x => x.CurrentState);

        Event(() => BookingInitiated, x => x.CorrelateById(m => m.Message.BookingId));
        Event(() => PriceReconfirmed, x => x.CorrelateById(m => m.Message.BookingId));
        Event(() => PnrCreated, x => x.CorrelateById(m => m.Message.BookingId));
        Event(() => PaymentAuthorized, x => x.CorrelateById(m => m.Message.BookingId));
        Event(() => TicketIssued, x => x.CorrelateById(m => m.Message.BookingId));
        Event(() => PaymentCaptured, x => x.CorrelateById(m => m.Message.BookingId));

        Event(() => PriceReconfirmationFailed, x => x.CorrelateById(m => m.Message.BookingId));
        Event(() => PnrCreationFailed, x => x.CorrelateById(m => m.Message.BookingId));
        Event(() => PaymentAuthorizationFailed, x => x.CorrelateById(m => m.Message.BookingId));
        Event(() => TicketIssuanceFailed, x => x.CorrelateById(m => m.Message.BookingId));
        Event(() => PaymentCaptureFailed, x => x.CorrelateById(m => m.Message.BookingId));

        Schedule(() => HardTimeout, x => x.TimeoutTokenId, s =>
        {
            s.Delay = TimeSpan.FromMinutes(60); // default; Initially overrides using ticketing deadline
            s.Received = r => r.CorrelateById(m => m.Message.BookingId);
        });

        // ---------- Forward chain ----------
        Initially(
            When(BookingInitiated)
                .Then(ctx =>
                {
                    ctx.Saga.BookingReference = ctx.Message.BookingReference;
                    ctx.Saga.ProductType = ctx.Message.ProductType;
                    ctx.Saga.Channel = ctx.Message.Channel;
                    ctx.Saga.UserId = ctx.Message.UserId;
                    ctx.Saga.TotalAmount = ctx.Message.TotalAmount;
                    ctx.Saga.Currency = ctx.Message.Currency;
                    ctx.Saga.PaymentMethod = ctx.Message.PaymentMethod;
                    ctx.Saga.WalletId = ctx.Message.WalletId;
                    ctx.Saga.InitiatedAtUtc = ctx.Message.InitiatedAt.UtcDateTime;
                    ctx.Saga.LastSuccessfulStep = "Initiated";
                })
                .Schedule(HardTimeout, ctx => new BookingTimeoutExpired(ctx.Message.BookingId))
                .Publish(ctx => new ReconfirmPriceCommand(ctx.Message.BookingId, ctx.Saga.OfferToken ?? string.Empty))
                .TransitionTo(PriceReconfirming));

        During(PriceReconfirming,
            When(PriceReconfirmed)
                .Then(ctx =>
                {
                    ctx.Saga.TotalAmount = ctx.Message.ReconfirmedAmount;
                    ctx.Saga.Currency = ctx.Message.Currency;
                    ctx.Saga.LastSuccessfulStep = "PriceReconfirmed";
                })
                .Publish(ctx => new CreatePnrCommand(ctx.Saga.CorrelationId, ctx.Saga.OfferToken ?? string.Empty, Array.Empty<string>()))
                .TransitionTo(PnrCreating),
            When(PriceReconfirmationFailed)
                .Then(ctx => ctx.Saga.LastSuccessfulStep ??= "Initiated")
                .Unschedule(HardTimeout)
                .Publish(ctx => new BookingFailed(
                    ctx.Saga.CorrelationId, Guid.NewGuid(), ctx.Message.Cause,
                    ctx.Saga.LastSuccessfulStep ?? "Initiated", DateTimeOffset.UtcNow))
                .TransitionTo(Failed));

        During(PnrCreating,
            When(PnrCreated)
                .Then(ctx =>
                {
                    ctx.Saga.GdsPnr = ctx.Message.Pnr;
                    ctx.Saga.TicketingDeadlineUtc = ctx.Message.TicketingDeadlineUtc;
                    ctx.Saga.LastSuccessfulStep = "PnrCreated";
                })
                .Publish(ctx => new AuthorizePaymentCommand(
                    ctx.Saga.CorrelationId,
                    ctx.Saga.TotalAmount * 100m,
                    ctx.Saga.Currency,
                    ctx.Saga.UserId,
                    string.Empty /* PaymentMethodId — provided by checkout flow in 03-02 */))
                .TransitionTo(Authorizing),
            When(PnrCreationFailed)
                .Unschedule(HardTimeout)
                .Publish(ctx => new BookingFailed(
                    ctx.Saga.CorrelationId, Guid.NewGuid(), ctx.Message.Cause,
                    ctx.Saga.LastSuccessfulStep ?? "PriceReconfirmed", DateTimeOffset.UtcNow))
                .TransitionTo(Failed));

        During(Authorizing,
            When(PaymentAuthorized)
                .Then(ctx =>
                {
                    ctx.Saga.StripePaymentIntentId = ctx.Message.PaymentIntentId;
                    ctx.Saga.LastSuccessfulStep = "PaymentAuthorized";
                })
                .Publish(ctx => new IssueTicketCommand(ctx.Saga.CorrelationId, ctx.Saga.GdsPnr ?? string.Empty))
                .TransitionTo(TicketIssuing),
            When(PaymentAuthorizationFailed)
                // Compensation: void the PNR we created (only PNR is outstanding).
                .Publish(ctx => new VoidPnrCommand(ctx.Saga.CorrelationId, ctx.Saga.GdsPnr ?? string.Empty, "auth_failed"))
                .Unschedule(HardTimeout)
                .Publish(ctx => new BookingFailed(
                    ctx.Saga.CorrelationId, Guid.NewGuid(), ctx.Message.Cause,
                    ctx.Saga.LastSuccessfulStep ?? "PnrCreated", DateTimeOffset.UtcNow))
                .TransitionTo(Failed));

        During(TicketIssuing,
            When(TicketIssued)
                .Then(ctx =>
                {
                    ctx.Saga.TicketNumber = ctx.Message.TicketNumber;
                    ctx.Saga.LastSuccessfulStep = "TicketIssued";
                })
                .Publish(ctx => new CapturePaymentCommand(
                    ctx.Saga.CorrelationId,
                    ctx.Saga.StripePaymentIntentId ?? string.Empty,
                    ctx.Saga.TotalAmount * 100m))
                .TransitionTo(Capturing),
            When(TicketIssuanceFailed)
                // Compensation: CancelAuthorization THEN VoidPnr (reverse of success order).
                .Publish(ctx => new CancelAuthorizationCommand(ctx.Saga.CorrelationId, ctx.Saga.StripePaymentIntentId ?? string.Empty))
                .Publish(ctx => new VoidPnrCommand(ctx.Saga.CorrelationId, ctx.Saga.GdsPnr ?? string.Empty, "ticket_failed"))
                .Unschedule(HardTimeout)
                .Publish(ctx => new BookingFailed(
                    ctx.Saga.CorrelationId, Guid.NewGuid(), ctx.Message.Cause,
                    ctx.Saga.LastSuccessfulStep ?? "PaymentAuthorized", DateTimeOffset.UtcNow))
                .TransitionTo(Failed));

        During(Capturing,
            When(PaymentCaptured)
                .Then(ctx => ctx.Saga.LastSuccessfulStep = "PaymentCaptured")
                .Unschedule(HardTimeout)
                .Publish(ctx => new BookingConfirmed(
                    ctx.Saga.CorrelationId,
                    Guid.NewGuid(),
                    ctx.Saga.BookingReference,
                    ctx.Saga.GdsPnr ?? string.Empty,
                    ctx.Saga.TicketNumber ?? string.Empty,
                    ctx.Saga.StripePaymentIntentId ?? string.Empty,
                    DateTimeOffset.UtcNow))
                .TransitionTo(Confirmed)
                .Finalize(),
            // PaymentCaptureFailed: after retries are exhausted, the saga definition
            // lets the message land here; emit SagaDeadLetterRequested (consumed by
            // SagaDeadLetterSink) and go to Failed — do NOT void PNR (tickets issued).
            When(PaymentCaptureFailed)
                .Publish(ctx => new SagaDeadLetterRequested(
                    ctx.Saga.CorrelationId,
                    "capture",
                    ctx.Message.Cause,
                    DateTimeOffset.UtcNow))
                .Unschedule(HardTimeout)
                .Publish(ctx => new BookingFailed(
                    ctx.Saga.CorrelationId, Guid.NewGuid(), ctx.Message.Cause,
                    ctx.Saga.LastSuccessfulStep ?? "TicketIssued", DateTimeOffset.UtcNow))
                .TransitionTo(Failed));

        SetCompletedWhenFinalized();
    }
}

