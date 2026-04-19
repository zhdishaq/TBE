using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Stripe;
using TBE.Contracts.Events;
using TBE.PaymentService.Application.Stripe;
using TBE.PaymentService.Infrastructure;
using TBE.PaymentService.Infrastructure.Stripe;

namespace TBE.PaymentService.API.Controllers;

/// <summary>
/// W3 boundary: DUMB INGRESS. This controller verifies the Stripe signature, de-duplicates
/// by event.Id, and publishes EXACTLY ONE typed envelope (<see cref="StripeWebhookReceived"/>).
/// It MUST NOT publish any saga event or wallet event directly — those are the responsibility
/// of <c>StripeWebhookConsumer</c> and <c>StripeTopUpConsumer</c>.
/// </summary>
[ApiController]
[Route("webhooks/stripe")]
[AllowAnonymous]
public sealed class StripeWebhookController : ControllerBase
{
    private readonly IOptionsMonitor<StripeOptions> _opts;
    private readonly PaymentDbContext _db;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<StripeWebhookController> _log;

    public StripeWebhookController(
        IOptionsMonitor<StripeOptions> opts,
        PaymentDbContext db,
        IPublishEndpoint publishEndpoint,
        ILogger<StripeWebhookController> log)
    {
        _opts = opts;
        _db = db;
        _publishEndpoint = publishEndpoint;
        _log = log;
    }

    [HttpPost]
    public async Task<IActionResult> HandleAsync(CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body);
        var json = await reader.ReadToEndAsync(ct);
        var sig = Request.Headers["Stripe-Signature"].ToString();

        global::Stripe.Event evt;
        try
        {
            evt = EventUtility.ConstructEvent(
                json, sig, _opts.CurrentValue.WebhookSecret,
                tolerance: 300,
                throwOnApiVersionMismatch: false);
        }
        catch (StripeException)
        {
            return BadRequest();
        }

        if (await _db.StripeWebhookEvents.AsNoTracking().AnyAsync(e => e.EventId == evt.Id, ct))
        {
            return Ok();
        }

        string? paymentIntentId = null;
        Guid? bookingId = null;
        Guid? walletId = null;
        Guid? agencyId = null;
        decimal? topUpAmount = null;

        if (evt.Data.Object is PaymentIntent pi)
        {
            paymentIntentId = pi.Id;
            if (pi.Metadata is { Count: > 0 } md)
            {
                if (md.TryGetValue("booking_id", out var b) && Guid.TryParse(b, out var bg)) bookingId = bg;
                if (md.TryGetValue("wallet_id",  out var w) && Guid.TryParse(w, out var wg)) walletId  = wg;
                if (md.TryGetValue("agency_id",  out var a) && Guid.TryParse(a, out var ag)) agencyId  = ag;
                if (md.TryGetValue("topup_amount", out var t) && decimal.TryParse(t, out var td)) topUpAmount = td;
            }
        }
        else if (evt.Data.Object is Charge ch)
        {
            paymentIntentId = ch.PaymentIntentId;
            if (ch.Metadata is { Count: > 0 } cmd)
            {
                if (cmd.TryGetValue("booking_id", out var b) && Guid.TryParse(b, out var bg)) bookingId = bg;
                if (cmd.TryGetValue("wallet_id",  out var w) && Guid.TryParse(w, out var wg)) walletId  = wg;
                if (cmd.TryGetValue("agency_id",  out var a) && Guid.TryParse(a, out var ag)) agencyId  = ag;
                if (cmd.TryGetValue("topup_amount", out var t) && decimal.TryParse(t, out var td)) topUpAmount = td;
            }
        }

        // Plan 06-02 Task 3 (BO-06 / D-55) — persist the FULL Stripe
        // envelope so the nightly reconciliation job can compare against
        // the wallet ledger. Processed=false; the typed consumer flips
        // it to true once handling completes.
        _db.StripeWebhookEvents.Add(new StripeWebhookEvent
        {
            EventId = evt.Id,
            EventType = evt.Type,
            ReceivedAtUtc = DateTime.UtcNow,
            RawPayload = json,
            Processed = false,
        });

        var envelope = new StripeWebhookReceived(
            evt.Id, evt.Type, paymentIntentId, bookingId, walletId, topUpAmount, agencyId, DateTimeOffset.UtcNow);

        await _publishEndpoint.Publish(envelope, ct);
        await _db.SaveChangesAsync(ct);

        _log.LogInformation("stripe webhook accepted event={EventId} type={EventType}", evt.Id, evt.Type);
        return Ok();
    }
}
