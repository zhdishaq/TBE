using MassTransit;
using TBE.Contracts.Commands;
using TBE.Contracts.Enums;
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

    // Plan 05-02 Task 2 — B2B wallet reserve states (D-24).
    public State WalletReserving { get; private set; } = null!;
    public State WalletReserveFailedState { get; private set; } = null!;

    // Terminal/compensation
    public State Compensating { get; private set; } = null!;
    public State Failed { get; private set; } = null!;

    // Plan 05-04 Task 1 (B2B-10) — terminal state for admin-requested
    // pre-ticket voids. Distinct from Failed so ops metrics can distinguish
    // customer-requested cancellations from saga-compensation failures.
    public State Cancelled { get; private set; } = null!;

    // Forward events
    public Event<BookingInitiated> BookingInitiated { get; private set; } = null!;
    public Event<PriceReconfirmed> PriceReconfirmed { get; private set; } = null!;
    public Event<PnrCreated> PnrCreated { get; private set; } = null!;
    public Event<PaymentAuthorized> PaymentAuthorized { get; private set; } = null!;
    public Event<TicketIssued> TicketIssued { get; private set; } = null!;
    public Event<PaymentCaptured> PaymentCaptured { get; private set; } = null!;

    // Plan 05-02 Task 2 — B2B wallet-reserve events (D-40).
    public Event<WalletReserved> WalletReserved { get; private set; } = null!;
    public Event<WalletReserveFailed> WalletReserveFailed { get; private set; } = null!;

    // Plan 05-02 Task 2 — follow-up event stamping agency pricing + customer contact
    // onto the saga immediately after BookingInitiated (D-33, D-36, D-37, B2B-04).
    public Event<AgentBookingDetailsCaptured> AgentBookingDetailsCaptured { get; private set; } = null!;

    // Plan 05-04 Task 1 (B2B-10) — pre-ticket void request event.
    public Event<VoidRequested> VoidRequested { get; private set; } = null!;

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

        // Plan 06-01 Task 5 (BO-05 / D-50) — wire the BookingEvents state
        // observer so every TransitionTo writes one dbo.BookingEvents row
        // via IBookingEventsWriter. Connected here so a new forward state
        // added in a later plan automatically gets an audit row without a
        // second edit in SagaDefinition + Program.cs.
        ConnectStateObserver(new BookingEventsObserver());

        Event(() => BookingInitiated, x => x.CorrelateById(m => m.Message.BookingId));
        Event(() => PriceReconfirmed, x => x.CorrelateById(m => m.Message.BookingId));
        Event(() => PnrCreated, x => x.CorrelateById(m => m.Message.BookingId));
        Event(() => PaymentAuthorized, x => x.CorrelateById(m => m.Message.BookingId));
        Event(() => TicketIssued, x => x.CorrelateById(m => m.Message.BookingId));
        Event(() => PaymentCaptured, x => x.CorrelateById(m => m.Message.BookingId));

        Event(() => WalletReserved, x => x.CorrelateById(m => m.Message.BookingId));
        Event(() => WalletReserveFailed, x => x.CorrelateById(m => m.Message.BookingId));

        Event(() => AgentBookingDetailsCaptured, x => x.CorrelateById(m => m.Message.BookingId));

        Event(() => VoidRequested, x => x.CorrelateById(m => m.Message.BookingId));

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
                    // Plan 05-02 Task 2 — preserve the Phase-3 string channel
                    // value on ChannelText for backwards compatibility, and
                    // parse it into the typed enum used by the B2B IfElse
                    // branch at PnrCreated. BookingInitiated.Channel remains
                    // a string on the contract (Phase-3 compat); server-side
                    // writers — including AgentBookingsController in this
                    // plan — send "b2b".
                    ctx.Saga.ChannelText = ctx.Message.Channel;
                    ctx.Saga.Channel = string.Equals(ctx.Message.Channel, "b2b", StringComparison.OrdinalIgnoreCase)
                        ? Channel.B2B
                        : Channel.B2C;
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
                // Plan 05-02 Task 2 — IfElse on Channel == Channel.B2B at
                // PnrCreated. B2B → WalletReserveCommand (agency wallet debit,
                // idempotent on BookingId per T-05-02-04). B2C → unchanged
                // AuthorizePaymentCommand (Stripe authorize; Plan 03-01
                // regression preserved).
                .IfElse(ctx => ctx.Saga.Channel == Channel.B2B,
                    b2b => b2b
                        .Publish(ctx => new WalletReserveCommand(
                            CorrelationId: ctx.Saga.CorrelationId,
                            BookingId: ctx.Saga.CorrelationId,
                            AgencyId: ctx.Saga.AgencyId ?? Guid.Empty,
                            WalletId: ctx.Saga.WalletId ?? Guid.Empty,
                            Amount: ctx.Saga.AgencyNetFare ?? ctx.Saga.TotalAmount,
                            Currency: ctx.Saga.Currency,
                            IdempotencyKey: ctx.Saga.CorrelationId.ToString()))
                        .TransitionTo(WalletReserving),
                    b2c => b2c
                        .Publish(ctx => new AuthorizePaymentCommand(
                            ctx.Saga.CorrelationId,
                            ctx.Saga.TotalAmount * 100m,
                            ctx.Saga.Currency,
                            ctx.Saga.UserId,
                            string.Empty /* PaymentMethodId — provided by checkout flow in 03-02 */))
                        .TransitionTo(Authorizing)),
            When(PnrCreationFailed)
                .Unschedule(HardTimeout)
                .Publish(ctx => new BookingFailed(
                    ctx.Saga.CorrelationId, Guid.NewGuid(), ctx.Message.Cause,
                    ctx.Saga.LastSuccessfulStep ?? "PriceReconfirmed", DateTimeOffset.UtcNow))
                .TransitionTo(Failed));

        // Plan 05-02 Task 2 — B2B wallet-reserve outcomes. Success issues the
        // ticket (wallet hold commits post-ticketing in Phase 6). Failure
        // compensates by voiding the PNR we just created (Pitfall 23 — cancel
        // before customer-visible money movement); no charge occurred so no
        // refund is needed.
        During(WalletReserving,
            When(WalletReserved)
                .Then(ctx =>
                {
                    ctx.Saga.WalletReservationTxId = ctx.Message.LedgerEntryId;
                    ctx.Saga.LastSuccessfulStep = "WalletReserved";
                })
                .Publish(ctx => new IssueTicketCommand(ctx.Saga.CorrelationId, ctx.Saga.GdsPnr ?? string.Empty))
                .TransitionTo(TicketIssuing),
            When(WalletReserveFailed)
                .Then(ctx => ctx.Saga.FailureReason = ctx.Message.Reason)
                .Publish(ctx => new VoidPnrCommand(ctx.Saga.CorrelationId, ctx.Saga.GdsPnr ?? string.Empty, ctx.Message.Reason))
                .Unschedule(HardTimeout)
                .Publish(ctx => new BookingFailed(
                    ctx.Saga.CorrelationId, Guid.NewGuid(), ctx.Message.Reason,
                    ctx.Saga.LastSuccessfulStep ?? "PnrCreated", DateTimeOffset.UtcNow))
                .TransitionTo(WalletReserveFailedState));

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

        // Plan 05-02 Task 2 — the agency pricing + customer-contact snapshot
        // follows BookingInitiated on the outbox in the same controller action.
        // Handled DuringAny so it lands before PnrCreated fires the B2B IfElse,
        // regardless of which forward state the saga has already transitioned
        // to (PriceReconfirming → PnrCreating). Idempotent: repeated deliveries
        // overwrite identical frozen amounts, satisfying D-40.
        DuringAny(
            When(AgentBookingDetailsCaptured)
                .Then(ctx =>
                {
                    ctx.Saga.AgencyId = ctx.Message.AgencyId;
                    ctx.Saga.AgencyNetFare = ctx.Message.AgencyNetFare;
                    ctx.Saga.AgencyMarkupAmount = ctx.Message.AgencyMarkupAmount;
                    ctx.Saga.AgencyGrossAmount = ctx.Message.AgencyGrossAmount;
                    ctx.Saga.AgencyCommissionAmount = ctx.Message.AgencyCommissionAmount;
                    ctx.Saga.AgencyMarkupOverride = ctx.Message.AgencyMarkupOverride;
                    ctx.Saga.CustomerName = ctx.Message.CustomerName;
                    ctx.Saga.CustomerEmail = ctx.Message.CustomerEmail;
                    ctx.Saga.CustomerPhone = ctx.Message.CustomerPhone;
                    if (!string.IsNullOrWhiteSpace(ctx.Message.OfferId))
                        ctx.Saga.OfferToken ??= ctx.Message.OfferId;
                }));

        // Plan 05-04 Task 1 (B2B-10) — admin-requested pre-ticket void.
        // The controller refuses post-ticket voids with 409 D-39, so the saga
        // treats TicketNumber / LastSuccessfulStep as the pre/post-ticket
        // discriminator. Once a ticket has been issued (TicketNumber set) OR
        // the saga has already transitioned past TicketIssuing (Capturing /
        // Confirmed / Failed), VoidRequested is silently dropped.
        //
        // For B2B sagas that have reserved the wallet we publish
        // WalletReleaseCommand (wallet ledger unholds the balance).
        // For B2C sagas we cancel any outstanding Stripe authorization.
        // For any saga with a PNR we publish VoidPnrCommand.
        // Finally BookingCancelled is published so NOTF-04's existing
        // BookingCancelledConsumer sends the cancellation email.
        DuringAny(
            When(VoidRequested,
                 ctx => string.IsNullOrWhiteSpace(ctx.Saga.TicketNumber)
                        && ctx.Saga.LastSuccessfulStep != "PaymentCaptured"
                        && ctx.Saga.LastSuccessfulStep != "TicketIssued"
                        && ctx.Saga.LastSuccessfulStep != "Cancelled")
                .If(ctx => ctx.Saga.Channel == Channel.B2B
                           && ctx.Saga.WalletReservationTxId.HasValue
                           && ctx.Saga.WalletId.HasValue,
                    b => b.Publish(ctx => new WalletReleaseCommand(
                        BookingId: ctx.Saga.CorrelationId,
                        WalletId: ctx.Saga.WalletId!.Value,
                        ReservationTxId: ctx.Saga.WalletReservationTxId!.Value)))
                .If(ctx => ctx.Saga.Channel == Channel.B2C
                           && !string.IsNullOrEmpty(ctx.Saga.StripePaymentIntentId),
                    b => b.Publish(ctx => new CancelAuthorizationCommand(
                        ctx.Saga.CorrelationId,
                        ctx.Saga.StripePaymentIntentId ?? string.Empty)))
                .If(ctx => !string.IsNullOrEmpty(ctx.Saga.GdsPnr),
                    b => b.Publish(ctx => new VoidPnrCommand(
                        ctx.Saga.CorrelationId,
                        ctx.Saga.GdsPnr ?? string.Empty,
                        "admin_requested_void")))
                .Unschedule(HardTimeout)
                .Publish(ctx => new BookingCancelled(
                    ctx.Saga.CorrelationId,
                    Guid.NewGuid(),
                    ctx.Message.Reason ?? "admin_requested_void",
                    DateTimeOffset.UtcNow))
                .Then(ctx => ctx.Saga.LastSuccessfulStep = "Cancelled")
                .TransitionTo(Cancelled)
                .Finalize());

        SetCompletedWhenFinalized();
    }
}

