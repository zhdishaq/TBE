using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TBE.BookingService.Application.Baskets;
using TBE.Contracts.Events;

namespace TBE.BookingService.Infrastructure.Baskets;

/// <summary>
/// Plan 04-04 / D-08 / D-09 / D-10 — the single-PaymentIntent orchestrator that turns
/// saga outcomes into sequential partial captures, partial-failure release-remainder,
/// or a hard-failure void against ONE combined Stripe PaymentIntent per basket.
/// <para>
/// Subscribes (MassTransit):
/// <list type="bullet">
///   <item><see cref="TicketIssued"/> — stage 1 partial capture (<c>FinalCapture=false</c>).</item>
///   <item><see cref="HotelBookingConfirmed"/> — stage 2 final capture (<c>FinalCapture=true</c>).</item>
///   <item><see cref="HotelBookingFailed"/> — D-09 release-remainder finalize OR hard-void if flight not yet captured.</item>
///   <item><see cref="BookingFailed"/> — hard void (flight leg failed before ticket issuance).</item>
/// </list>
/// </para>
/// Per-basket idempotency (T-04-04-04) uses <see cref="BasketEventLog"/> keyed uniquely
/// on <c>(BasketId, EventId)</c>; a replay inserts fail and short-circuit before any
/// Stripe call. Stripe's own idempotency key is the second line of defence.
/// </summary>
public sealed class BasketPaymentOrchestrator :
    IConsumer<TicketIssued>,
    IConsumer<HotelBookingConfirmed>,
    IConsumer<HotelBookingFailed>,
    IConsumer<BookingFailed>
{
    private readonly BookingDbContext _db;
    private readonly IBasketPaymentGateway _payments;
    private readonly ILogger<BasketPaymentOrchestrator> _log;

    public BasketPaymentOrchestrator(
        BookingDbContext db,
        IBasketPaymentGateway payments,
        ILogger<BasketPaymentOrchestrator> log)
    {
        _db = db;
        _payments = payments;
        _log = log;
    }

    // Stripe expects minor units (e.g. pence for GBP). Standard 2dp ISO currencies use *100.
    private static long ToMinorUnits(decimal amount) => (long)Math.Round(amount * 100m, MidpointRounding.AwayFromZero);

    // ----- TicketIssued (flight saga stage 1 — partial capture with FinalCapture=false) -----
    public async Task Consume(ConsumeContext<TicketIssued> ctx)
    {
        var evt = ctx.Message;
        var basket = await _db.Baskets.FirstOrDefaultAsync(
            b => b.FlightBookingId == evt.BookingId, ctx.CancellationToken).ConfigureAwait(false);
        if (basket is null) return; // not a basket-backed flight — single-flight saga handles its own capture.

        if (!await LockEventAsync(basket.BasketId, ctx.MessageId ?? Guid.NewGuid(), nameof(TicketIssued), ctx.CancellationToken)
            .ConfigureAwait(false))
        {
            _log.LogInformation(
                "BasketPaymentOrchestrator: duplicate TicketIssued for basket {BasketId} skipped", basket.BasketId);
            return;
        }

        if (string.IsNullOrEmpty(basket.StripePaymentIntentId))
        {
            _log.LogWarning(
                "BasketPaymentOrchestrator: TicketIssued for basket {BasketId} but no StripePaymentIntentId — skipping",
                basket.BasketId);
            return;
        }
        if (basket.FlightCaptured) return;

        var idempotencyKey = $"basket-{basket.BasketId}-capture-flight";
        _log.LogInformation(
            "BasketPaymentOrchestrator: partial-capture flight basket={BasketId} pi={Pi} amount={Amount}",
            basket.BasketId, basket.StripePaymentIntentId, basket.FlightSubtotal);

        await _payments.CapturePartialAsync(
            basket.StripePaymentIntentId,
            amountToCaptureMinorUnits: ToMinorUnits(basket.FlightSubtotal),
            finalCapture: false,
            idempotencyKey: idempotencyKey,
            ctx.CancellationToken).ConfigureAwait(false);

        basket.FlightCaptured = true;
        basket.ChargedAmount += basket.FlightSubtotal;
        basket.UpdatedUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ctx.CancellationToken).ConfigureAwait(false);
    }

    // ----- HotelBookingConfirmed (stage 2 — final capture with FinalCapture=true) -----
    public async Task Consume(ConsumeContext<HotelBookingConfirmed> ctx)
    {
        var evt = ctx.Message;
        var basket = await _db.Baskets.FirstOrDefaultAsync(
            b => b.HotelBookingId == evt.BookingId, ctx.CancellationToken).ConfigureAwait(false);
        if (basket is null) return;

        if (!await LockEventAsync(basket.BasketId, evt.EventId, nameof(HotelBookingConfirmed), ctx.CancellationToken)
            .ConfigureAwait(false))
        {
            _log.LogInformation(
                "BasketPaymentOrchestrator: duplicate HotelBookingConfirmed for basket {BasketId} skipped", basket.BasketId);
            return;
        }

        if (string.IsNullOrEmpty(basket.StripePaymentIntentId)) return;
        if (basket.HotelCaptured) return;

        // Flight must have finalised first; if not we still do not proceed with capture.
        if (!basket.FlightCaptured)
        {
            _log.LogWarning(
                "BasketPaymentOrchestrator: HotelBookingConfirmed before flight capture for basket {BasketId}",
                basket.BasketId);
            return;
        }

        var idempotencyKey = $"basket-{basket.BasketId}-capture-hotel";
        await _payments.CapturePartialAsync(
            basket.StripePaymentIntentId,
            amountToCaptureMinorUnits: ToMinorUnits(basket.HotelSubtotal),
            finalCapture: true,
            idempotencyKey: idempotencyKey,
            ctx.CancellationToken).ConfigureAwait(false);

        basket.HotelCaptured = true;
        basket.ChargedAmount += basket.HotelSubtotal;
        basket.Status = "Confirmed";
        basket.UpdatedUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ctx.CancellationToken).ConfigureAwait(false);

        // CONTEXT D-19: EventId is the NOTF-06 idempotency key for the combined confirmation email.
        await ctx.Publish(new BasketConfirmed(
            basket.BasketId,
            EventId: Guid.NewGuid(),
            FlightBookingReference: evt.BookingReference, // placeholder — flight ref flows via saga enrichment in Phase 5
            HotelSupplierRef: evt.SupplierRef,
            GuestEmail: basket.GuestEmail,
            GuestFullName: basket.GuestFullName,
            TotalAmount: basket.ChargedAmount,
            Currency: basket.Currency,
            At: DateTimeOffset.UtcNow)).ConfigureAwait(false);
    }

    // ----- HotelBookingFailed — D-09 partial failure (release remainder) OR hard void -----
    public async Task Consume(ConsumeContext<HotelBookingFailed> ctx)
    {
        var evt = ctx.Message;
        var basket = await _db.Baskets.FirstOrDefaultAsync(
            b => b.HotelBookingId == evt.BookingId, ctx.CancellationToken).ConfigureAwait(false);
        if (basket is null) return;

        if (!await LockEventAsync(basket.BasketId, ctx.MessageId ?? Guid.NewGuid(), nameof(HotelBookingFailed), ctx.CancellationToken)
            .ConfigureAwait(false))
        {
            _log.LogInformation(
                "BasketPaymentOrchestrator: duplicate HotelBookingFailed for basket {BasketId} skipped", basket.BasketId);
            return;
        }

        if (string.IsNullOrEmpty(basket.StripePaymentIntentId)) return;

        if (basket.FlightCaptured)
        {
            // D-09 release-remainder: flight already captured with FinalCapture=false.
            // Closing the PI with AmountToCapture=0, FinalCapture=true releases the
            // uncaptured hotel portion. Customer sees ONE charge of FlightSubtotal.
            var idempotencyKey = $"basket-{basket.BasketId}-finalize-partial";
            await _payments.CapturePartialAsync(
                basket.StripePaymentIntentId,
                amountToCaptureMinorUnits: 0,
                finalCapture: true,
                idempotencyKey: idempotencyKey,
                ctx.CancellationToken).ConfigureAwait(false);

            basket.HotelCaptured = true; // PI closed
            basket.RefundedAmount = basket.HotelSubtotal; // released, not refunded
            basket.Status = "PartiallyConfirmed";
            basket.UpdatedUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ctx.CancellationToken).ConfigureAwait(false);

            await ctx.Publish(new BasketPartiallyConfirmed(
                basket.BasketId,
                EventId: Guid.NewGuid(),
                SucceededComponent: "Flight",
                FailedComponent: "Hotel",
                FlightBookingReference: null,
                HotelSupplierRef: null,
                GuestEmail: basket.GuestEmail,
                GuestFullName: basket.GuestFullName,
                ChargedAmount: basket.FlightSubtotal,
                RefundedAmount: basket.HotelSubtotal,
                Currency: basket.Currency,
                Cause: evt.Cause ?? "hotel supplier failure",
                At: DateTimeOffset.UtcNow)).ConfigureAwait(false);
            return;
        }

        // Flight hasn't captured yet → race: void the single PI so nothing is charged.
        await _payments.VoidAsync(
            basket.StripePaymentIntentId,
            idempotencyKey: $"basket-{basket.BasketId}-void",
            ctx.CancellationToken).ConfigureAwait(false);

        basket.Status = "Failed";
        basket.UpdatedUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ctx.CancellationToken).ConfigureAwait(false);

        await ctx.Publish(new BasketFailed(basket.BasketId, evt.Cause ?? "hotel supplier failure", DateTimeOffset.UtcNow))
            .ConfigureAwait(false);
    }

    // ----- BookingFailed (flight leg) — hard void before any capture -----
    public async Task Consume(ConsumeContext<BookingFailed> ctx)
    {
        var evt = ctx.Message;
        var basket = await _db.Baskets.FirstOrDefaultAsync(
            b => b.FlightBookingId == evt.BookingId, ctx.CancellationToken).ConfigureAwait(false);
        if (basket is null) return;

        if (!await LockEventAsync(basket.BasketId, evt.EventId, nameof(BookingFailed), ctx.CancellationToken)
            .ConfigureAwait(false))
        {
            _log.LogInformation(
                "BasketPaymentOrchestrator: duplicate BookingFailed for basket {BasketId} skipped", basket.BasketId);
            return;
        }

        if (string.IsNullOrEmpty(basket.StripePaymentIntentId)) return;

        // Hard failure: flight never ticketed, so nothing is captured yet. Void the PI.
        await _payments.VoidAsync(
            basket.StripePaymentIntentId,
            idempotencyKey: $"basket-{basket.BasketId}-void",
            ctx.CancellationToken).ConfigureAwait(false);

        basket.Status = "Failed";
        basket.UpdatedUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ctx.CancellationToken).ConfigureAwait(false);

        await ctx.Publish(new BasketFailed(basket.BasketId, evt.Cause, DateTimeOffset.UtcNow))
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Inbox-pattern dedupe on <c>(BasketId, EventId)</c>. Two-phase check:
    /// <list type="number">
    ///   <item>Query first — handles every duplicate under test-time providers (EF InMemory
    ///         does NOT enforce unique indexes) and the overwhelmingly common production path.</item>
    ///   <item>Catch <see cref="DbUpdateException"/> on <see cref="DbContext.SaveChangesAsync(CancellationToken)"/>
    ///         — handles the rare SQL Server race where two consumers insert concurrently and
    ///         the unique index is the final arbiter.</item>
    /// </list>
    /// Returns <c>true</c> on successful insert (we own this event), <c>false</c> on duplicate.
    /// </summary>
    private async Task<bool> LockEventAsync(Guid basketId, Guid eventId, string eventType, CancellationToken ct)
    {
        var exists = await _db.BasketEventLogs
            .AsNoTracking()
            .AnyAsync(x => x.BasketId == basketId && x.EventId == eventId, ct)
            .ConfigureAwait(false);
        if (exists) return false;

        _db.BasketEventLogs.Add(new BasketEventLog
        {
            BasketId = basketId,
            EventId = eventId,
            EventType = eventType,
            HandledAtUtc = DateTime.UtcNow,
        });
        try
        {
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            return true;
        }
        catch (DbUpdateException)
        {
            // SQL Server unique-index race: concurrent inserter won. Detach pending entity.
            var pending = _db.ChangeTracker.Entries<BasketEventLog>()
                .Where(e => e.State == EntityState.Added && e.Entity.BasketId == basketId && e.Entity.EventId == eventId)
                .ToList();
            foreach (var e in pending)
            {
                e.State = EntityState.Detached;
            }
            return false;
        }
    }
}
